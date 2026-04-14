<#
.SYNOPSIS
    Builds and optionally installs the Voice Input MSI.

.DESCRIPTION
    1. Publishes VoiceInput.App as self-contained single-file for win-x64
    2. Builds MSI installer using WiX Toolset
    3. Optionally: silent uninstall old → silent install new

.PARAMETER Configuration
    Build configuration (default: Release)

.PARAMETER Version
    Product version for the MSI (default: 1.0.0.0)

.PARAMETER Install
    After building, silently uninstall previous version and install new one.

.PARAMETER Uninstall
    Silently uninstall and exit.

.PARAMETER Verbose
    Show msiexec output (default: silent /qn).

.EXAMPLE
    .\build-installer.ps1                          # build only
    .\build-installer.ps1 -Install                 # build + silent reinstall
    .\build-installer.ps1 -Install -Verbose        # build + verbose reinstall
    .\build-installer.ps1 -Uninstall               # silent uninstall only
#>
param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0.0",
    [switch]$Install,
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$msiPath = Join-Path $root "installer\VoiceInputSetup.msi"
$productName = "Aether Voice"
# Must match UpgradeCode in VoiceInput.wxs
$upgradeCode = "9B47EC21-85FB-4BF8-8B1D-716B7FA0386E"
$uiLevel = if ($VerbosePreference -eq 'Continue' -or $PSBoundParameters.ContainsKey('Verbose')) { "/qb" } else { "/qn" }

# ── Helper: find installed product code by UpgradeCode ────────────────
function Get-InstalledProductCode {
    try {
        $installer = New-Object -ComObject WindowsInstaller.Installer
        $related = $installer.RelatedProducts($upgradeCode)
        foreach ($code in $related) { return $code }
    } catch {}
    return $null
}

# ── Helper: stop running app ─────────────────────────────────────────
function Stop-VoiceInput {
    $procs = Get-Process -Name "VoiceInput.App" -ErrorAction SilentlyContinue
    if ($procs) {
        Write-Host "  Stopping VoiceInput.App..." -ForegroundColor Yellow
        $procs | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }
}

# ── Uninstall only ────────────────────────────────────────────────────
if ($Uninstall) {
    Write-Host "=== Voice Input Uninstall ===" -ForegroundColor Cyan
    Stop-VoiceInput
    $productCode = Get-InstalledProductCode
    if ($productCode) {
        Write-Host "  Uninstalling $productCode..." -ForegroundColor Yellow
        $proc = Start-Process msiexec -ArgumentList "/x $productCode $uiLevel /norestart" -Wait -PassThru
        if ($proc.ExitCode -eq 0) { Write-Host "  Uninstalled." -ForegroundColor Green }
        else { Write-Host "  msiexec exit code: $($proc.ExitCode)" -ForegroundColor Red }
    } else {
        Write-Host "  Not installed." -ForegroundColor DarkGray
    }
    return
}

# ── Step 1: Publish ──────────────────────────────────────────────────
Write-Host "=== Voice Input Installer Build ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration"
Write-Host "Version:       $Version"
Write-Host ""

Write-Host "[1/3] Publishing VoiceInput.App (win-x64, self-contained)..." -ForegroundColor Yellow

$publishDir = Join-Path $root "publish\win-x64"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

dotnet publish "$root\src\VoiceInput.App\VoiceInput.App.csproj" `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -p:Version=$Version `
    -o $publishDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$exe = Get-Item "$publishDir\VoiceInput.App.exe"
Write-Host "  Published: $($exe.Name) ($([math]::Round($exe.Length/1MB,1)) MB)" -ForegroundColor Green

# ── Step 2: Clean previous installer ────────────────────────────────
Write-Host "[2/3] Cleaning previous build artifacts..." -ForegroundColor Yellow

$installerDir = Join-Path $root "installer"
Remove-Item "$installerDir\VoiceInputSetup.msi" -ErrorAction SilentlyContinue
Remove-Item "$installerDir\VoiceInputSetup.wixpdb" -ErrorAction SilentlyContinue
Remove-Item "$installerDir\cab1.cab" -ErrorAction SilentlyContinue

# ── Step 3: Build MSI ───────────────────────────────────────────────
Write-Host "[3/3] Building MSI installer with WiX..." -ForegroundColor Yellow

$env:Path += ";$env:USERPROFILE\.dotnet\tools"
$wxsPath = Join-Path $installerDir "VoiceInput.wxs"

wix build $wxsPath `
    -ext WixToolset.UI.wixext `
    -d "SourceDir=$publishDir" `
    -o $msiPath

if ($LASTEXITCODE -ne 0) { throw "wix build failed" }

$msi = Get-Item $msiPath
Write-Host ""
Write-Host "=== Build Complete ===" -ForegroundColor Green
Write-Host "MSI: $($msi.FullName)" -ForegroundColor Green
Write-Host "Size: $([math]::Round($msi.Length/1MB,1)) MB" -ForegroundColor Green

# ── Step 4 (optional): Install ──────────────────────────────────────
if ($Install) {
    Write-Host ""
    Write-Host "=== Installing ===" -ForegroundColor Cyan

    Stop-VoiceInput

    # Uninstall previous version if present
    $productCode = Get-InstalledProductCode
    if ($productCode) {
        Write-Host "  Removing previous installation..." -ForegroundColor Yellow
        $proc = Start-Process msiexec -ArgumentList "/x $productCode $uiLevel /norestart" -Wait -PassThru
        if ($proc.ExitCode -ne 0 -and $proc.ExitCode -ne 1605) {
            Write-Host "  Warning: uninstall exit code $($proc.ExitCode)" -ForegroundColor DarkYellow
        }
        Start-Sleep -Seconds 1
    }

    # Install new
    Write-Host "  Installing $msiPath..." -ForegroundColor Yellow
    $proc = Start-Process msiexec -ArgumentList "/i `"$msiPath`" $uiLevel /norestart" -Wait -PassThru
    if ($proc.ExitCode -eq 0) {
        Write-Host "  Installed successfully." -ForegroundColor Green

        # Launch
        $appExe = "$env:LOCALAPPDATA\AetherVoice\App\VoiceInput.App.exe"
        if (Test-Path $appExe) {
            Write-Host "  Launching..." -ForegroundColor Green
            Start-Process $appExe
        }
    } else {
        Write-Host "  msiexec exit code: $($proc.ExitCode)" -ForegroundColor Red
    }
}
