param(
    [string]$Label = 'client',
    [string]$Unity = 'D:\openAI\unity\6000.6.2f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source = Join-Path $workspace 'client/SocialSystem.Unity'
$validation = Join-Path $workspace 'client/Build/Stage8Validation'
New-Item -ItemType Directory -Path $validation -Force | Out-Null
foreach ($folder in @('Assets','Packages','ProjectSettings')) {
    Copy-Item -LiteralPath (Join-Path $source $folder) -Destination $validation -Recurse -Force
}
$log = Join-Path $validation ($Label + '.log')
$process = Start-Process -FilePath $Unity -ArgumentList @('-batchmode','-nographics','-quit','-projectPath',('"' + $validation + '"'),'-executeMethod','ClientProjectSetup.CreateAndBuild','-logFile',('"' + $log + '"')) -WindowStyle Hidden -PassThru
$deadline = [DateTime]::UtcNow.AddMinutes(12)
while (-not $process.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $deadline) { Stop-Process -Id $process.Id; throw 'Unity build timed out.' }
}
$errors = @(Select-String -LiteralPath $log -Pattern 'error CS[0-9]+|Scripts have compiler errors')
if ($errors.Count -gt 0 -or $process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch 'PASS: Windows development client built.')) {
    Get-Content -LiteralPath $log -Tail 70
    throw ('Unity build failed: ' + $Label)
}
Write-Output ('PASS: ' + $Label + ' Unity Windows build; no C# compiler errors. Log: ' + $log)

