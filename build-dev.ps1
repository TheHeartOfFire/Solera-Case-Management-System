param(
    [string]$Version = "2.10.3-dev.$(Get-Date -Format 'yyyyMMddHHmm')",
    [string]$Runtime = "win-x64",
    [string]$PublishDir = ".\publish-dev",
    [string]$ReleasesDir = ".\Releases.Dev"
)

$ErrorActionPreference = "Stop"

Write-Host "Publishing Dev build ($Version)..." -ForegroundColor Cyan
dotnet publish AMFormsCST.Desktop\AMFormsCST.Desktop.csproj `
    -c Release `
    -r $Runtime `
    --self-contained false `
    -p:DevBuild=true `
    -p:Version=$Version `
    -o $PublishDir

if (Get-Command vpk -ErrorAction SilentlyContinue) {
    Write-Host "Packaging Velopack Dev installer into $ReleasesDir..." -ForegroundColor Cyan
    vpk pack `
        --packId "SoleraCaseManagementSystem.Dev" `
        --packVersion $Version `
        --packDir $PublishDir `
        --packTitle "Solera Case Management System (Dev)" `
        --mainExe "AMFormsCST.Desktop.exe" `
        --channel "dev" `
        --outputDir $ReleasesDir

    Write-Host "Done! Dev installer ready in $ReleasesDir" -ForegroundColor Green
}
else {
    Write-Host "Velopack CLI ('vpk') not found. Install it with: dotnet tool install -g vpk" -ForegroundColor Yellow
    Write-Host "Standalone Dev publish output is ready in $PublishDir" -ForegroundColor Green
}

