<#
.SYNOPSIS
    Starts the MyTradingToolbox backend (.NET Web API) and frontend (React + Vite) services.

.DESCRIPTION
    This script verifies prerequisites, ensures frontend dependencies are installed,
    and launches both the backend API and frontend dev server in separate terminal windows.

.PARAMETER BackendOnly
    Starts only the .NET Web API service.

.PARAMETER FrontendOnly
    Starts only the React + Vite frontend service.

.PARAMETER ApiPort
    The port for the ASP.NET Core API server (default: 5000).

.EXAMPLE
    .\start-services.ps1
    Starts both backend (http://localhost:5000) and frontend (http://localhost:3000).

.EXAMPLE
    .\start-services.ps1 -BackendOnly
    Starts only the backend API server.
#>

[CmdletBinding()]
param(
    [switch]$BackendOnly,
    [switch]$FrontendOnly,
    [int]$ApiPort = 5000
)

$ErrorActionPreference = "Stop"

$RootDir = $PSScriptRoot
$ApiProject = Join-Path $RootDir "src\MyTradingToolbox.Api\MyTradingToolbox.Api.csproj"
$FrontendDir = Join-Path $RootDir "frontend"

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  MyTradingToolbox - Service Starter" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""

# Check Prerequisites
function Test-CommandAvailable {
    param([string]$CommandName)
    return (Get-Command $CommandName -ErrorAction SilentlyContinue) -ne $null
}

if (-not (Test-CommandAvailable "dotnet")) {
    Write-Error "Error: .NET SDK ('dotnet') is not found in PATH. Please install .NET SDK."
    exit 1
}

if (-not (Test-CommandAvailable "npm")) {
    Write-Error "Error: Node.js / npm is not found in PATH. Please install Node.js."
    exit 1
}

$startBackend = -not $FrontendOnly
$startFrontend = -not $BackendOnly

# 1. Start Backend API
if ($startBackend) {
    if (-not (Test-Path $ApiProject)) {
        Write-Error "Backend project file not found at: $ApiProject"
        exit 1
    }

    Write-Host "[Backend] Launching .NET Web API on port $ApiPort..." -ForegroundColor Green
    $backendCmd = "Write-Host 'Starting MyTradingToolbox.Api on http://localhost:$ApiPort...' -ForegroundColor Cyan; dotnet run --project `"$ApiProject`" --urls `"http://localhost:$ApiPort`""
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $backendCmd -WorkingDirectory $RootDir
    Write-Host "  -> Backend process spawned in separate window." -ForegroundColor Gray
}

# 2. Start Frontend Dev Server
if ($startFrontend) {
    if (-not (Test-Path $FrontendDir)) {
        Write-Error "Frontend directory not found at: $FrontendDir"
        exit 1
    }

    $nodeModulesDir = Join-Path $FrontendDir "node_modules"
    if (-not (Test-Path $nodeModulesDir)) {
        Write-Host "[Frontend] node_modules not found. Running npm install..." -ForegroundColor Yellow
        Push-Location $FrontendDir
        try {
            npm install
        }
        finally {
            Pop-Location
        }
    }

    Write-Host "[Frontend] Launching Vite Dev Server on port 3000..." -ForegroundColor Green
    $frontendCmd = "Write-Host 'Starting Frontend Dev Server (http://localhost:3000)...' -ForegroundColor Cyan; npm run dev"
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $frontendCmd -WorkingDirectory $FrontendDir
    Write-Host "  -> Frontend process spawned in separate window." -ForegroundColor Gray
}

Write-Host ""
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  Services Initiated Successfully!" -ForegroundColor Green
Write-Host "======================================================" -ForegroundColor Cyan
if ($startBackend) {
    Write-Host "  Backend API:     http://localhost:$ApiPort" -ForegroundColor White
    Write-Host "  Swagger UI:      http://localhost:$ApiPort/swagger" -ForegroundColor White
    Write-Host "  Health Check:    http://localhost:$ApiPort/health" -ForegroundColor White
}
if ($startFrontend) {
    Write-Host "  Web Dashboard:   http://localhost:3000" -ForegroundColor White
}
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""
