# Build script for FormGenAgent.jar
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceFile = Join-Path $scriptDir "FormGenAgent.java"
$manifestFile = Join-Path $scriptDir "MANIFEST.MF"
$outputJar = Join-Path $scriptDir "FormGenAgent.jar"

# Look for javac
$javac = "javac.exe"
if (Test-Path "C:\Program Files\Eclipse Adoptium\jdk-8.0.472.8-hotspot\bin\javac.exe") {
    $javac = "C:\Program Files\Eclipse Adoptium\jdk-8.0.472.8-hotspot\bin\javac.exe"
}
$jar = "jar.exe"
if (Test-Path "C:\Program Files\Eclipse Adoptium\jdk-8.0.472.8-hotspot\bin\jar.exe") {
    $jar = "C:\Program Files\Eclipse Adoptium\jdk-8.0.472.8-hotspot\bin\jar.exe"
}

# Look for swt jar
$swtJar = "$env:APPDATA\NewFormGen\swt-4.10-win.jar"
if (-not (Test-Path $swtJar)) {
    Write-Warning "Could not find swt-4.10-win.jar in NewFormGen directory, looking across repo..."
    $found = Get-ChildItem -Path "$env:USERPROFILE" -Filter "swt-*.jar" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { $swtJar = $found.FullName }
}

Write-Host "Compiling FormGenAgent with $javac and classpath $swtJar..."
$buildDir = Join-Path $scriptDir "bin"
if (Test-Path $buildDir) { Remove-Item -Recurse -Force $buildDir }
New-Item -ItemType Directory -Path $buildDir | Out-Null

& $javac -cp "$swtJar" -d "$buildDir" "$sourceFile"
if ($LASTEXITCODE -ne 0) { throw "javac failed with code $LASTEXITCODE" }

Write-Host "Packaging $outputJar..."
& $jar cfm "$outputJar" "$manifestFile" -C "$buildDir" .
if ($LASTEXITCODE -ne 0) { throw "jar packaging failed with code $LASTEXITCODE" }

Remove-Item -Recurse -Force $buildDir
Write-Host "Successfully built $outputJar"

