# Script para build e criação do instalador
# NÃO requer permissões de administrador

param(
    [string]$Configuration = "Release",
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"

function Confirm-Action {
    param([string]$Message)
    Write-Host ""
    Write-Host $Message -ForegroundColor Yellow
    $response = Read-Host "Deseja continuar? (S/N)"
    return $response -match "^[Ss]"
}

function Get-DotNetSdk {
    # Verificar locais comuns do dotnet SDK
    $dotnetPaths = @(
        "C:\dotnet\dotnet.exe",  # Instalação customizada
        "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe",
        "$PSScriptRoot\dotnet\dotnet.exe",
        (Get-Command "dotnet" -ErrorAction SilentlyContinue).Source,
        "$env:ProgramFiles\dotnet\dotnet.exe",
        "${env:ProgramFiles(x86)}\dotnet\dotnet.exe"
    )
    
    foreach ($path in $dotnetPaths) {
        if ($path -and (Test-Path $path)) {
            # Verificar se é SDK (não apenas runtime)
            try {
                $sdkOutput = & $path --list-sdks 2>&1
                if ($sdkOutput -and $sdkOutput -notmatch "No .NET SDKs") {
                    return $path
                }
            } catch {
                # Continuar para próximo caminho
            }
        }
    }
    return $null
}

function Get-InnoSetup {
    # Verificar locais comuns do Inno Setup
    $innoPaths = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    
    foreach ($path in $innoPaths) {
        if (Test-Path $path) {
            return $path
        }
    }
    return $null
}

function Install-DotNetSdk {
    Write-Host "`nBaixando .NET SDK 8.0..." -ForegroundColor Cyan
    
    $installerUrl = "https://dot.net/v1/dotnet-install.ps1"
    $installerPath = Join-Path $env:TEMP "dotnet-install.ps1"
    
    try {
        Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing
        Write-Host "Instalando .NET SDK 8.0 (pode demorar alguns minutos)..." -ForegroundColor Cyan
        
        # Instalar na pasta do usuário (não requer admin)
        $installDir = "$env:LOCALAPPDATA\Microsoft\dotnet"
        & $installerPath -Channel 8.0 -InstallDir $installDir
        
        # Adicionar ao PATH da sessão atual
        $env:PATH = "$installDir;$env:PATH"
        
        Write-Host ".NET SDK instalado com sucesso!" -ForegroundColor Green
        return "$installDir\dotnet.exe"
    }
    catch {
        Write-Host "ERRO ao instalar .NET SDK: $_" -ForegroundColor Red
        return $null
    }
    finally {
        if (Test-Path $installerPath) {
            Remove-Item $installerPath -Force
        }
    }
}

function Install-InnoSetup {
    Write-Host "`nBaixando Inno Setup 6..." -ForegroundColor Cyan
    
    $installerUrl = "https://jrsoftware.org/download.php/is.exe"
    $installerPath = Join-Path $env:TEMP "innosetup-installer.exe"
    
    try {
        Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing
        Write-Host "Executando instalador do Inno Setup..." -ForegroundColor Cyan
        Write-Host "(Siga as instruções do instalador)" -ForegroundColor Yellow
        
        # Executar instalador (instala em AppData por padrão, não requer admin)
        Start-Process -FilePath $installerPath -ArgumentList "/CURRENTUSER", "/SILENT" -Wait
        
        # Verificar se foi instalado
        Start-Sleep -Seconds 2
        $innoPath = Get-InnoSetup
        
        if ($innoPath) {
            Write-Host "Inno Setup instalado com sucesso!" -ForegroundColor Green
            return $innoPath
        }
        else {
            Write-Host "Inno Setup foi instalado, mas não foi encontrado automaticamente." -ForegroundColor Yellow
            Write-Host "Execute o script novamente após a instalação." -ForegroundColor Yellow
            return $null
        }
    }
    catch {
        Write-Host "ERRO ao baixar Inno Setup: $_" -ForegroundColor Red
        Write-Host "Você pode baixar manualmente de: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
        return $null
    }
    finally {
        if (Test-Path $installerPath) {
            Remove-Item $installerPath -Force -ErrorAction SilentlyContinue
        }
    }
}

# ============================================
# INÍCIO DO SCRIPT
# ============================================

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "VPS File Manager - Build & Installer" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# ============================================
# ETAPA 1: Verificar/Instalar .NET SDK
# ============================================

Write-Host "Verificando dependências..." -ForegroundColor Yellow

$dotnetPath = Get-DotNetSdk

if (-not $dotnetPath) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Red
    Write-Host ".NET SDK não encontrado!" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "O .NET SDK 8.0 é necessário para compilar o projeto." -ForegroundColor White
    Write-Host "Será baixado e instalado em: $env:LOCALAPPDATA\Microsoft\dotnet" -ForegroundColor Gray
    Write-Host "(Aproximadamente 200MB)" -ForegroundColor Gray
    
    if (Confirm-Action "Deseja baixar e instalar o .NET SDK 8.0?") {
        $dotnetPath = Install-DotNetSdk
        if (-not $dotnetPath) {
            Write-Host "Não foi possível instalar o .NET SDK. Saindo..." -ForegroundColor Red
            exit 1
        }
    }
    else {
        Write-Host "Instalação cancelada pelo usuário." -ForegroundColor Yellow
        Write-Host "Baixe manualmente de: https://dotnet.microsoft.com/download" -ForegroundColor Cyan
        exit 1
    }
}
else {
    Write-Host "[OK] .NET SDK encontrado: $dotnetPath" -ForegroundColor Green
}

