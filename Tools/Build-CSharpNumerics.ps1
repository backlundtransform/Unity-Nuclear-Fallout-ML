<#
.SYNOPSIS
    Builds CSharpNumerics.dll from the git submodule source and copies it to Assets/Plugins/.

.DESCRIPTION
    Builds the netstandard2.1 target (Unity-compatible) from the CSharpNumerics
    submodule at External/CSharpNumerics/ and copies the output DLL to
    Assets/Plugins/CSharpNumerics/.

.EXAMPLE
    .\Tools\Build-CSharpNumerics.ps1
    .\Tools\Build-CSharpNumerics.ps1 -Configuration Debug
#>
param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $repoRoot "External/CSharpNumerics/Numerics/Numerics/CSharpNumerics.csproj"
$pluginDir = Join-Path $repoRoot "Assets/Plugins/CSharpNumerics"

if (-not (Test-Path $csproj)) {
    Write-Error @"
CSharpNumerics.csproj not found at: $csproj
Did you initialize the submodule?
  git submodule update --init --recursive
"@
    exit 1
}

Write-Host "Building CSharpNumerics ($Configuration, netstandard2.1)..." -ForegroundColor Cyan
dotnet build $csproj -c $Configuration -f netstandard2.1 --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# Locate built DLL
$buildOutput = Join-Path (Split-Path $csproj) "bin/$Configuration/netstandard2.1"
$dll = Join-Path $buildOutput "CSharpNumerics.dll"

if (-not (Test-Path $dll)) {
    Write-Error "Built DLL not found at: $dll"
    exit 1
}

# Copy to Plugins folder
if (-not (Test-Path $pluginDir)) {
    New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
}

Copy-Item $dll -Destination $pluginDir -Force
Write-Host "Copied CSharpNumerics.dll -> $pluginDir" -ForegroundColor Green

# Also copy XML docs if available
$xmlDoc = Join-Path $buildOutput "CSharpNumerics.xml"
if (Test-Path $xmlDoc) {
    Copy-Item $xmlDoc -Destination $pluginDir -Force
    Write-Host "Copied CSharpNumerics.xml -> $pluginDir" -ForegroundColor Green
}

Write-Host "Done." -ForegroundColor Green
