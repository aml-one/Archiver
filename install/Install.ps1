<#
.SYNOPSIS
    Installs the Archiver Windows Service from the pre-built publish folder.

.DESCRIPTION
    Copies the pre-published binaries from the publish folder, registers the
    Windows Service, creates config/log directories, and starts the service.

.PARAMETER ServiceName
    Windows Service name. Default: "Archiver"

.PARAMETER ConfigPath
    Directory for config and logs. Default: "C:\ProgramData\Archiver"

.PARAMETER ServiceAccount
    Windows account to run the service as. Default: "LocalSystem"
    For network share access with machine credentials, use a domain account:
      "DOMAIN\svc_archiver"
    Or keep LocalSystem and use the credential entries in config.json.

.PARAMETER ServicePassword
    Password for the ServiceAccount. Only needed when ServiceAccount is not LocalSystem.

.EXAMPLE
    .\Install.ps1
    Installs with defaults, using the built publish folder.

.EXAMPLE
    .\Install.ps1 -ServiceAccount "DOMAIN\svc_backup" -ServicePassword "p@ss!"
    Installs running as a domain service account.
#>

param(
    [string]$ServiceName = "Archiver",
    [string]$ServiceDisplayName = "Archiver - File Archive Service",
    [string]$ServiceDescription = "Copies files to network shares and external drives on a schedule.",
    [string]$ConfigPath = "C:\ProgramData\Archiver",
    [string]$InstallPath = "C:\ProgramData\Archiver\Service",
    [string]$ServiceAccount = "LocalSystem",
    [string]$ServicePassword = ""
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishSource = Resolve-Path "$scriptRoot\..\publish"

if (-not (Test-Path $publishSource)) {
    Write-Host "ERROR: Publish folder not found at: $publishSource" -ForegroundColor Red
    Write-Host "Run 'dotnet publish .\Archiver.slnx -c Release -r win-x64 -p:PublishSingleFile=true -o .\publish' first." -ForegroundColor Yellow
    exit 1
}

Write-Host "=== Archiver Service Installer ===" -ForegroundColor Cyan
Write-Host ""

# ── 1. Copy binaries from publish folder ──────────────────────────
Write-Host "[1/6] Copying binaries from $publishSource..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
Copy-Item -Recurse -Force "$publishSource\*" "$InstallPath\" -Exclude "*.pdb"
Write-Host "       Installed to: $InstallPath" -ForegroundColor Green

# ── 2. Create directories ─────────────────────────────────────────
Write-Host "[2/6] Creating directories..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path $ConfigPath | Out-Null
New-Item -ItemType Directory -Force -Path "$ConfigPath\logs" | Out-Null
Write-Host "       Config: $ConfigPath" -ForegroundColor Green
Write-Host "       Logs:   $ConfigPath\logs" -ForegroundColor Green

# ── 3. Check configuration ────────────────────────────────────────
Write-Host "[3/6] Checking configuration..." -ForegroundColor Yellow
if (-not (Test-Path "$ConfigPath\config.json")) {
    Write-Host "       No config found. A default config will be created on first service start." -ForegroundColor Yellow
    Write-Host "       Edit $ConfigPath\config.json to configure your jobs." -ForegroundColor Yellow
    Write-Host "       Use the Archiver.UI WPF app for easy configuration." -ForegroundColor Yellow
}
else {
    Write-Host "       Config already exists at $ConfigPath\config.json" -ForegroundColor Green
}

# ── 4. Remove existing service if present ─────────────────────────
Write-Host "[4/6] Registering Windows Service..." -ForegroundColor Yellow
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "       Stopping existing service..." -ForegroundColor Yellow
    Stop-Service $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    sc.exe delete $ServiceName
    Start-Sleep -Seconds 2
    Write-Host "       Removed previous installation." -ForegroundColor Green
}

# ── 5. Create the service ─────────────────────────────────────────
$binaryPath = "`"$InstallPath\Archiver.Service.exe`""

$serviceParams = @{
    Name            = $ServiceName
    BinaryPathName  = $binaryPath
    DisplayName     = $ServiceDisplayName
    Description     = $ServiceDescription
    StartupType     = "Automatic"
}

if ($ServiceAccount -ne "LocalSystem" -and $ServicePassword) {
    $credential = New-Object System.Management.Automation.PSCredential(
        $ServiceAccount,
        (ConvertTo-SecureString $ServicePassword -AsPlainText -Force))
    $serviceParams["Credential"] = $credential
}

New-Service @serviceParams
Write-Host "       Service created." -ForegroundColor Green

# ── 6. Configure recovery and start ───────────────────────────────
Write-Host "[5/6] Configuring service recovery..." -ForegroundColor Yellow
sc.exe failure $ServiceName reset=86400 actions=restart/60000/restart/60000/restart/60000 | Out-Null
Write-Host "       Recovery: restart on failure (3 attempts, 60s delay)" -ForegroundColor Green

Write-Host "[6/6] Starting service..." -ForegroundColor Yellow
Start-Service $ServiceName
Write-Host "       Service started!" -ForegroundColor Green

Write-Host ""
Write-Host "=== Installation Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Service:    $ServiceDisplayName"
Write-Host "  Status:     Running"
Write-Host "  Config:     $ConfigPath\config.json"
Write-Host "  Logs:       $ConfigPath\logs\"
Write-Host "  UI Tool:    $InstallPath\Archiver.UI.exe"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Launch $InstallPath\Archiver.UI.exe to configure jobs"
Write-Host "  2. Or edit $ConfigPath\config.json directly"
Write-Host "  3. Check logs at $ConfigPath\logs\"
Write-Host "  4. Use 'services.msc' to manage the service"
Write-Host ""
