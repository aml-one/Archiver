<#
.SYNOPSIS
    Uninstalls the Archiver Windows Service.

.DESCRIPTION
    Stops and removes the Archiver Windows Service.
    Config files and logs are preserved by default unless -RemoveData is specified.

.PARAMETER ServiceName
    Windows Service name. Default: "Archiver"

.PARAMETER RemoveData
    If set, also removes C:\ProgramData\Archiver entirely (config, logs, binaries).

.EXAMPLE
    .\Uninstall.ps1
    Removes the service, keeps config, logs, and service binaries.

.EXAMPLE
    .\Uninstall.ps1 -RemoveData
    Removes the service AND all data (config + logs + service binaries).
#>

param(
    [string]$ServiceName = "Archiver",
    [switch]$RemoveData
)

$ErrorActionPreference = "Continue"
$configPath = "C:\ProgramData\Archiver"

Write-Host "=== Archiver Service Uninstaller ===" -ForegroundColor Cyan
Write-Host ""

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $existing) {
    Write-Host "Service '$ServiceName' is not installed." -ForegroundColor Yellow
}
else {
    Write-Host "Stopping service..." -ForegroundColor Yellow
    Stop-Service $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2

    Write-Host "Removing service..." -ForegroundColor Yellow
    sc.exe delete $ServiceName
    Write-Host "Service '$ServiceName' removed." -ForegroundColor Green
}

if ($RemoveData) {
    Write-Host "Removing data directory: $configPath" -ForegroundColor Yellow
    if (Test-Path $configPath) {
        Remove-Item -Recurse -Force $configPath -ErrorAction SilentlyContinue
        Write-Host "Data directory removed." -ForegroundColor Green
    }
}
else {
    Write-Host "Data preserved at: $configPath" -ForegroundColor Green
    Write-Host "  Config:     $configPath\config.json" -ForegroundColor Gray
    Write-Host "  Logs:       $configPath\logs\" -ForegroundColor Gray
    Write-Host "  Service:    $configPath\Service\" -ForegroundColor Gray
    Write-Host "Delete this directory manually if no longer needed, or re-run with -RemoveData." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Uninstall Complete ===" -ForegroundColor Cyan