# ============================================
# ETAPA 2: Verificar Inno Setup (se necessário)
# ============================================

$innoSetupPath = $null
if (-not $SkipInstaller) {
    $innoSetupPath = Get-InnoSetup
    
    if (-not $innoSetupPath) {
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Yellow
        Write-Host "Inno Setup não encontrado!" -ForegroundColor Yellow
        Write-Host "========================================" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "O Inno Setup é necessário para criar o instalador." -ForegroundColor White
        Write-Host "Será baixado e instalado automaticamente." -ForegroundColor Gray
        Write-Host "(Aproximadamente 3MB)" -ForegroundColor Gray
        
        if (Confirm-Action "Deseja baixar e instalar o Inno Setup 6?") {
            $innoSetupPath = Install-InnoSetup
        }
        else {
            Write-Host "Instalação do Inno Setup cancelada." -ForegroundColor Yellow
            Write-Host "O build continuará, mas o instalador não será criado." -ForegroundColor Yellow
            $SkipInstaller = $true
        }
    }
    else {
        Write-Host "[OK] Inno Setup encontrado: $innoSetupPath" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "Dependências verificadas!" -ForegroundColor Green
Write-Host ""

# ============================================
# ETAPA 3: Build do Projeto
# ============================================

$projectPath = Join-Path $PSScriptRoot "VPSFileManager"
Set-Location $projectPath

Write-Host "1. Limpando build anterior..." -ForegroundColor Yellow
try {
    & $dotnetPath clean --configuration $Configuration 2>&1 | Out-Null
} catch {
    # Ignorar erros no clean - não é crítico
}
Write-Host "   Concluído!" -ForegroundColor Gray

Write-Host "`n2. Compilando projeto..." -ForegroundColor Yellow
& $dotnetPath build --configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERRO na compilação do projeto!" -ForegroundColor Red
    exit 1
}

Write-Host "`n3. Publicando aplicação..." -ForegroundColor Yellow
& $dotnetPath publish --configuration $Configuration --output "bin\Publish" --self-contained false
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERRO ao publicar aplicação!" -ForegroundColor Red
    exit 1
}

# ============================================
# ETAPA 4: Criar Instalador
# ============================================

if (-not $SkipInstaller -and $innoSetupPath) {
    Write-Host "`n4. Criando instalador..." -ForegroundColor Yellow
    & $innoSetupPath "installer.iss"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERRO ao criar instalador!" -ForegroundColor Red
        exit 1
    }
}

# ============================================
# RESULTADO FINAL
# ============================================

Write-Host ""
Write-Host "=====================================" -ForegroundColor Green
Write-Host "BUILD CONCLUÍDO COM SUCESSO!" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host ""
Write-Host "Executável: $projectPath\bin\$Configuration\net8.0-windows\VPSFileManager.exe" -ForegroundColor Cyan

if (-not $SkipInstaller -and $innoSetupPath) {
    Write-Host "Instalador: $projectPath\Installer\VPSFileManager-Setup.exe" -ForegroundColor Cyan
}
else {
    Write-Host "Instalador: (não criado - use -SkipInstaller:`$false para criar)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "O instalador NÃO requer permissões de administrador!" -ForegroundColor Yellow
Write-Host ""
