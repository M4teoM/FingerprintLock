# Compiles the service and the app with the C# compiler that ships with Windows (.NET Framework 4).
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$src = $PSScriptRoot
$common = @('/nologo', '/platform:x64', '/optimize+', '/codepage:65001')

& $csc @common /target:exe /r:System.ServiceProcess.dll `
    "/out:$src\FingerprintLock.exe" "$src\Service.cs" "$src\Common.cs"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $csc @common /target:winexe /r:System.ServiceProcess.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll `
    "/out:$src\FingerprintLockApp.exe" "$src\App.cs" "$src\Ui.cs" "$src\Common.cs"
exit $LASTEXITCODE
