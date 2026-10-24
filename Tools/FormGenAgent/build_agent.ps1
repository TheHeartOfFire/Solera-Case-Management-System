# Build script for FormGenAgent.jar
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceFile = Join-Path $scriptDir "FormGenAgent.java"
$manifestFile = Join-Path $scriptDir "MANIFEST.MF"
$outputJar = Join-Path $scriptDir "FormGenAgent.jar"

# Look for javac
$javac = "javac.exe"
$adoptiumJavac = Get-ChildItem "C:\Program Files\Eclipse Adoptium\*\bin\javac.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($adoptiumJavac) {
    $javac = $adoptiumJavac.FullName
} elseif (Get-Command javac.exe -ErrorAction SilentlyContinue) {
    $javac = (Get-Command javac.exe).Source
}

# Look for jar
$jar = "jar.exe"
$adoptiumJar = Get-ChildItem "C:\Program Files\Eclipse Adoptium\*\bin\jar.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($adoptiumJar) {
    $jar = $adoptiumJar.FullName
} elseif (Get-Command jar.exe -ErrorAction SilentlyContinue) {
    $jar = (Get-Command jar.exe).Source
}

# Look for swt jar (optional since code uses pure reflection)
$swtJar = "$env:APPDATA\NewFormGen\swt-4.10-win.jar"
if (-not (Test-Path $swtJar)) {
    $found = Get-ChildItem -Path "$env:USERPROFILE" -Filter "swt-*.jar" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { $swtJar = $found.FullName }
}

Write-Host "Compiling FormGenAgent with $javac (--release 8)..."
$buildDir = Join-Path $scriptDir "bin"
if (Test-Path $buildDir) { Remove-Item -Recurse -Force $buildDir }
New-Item -ItemType Directory -Path $buildDir | Out-Null

if (Test-Path $swtJar) {
    & $javac --release 8 -cp "$swtJar" -d "$buildDir" "$sourceFile"
} else {
    & $javac --release 8 -d "$buildDir" "$sourceFile"
}
if ($LASTEXITCODE -ne 0) { throw "javac failed with code $LASTEXITCODE" }

Write-Host "Packaging $outputJar with $jar..."
& $jar cfm "$outputJar" "$manifestFile" -C "$buildDir" .
if ($LASTEXITCODE -ne 0) { throw "jar packaging failed with code $LASTEXITCODE" }

Remove-Item -Recurse -Force $buildDir
Write-Host "Successfully built $outputJar"
