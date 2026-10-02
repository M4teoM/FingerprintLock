# Installs FingerprintLock (service + tray app). Run from an elevated PowerShell.
$ErrorActionPreference = 'Stop'
$name   = 'FingerprintLock'
$dest   = Join-Path $env:ProgramFiles $name
$data   = Join-Path $env:ProgramData $name
$svcExe = Join-Path $dest 'FingerprintLock.exe'
$appExe = Join-Path $dest 'FingerprintLockApp.exe'
$runKey = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run'
$lnk    = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\Fingerprint Lock.lnk'

$id = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not ([Security.Principal.WindowsPrincipal]$id).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Ejecuta este script desde PowerShell como administrador."; exit 1
}

# Files from a downloaded zip carry the "from the internet" mark; clear it so Windows runs them.
Get-ChildItem $PSScriptRoot -File | Unblock-File

# A fresh clone has no binaries: build them first.
if (-not (Test-Path (Join-Path $PSScriptRoot 'FingerprintLock.exe')) -or -not (Test-Path (Join-Path $PSScriptRoot 'FingerprintLockApp.exe'))) {
    Write-Host "Compilando..."
    & (Join-Path $PSScriptRoot 'build.ps1')
    if ($LASTEXITCODE -ne 0) { throw "La compilación falló" }
}

# Remove a previous install so the files can be replaced.
if (Get-Service $name -ErrorAction SilentlyContinue) {
    Write-Host "Ya estaba instalado; reinstalando..."
    Stop-Service $name -ErrorAction SilentlyContinue
    & sc.exe delete $name | Out-Null
    Start-Sleep 2
}
Get-Process FingerprintLockApp -ErrorAction SilentlyContinue | Stop-Process -Force

New-Item -ItemType Directory -Force $dest, $data | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'FingerprintLock.exe'), (Join-Path $PSScriptRoot 'FingerprintLockApp.exe') $dest -Force

# Settings file: default is "double tap locks", editable by any user through the app.
$cfg = Join-Path $data 'config.ini'
if (-not (Test-Path $cfg)) { "enabled=1`r`nsingle=none`r`ndouble=lock`r`nwindow=1200" | Set-Content $cfg -Encoding ASCII }
& icacls.exe $cfg /grant '*S-1-5-32-545:(M)' | Out-Null   # BUILTIN\Users: modify

& sc.exe create $name binPath= "`"$svcExe`"" start= auto obj= LocalSystem DisplayName= "Fingerprint Lock" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "sc create falló ($LASTEXITCODE)" }
& sc.exe description $name "Ejecuta acciones (bloquear, suspender...) al tocar el lector de huellas con el PC desbloqueado." | Out-Null
& sc.exe failure $name reset= 86400 actions= restart/5000/restart/5000/restart/30000 | Out-Null
Start-Service $name

# Tray app: starts with every user's sign-in, plus a Start menu shortcut to open the settings.
Set-ItemProperty $runKey -Name $name -Value "`"$appExe`" --tray"
$shell = New-Object -ComObject WScript.Shell
$sc = $shell.CreateShortcut($lnk)
$sc.TargetPath = $appExe
$sc.Description = 'Ajustes de Fingerprint Lock'
$sc.Save()

Write-Host ""
Write-Host "Instalado y en marcha."
Write-Host "Abre 'Fingerprint Lock' desde el menú Inicio para elegir las acciones."
Write-Host "Log: $data\service.log"
