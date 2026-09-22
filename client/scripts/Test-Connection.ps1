param(
    [string]$UnityProject = '',
    [switch]$CaptureUi,
    [ValidateSet('connection', 'social')][string]$Mode = 'connection',
    [string]$MySqlBin = 'C:\Program Files\MySQL\MySQL Server 8.0\bin',
    [string]$Dotnet = 'C:\Program Files\dotnet\dotnet.exe',
    [string]$Unity = 'D:\openAI\unity\6000.6.2f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$backend = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'backend'
$workspace = [IO.Path]::GetFullPath((Split-Path $backend -Parent))
$probe = Join-Path $workspace ('.db-validation-auth-' + [guid]::NewGuid().ToString('N'))
$server = $null
$oldTestConnection = $env:SOCIAL_TEST_MYSQL
$oldMysqlPassword = $env:MYSQL_PWD
$oldCliHome = $env:DOTNET_CLI_HOME
$oldCertSetting = $env:DOTNET_GENERATE_ASPNET_CERTIFICATE
try {
    New-Item -ItemType Directory -Path $probe | Out-Null
    $env:DOTNET_CLI_HOME = Join-Path $probe 'dotnet-home'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:MYSQL_PWD = $null
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = $listener.LocalEndpoint.Port
    $listener.Stop()
    $config = Join-Path $probe 'my.ini'
    @(
        '[mysqld]'
        ('basedir=' + (Split-Path $MySqlBin -Parent).Replace('\','/'))
        ('datadir=' + (Join-Path $probe 'data').Replace('\','/'))
        "port=$port"
        'bind-address=127.0.0.1'
        'mysqlx=0'
        'skip-log-bin'
        'innodb_buffer_pool_size=64M'
        'default-time-zone=+00:00'
        ('log-error=' + (Join-Path $probe 'mysql-error.log').Replace('\','/'))
    ) | Set-Content -LiteralPath $config -Encoding utf8
    $mysqld = Join-Path $MySqlBin 'mysqld.exe'
    $mysql = Join-Path $MySqlBin 'mysql.exe'
    & $mysqld "--defaults-file=$config" --initialize-insecure
    if ($LASTEXITCODE -ne 0) { throw 'Isolated MySQL initialization failed.' }
    $server = Start-Process -FilePath $mysqld -ArgumentList ('--defaults-file="' + $config + '"') -WindowStyle Hidden -PassThru
    $mysqlArgs = @('--no-defaults','--protocol=TCP','--host=127.0.0.1',"--port=$port",'--user=root','--default-character-set=utf8mb4','--batch')
    $ready = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        & $mysql @mysqlArgs --execute='SELECT 1' 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        Start-Sleep -Milliseconds 250
    }
    if (-not $ready) { throw 'Isolated MySQL did not become ready.' }
    $sql = (Get-Content (Join-Path $workspace 'database/01_init.sql') -Raw -Encoding utf8).Replace('social_system','social_system_auth_tests')
    $sql | & $mysql @mysqlArgs
    if ($LASTEXITCODE -ne 0) { throw 'Test schema initialization failed.' }
    $password = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    "ALTER USER 'root'@'localhost' IDENTIFIED BY '$password';" | & $mysql @mysqlArgs
    if ($LASTEXITCODE -ne 0) { throw 'Test account configuration failed.' }
    $env:MYSQL_PWD = $password
    $env:SOCIAL_TEST_MYSQL = "Server=127.0.0.1;Port=$port;Database=social_system_auth_tests;User ID=root;Password=$password;DateTimeKind=Utc;Connection Timeout=5"
    $apiProcess = $null
    $unityProcess = $null
    try {
        $apiListener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
        $apiListener.Start()
        $apiPort = $apiListener.LocalEndpoint.Port
        $apiListener.Stop()
        $url = "http://127.0.0.1:$apiPort"
        $apiRoot = Join-Path $backend 'SocialSystem.Api'
        $apiDll = Join-Path $apiRoot 'bin/Debug/net9.0/SocialSystem.Api.dll'
        if (-not (Test-Path -LiteralPath $apiDll)) { throw 'Build the backend first: dotnet build backend/SocialSystem.sln' }
        $apiProcess = Start-Process -FilePath $Dotnet -ArgumentList @(('"'+$apiDll+'"'),'--environment','Testing','--urls',$url) -WorkingDirectory $apiRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $probe 'api.log') -RedirectStandardError (Join-Path $probe 'api-error.log') -Environment @{
            ConnectionStrings__SocialSystem = $env:SOCIAL_TEST_MYSQL
            Jwt__SigningKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
            DOTNET_ENVIRONMENT = 'Testing'
            ASPNETCORE_ENVIRONMENT = 'Testing'
        }
        $apiReady = $false
        for ($attempt = 0; $attempt -lt 40; $attempt++) {
            try {
                $response = Invoke-WebRequest "$url/api/auth/me" -SkipHttpErrorCheck -TimeoutSec 2
                if ($response.StatusCode -eq 401) { $apiReady = $true; break }
            } catch { Start-Sleep -Milliseconds 250 }
        }
        if (-not $apiReady) { throw 'Isolated backend did not start.' }
        $unityProject = if ([string]::IsNullOrWhiteSpace($UnityProject)) { Join-Path $workspace 'client/SocialSystem.Unity' } else { [IO.Path]::GetFullPath($UnityProject) }
        $unityLog = Join-Path $probe 'unity-smoke.log'
        $graphicsArgs = if ($CaptureUi) { @() } else { @('-nographics') }
        $unityProcess = Start-Process -FilePath $Unity -ArgumentList (@('-batchmode') + $graphicsArgs + @('-projectPath',('"'+$unityProject+'"'),'-executeMethod','ClientSmokeRunner.Run','-logFile',('"'+$unityLog+'"'))) -WindowStyle Hidden -PassThru -Environment @{
            SOCIAL_UNITY_TEST_MODE = $Mode
            SOCIAL_UNITY_TEST_URL = $url
            SOCIAL_UNITY_TEST_USERNAME = ('unity_' + [guid]::NewGuid().ToString('N').Substring(0,20))
            SOCIAL_UNITY_TEST_PASSWORD = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
        }
        $deadline = [DateTime]::UtcNow.AddMinutes(4)
        while (-not $unityProcess.WaitForExit(1000)) {
            if ([DateTime]::UtcNow -gt $deadline) { throw 'Unity smoke test timed out.' }
        }
        $marker = if ($Mode -eq 'social') { 'PASS: Unity social modules' } else { 'PASS: Unity registration' }
        $passed = Select-String -LiteralPath $unityLog -SimpleMatch $marker
        if ($unityProcess.ExitCode -ne 0 -or -not $passed) {
            Select-String -LiteralPath $unityLog -Pattern 'error CS|Client smoke test failed|Exception|Aborting batchmode' | Select-Object -Last 12 | ForEach-Object { $_.Line }
            throw 'Unity connection verification failed.'
        }
        Select-String -LiteralPath $unityLog -Pattern 'PASS: (Social|Unity)' | ForEach-Object { $_.Line }
    }
    finally {
        foreach ($process in @($unityProcess, $apiProcess)) {
            if ($null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id; $process.WaitForExit() }
        }
    }
}
finally {
    if ($null -ne $server -and -not $server.HasExited) {
        & (Join-Path $MySqlBin 'mysqladmin.exe') --no-defaults --protocol=TCP --host=127.0.0.1 "--port=$port" --user=root shutdown 2>$null
        if (-not $server.WaitForExit(10000)) { Stop-Process -Id $server.Id; $server.WaitForExit() }
    }
    $env:SOCIAL_TEST_MYSQL = $oldTestConnection
    $env:MYSQL_PWD = $oldMysqlPassword
    $env:DOTNET_CLI_HOME = $oldCliHome
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = $oldCertSetting
    $resolvedProbe = [IO.Path]::GetFullPath($probe)
    if ((Split-Path $resolvedProbe -Parent) -ne $workspace -or (Split-Path $resolvedProbe -Leaf) -notlike '.db-validation-auth-*') {
        throw 'Refusing cleanup outside the isolated validation directory.'
    }
    if (Test-Path -LiteralPath $resolvedProbe) { Remove-Item -LiteralPath $resolvedProbe -Recurse -Force }
}
