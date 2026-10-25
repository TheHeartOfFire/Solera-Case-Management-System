param(
    [string]$ExtensionId
)

# PowerShell script to register Solera CMS Native Messaging Host for Chrome, Edge, and Opera GX
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$localExtDir = Join-Path $env:LocalAppData "AMFormsCST\BrowserExtension"
New-Item -ItemType Directory -Path $localExtDir -Force | Out-Null
Copy-Item -Path (Join-Path $scriptDir "*") -Destination $localExtDir -Force
$manifestPath = Join-Path $localExtDir "com.solera.cms.bridge.json"

# Find AMFormsCST.Desktop.exe path
$exeCandidates = @(
    (Join-Path (Split-Path -Parent $scriptDir) "AMFormsCST.Desktop\bin\Debug\net9.0-windows\AMFormsCST.Desktop.exe"),
    (Join-Path (Split-Path -Parent $scriptDir) "AMFormsCST.Desktop\bin\Release\net9.0-windows\AMFormsCST.Desktop.exe"),
    (Join-Path $env:LocalAppData "AMFormsCST\current\AMFormsCST.Desktop.exe"),
    (Join-Path $env:LocalAppData "SoleraCaseManagement\current\AMFormsCST.Desktop.exe")
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

# Update manifest json path and allowed_origins in LocalAppData
$jsonContent = Get-Content $manifestPath -Raw | ConvertFrom-Json
$jsonContent.path = $targetExe

if ($ExtensionId) {
    $cleanId = $ExtensionId.Trim().TrimStart("chrome-extension://").TrimEnd("/")
    $origin = "chrome-extension://$cleanId/"
    if (-not $jsonContent.allowed_origins) {
        $jsonContent.allowed_origins = @()
    }
    if ($jsonContent.allowed_origins -notcontains $origin) {
        $jsonContent.allowed_origins += $origin
    }
    Write-Host "Added allowed origin: $origin" -ForegroundColor Cyan
}

$jsonContent | ConvertTo-Json -Depth 5 | Set-Content $manifestPath -Encoding UTF8

Write-Host "Updated $manifestPath with executable path: $targetExe"

$registryTargets = @(
    @{ Name = "Google Chrome"; Key = "HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.solera.cms.bridge" },
    @{ Name = "Microsoft Edge"; Key = "HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\com.solera.cms.bridge" },
    @{ Name = "Opera GX"; Key = "HKCU:\Software\Opera Software\Opera GX Stable\NativeMessagingHosts\com.solera.cms.bridge" },
    @{ Name = "Opera Stable"; Key = "HKCU:\Software\Opera Software\Opera Stable\NativeMessagingHosts\com.solera.cms.bridge" }
)

foreach ($target in $registryTargets) {
    try {
        New-Item -Path $target.Key -Force | Out-Null
        Set-ItemProperty -Path $target.Key -Name "(Default)" -Value $manifestPath
        Write-Host "Registered $($target.Name) host at: $($target.Key)"
    } catch {
        Write-Warning "Failed to register for $($target.Name): $_"
    }
}

Write-Host "`nSolera CMS Native Messaging Host registered successfully!" -ForegroundColor Green

