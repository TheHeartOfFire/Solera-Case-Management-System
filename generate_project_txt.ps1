# Generates porject.txt containing a 1:1 copy of every .cs file in the repository
# Usage: pwsh.exe ./generate_project_txt.ps1

$root = Get-Location
$projectTxt = Join-Path $root 'porject.txt'

# Remove existing file if present
if (Test-Path $projectTxt) { Remove-Item -Path $projectTxt -Force }

# Find .cs files excluding obj and bin folders and generated files under obj
$files = Get-ChildItem -Path $root -Recurse -Filter *.cs | Where-Object {
    $_.FullName -notmatch '\\(obj|bin)\\' -and $_.FullName -notmatch '\\obj\\'
} | Sort-Object FullName

foreach ($f in $files) {
    # Compute path relative to repository root
    $rel = $f.FullName.Substring($root.Path.Length + 1)

    Add-Content -Path $projectTxt -Value "===== $rel ====="
    Get-Content -Path $f.FullName -Raw | ForEach-Object {
        # Preserve original lines exactly
        Add-Content -Path $projectTxt -Value $_
    }
    Add-Content -Path $projectTxt -Value ""  # blank line between files
}

Write-Host "Exported $($files.Count) .cs files to $projectTxt" -ForegroundColor Green
