#requires -version 5.0
<#
.SYNOPSIS
    Package the vscode-msgchain extension into a .vsix file.

.DESCRIPTION
    Updates the version field in tools/vscode-msgchain/package.json, then runs
    `vsce package` to produce a .vsix in the repository's dist/ directory.
    Requires the @vscode/vsce CLI to be installed and available on PATH.

.PARAMETER Version
    Semantic version to stamp into package.json before packaging, e.g. 0.1.0.

.PARAMETER SkipVersionWrite
    Do not modify package.json; package whatever version is currently set.
    Useful when -Version was already committed manually.

.EXAMPLE
    .\vscode-pack.ps1 -Version 0.1.0

.EXAMPLE
    .\vscode-pack.ps1 -Version 0.2.0 -SkipVersionWrite
#>

param(
    [Parameter(Mandatory=$true, Position=0)]
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z\.-]+)?$')]
    [string]$Version,

    [Parameter(Mandatory=$false)]
    [switch]$SkipVersionWrite
)

$ErrorActionPreference = 'Stop'

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ExtensionDir = Join-Path $ScriptRoot 'tools\vscode-msgchain'
$PackageJson = Join-Path $ExtensionDir 'package.json'

if (-not (Test-Path $PackageJson)) {
    throw "package.json not found at $PackageJson"
}

# ----------------------------------------------------------------------------
# Locate vsce
# ----------------------------------------------------------------------------
$Vsce = Get-Command vsce -ErrorAction SilentlyContinue
if (-not $Vsce) {
    throw "vsce CLI not found. Install it with: npm install -g @vscode/vsce"
}

# ----------------------------------------------------------------------------
# Update version in package.json (preserving formatting as best we can)
# ----------------------------------------------------------------------------
if (-not $SkipVersionWrite) {
    Write-Host ">> Setting version to $Version in package.json" -ForegroundColor Cyan
    $content = Get-Content -Path $PackageJson -Raw -Encoding UTF8
    $pattern = '("version"\s*:\s*")[^"]+(")'
    if ($content -notmatch $pattern) {
        throw "Could not find a 'version' field in $PackageJson"
    }
    $updated = [regex]::Replace($content, $pattern, "`${1}$Version`${2}")
    # Write UTF-8 without BOM to match typical package.json conventions.
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($PackageJson, $updated, $utf8NoBom)
}
else {
    Write-Host ">> Skipping package.json version write (-SkipVersionWrite)" -ForegroundColor DarkGray
}

# ----------------------------------------------------------------------------
# Resolve output path (always dist/ at repo root)
# ----------------------------------------------------------------------------
$OutDirAbs = Join-Path $ScriptRoot 'dist'
if (-not (Test-Path $OutDirAbs)) {
    New-Item -ItemType Directory -Path $OutDirAbs | Out-Null
}
$VsixPath = Join-Path $OutDirAbs "vscode-msgchain-$Version.vsix"

# ----------------------------------------------------------------------------
# Run vsce package
# ----------------------------------------------------------------------------
Write-Host ">> Packaging extension" -ForegroundColor Cyan
Write-Host "   Source : $ExtensionDir"
Write-Host "   Output : $VsixPath"

Push-Location $ExtensionDir
try {
    & vsce package --out $VsixPath
    if ($LASTEXITCODE -ne 0) {
        throw "vsce package failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "OK: created $VsixPath" -ForegroundColor Green
Write-Host ""
Write-Host "Install locally with:" -ForegroundColor Yellow
Write-Host "  code --install-extension `"$VsixPath`""
