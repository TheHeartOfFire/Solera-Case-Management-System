# PowerShell script to register Solera CMS Native Messaging Host for Chrome and Microsoft Edge
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$manifestPath = Join-Path $scriptDir "com.solera.cms.bridge.json"

# Find AMFormsCST.Desktop.exe path
$exeCandidates = @(
    (Join-Path (Split-Path -Parent $scriptDir) "AMFormsCST.Desktop\bin\Debug\net9.0-windows\AMFormsCST.Desktop.exe"),
    (Join-Path (Split-Path -Parent $scriptDir) "AMFormsCST.Desktop\bin\Release\net9.0-windows\AMFormsCST.Desktop.exe"),
    (Join-Path $env:LocalAppData "AMFormsCST\current\AMFormsCST.Desktop.exe")
)

$targetExe = $null
foreach ($cand in $exeCandidates) {
    if (Test-Path $cand) {
        $targetExe = (Resolve-Path $cand).Path
        break
    }
}

if (-not $targetExe) {
    Write-Warning "Could not find built AMFormsCST.Desktop.exe automatically. Please build the project or specify path."
    $targetExe = "AMFormsCST.Desktop.exe"
}

# Update manifest json path
$jsonContent = Get-Content $manifestPath -Raw | ConvertFrom-Json
$jsonContent.path = $targetExe
$jsonContent | ConvertTo-Json -Depth 5 | Set-Content $manifestPath -Encoding UTF8

Write-Host "Updated $manifestPath with executable path: $targetExe"

# Register in HKCU for Google Chrome
$chromeKey = "HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.solera.cms.bridge"
New-Item -Path $chromeKey -Force | Out-Null
Set-ItemProperty -Path $chromeKey -Name "(Default)" -Value $manifestPath
Write-Host "Registered Chrome host at: $chromeKey"

# Register in HKCU for Microsoft Edge
$edgeKey = "HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\com.solera.cms.bridge"
New-Item -Path $edgeKey -Force | Out-Null
Set-ItemProperty -Path $edgeKey -Name "(Default)" -Value $manifestPath
Write-Host "Registered Edge host at: $edgeKey"

Write-Host "Solera CMS Native Messaging Host registered successfully!" -ForegroundColor Green

