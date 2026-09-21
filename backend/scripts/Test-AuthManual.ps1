param([string]$BaseUrl = 'http://localhost:5080')

$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')
$securePassword = $null
$password = $null
$body = $null
$login = $null
$headers = $null

try {
    $username = Read-Host 'Username (3-32 letters, digits or underscores; use a new account)'
    $nickname = Read-Host 'Nickname (1-32 characters)'
    $securePassword = Read-Host 'Password (8-128 characters; input is hidden)' -AsSecureString
    $password = [System.Net.NetworkCredential]::new('', $securePassword).Password

    $body = @{ username = $username; password = $password; nickname = $nickname } | ConvertTo-Json
    $registered = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/auth/register" -ContentType 'application/json; charset=utf-8' -Body ([System.Text.Encoding]::UTF8.GetBytes($body))
    Write-Output "PASS: Registration succeeded. User ID: $($registered.id)"

    $body = @{ username = $username; password = $password } | ConvertTo-Json
    $login = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/auth/login" -ContentType 'application/json; charset=utf-8' -Body ([System.Text.Encoding]::UTF8.GetBytes($body))
    if ([string]::IsNullOrWhiteSpace($login.accessToken)) {
        throw 'Login response did not contain an access token.'
    }
    Write-Output "PASS: Login succeeded. Token expires at: $($login.expiresAtUtc)"

    $headers = @{ Authorization = "Bearer $($login.accessToken)" }
    $me = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/auth/me" -Headers $headers
    if ($null -eq $me.id -or $me.id -ne $registered.id) {
        throw 'Authenticated user ID does not match the registered user ID.'
    }
    Write-Output "PASS: Bearer authentication succeeded. User ID: $($me.id)"
    Write-Output 'PASS: All checks completed. The test account remains in the database.'
}
catch {
    Write-Output 'FAIL: Test stopped. Check that the API is running and review the HTTP status below.'
    Write-Output 'If registration already succeeded, use a new username when running this script again.'
    throw
}
finally {
    $password = $null
    $body = $null
    $login = $null
    $headers = $null
    if ($null -ne $securePassword) {
        $securePassword.Dispose()
    }
}
