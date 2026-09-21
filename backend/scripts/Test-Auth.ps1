param(
    [string]$MySqlBin = 'C:\Program Files\MySQL\MySQL Server 8.0\bin',
    [string]$Dotnet = 'C:\Program Files\dotnet\dotnet.exe'
)
$ErrorActionPreference = 'Stop'
$backend = Split-Path $PSScriptRoot -Parent
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
    & $Dotnet test (Join-Path $backend 'SocialSystem.sln') --no-build --no-restore --logger 'console;verbosity=normal'
    if ($LASTEXITCODE -ne 0) { throw 'Authentication tests failed.' }
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
