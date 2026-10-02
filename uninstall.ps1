# Removes FingerprintLock (service, tray app, settings). Run from an elevated PowerShell.
$name = 'FingerprintLock'
Stop-Service $name -ErrorAction SilentlyContinue
& sc.exe delete $name | Out-Null
Get-Process FingerprintLockApp -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run' -Name $name -ErrorAction SilentlyContinue
Remove-Item (Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\Fingerprint Lock.lnk') -ErrorAction SilentlyContinue
Start-Sleep 2
Remove-Item (Join-Path $env:ProgramFiles $name) -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $env:ProgramData $name) -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Fingerprint Lock desinstalado."
