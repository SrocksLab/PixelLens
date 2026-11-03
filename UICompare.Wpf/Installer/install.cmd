@echo off
setlocal
set "INSTALL_DIR=%LOCALAPPDATA%\Programs\UICompare"
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"
copy /Y "%~dp0UICompare.exe" "%INSTALL_DIR%\UICompare.exe" >nul
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$desktop=[Environment]::GetFolderPath('Desktop');$w=New-Object -ComObject WScript.Shell;$s=$w.CreateShortcut((Join-Path $desktop '效果图比对.lnk'));$s.TargetPath=(Join-Path $env:LOCALAPPDATA 'Programs\UICompare\UICompare.exe');$s.WorkingDirectory=(Join-Path $env:LOCALAPPDATA 'Programs\UICompare');$s.IconLocation=$s.TargetPath;$s.Save()"
start "" "%INSTALL_DIR%\UICompare.exe"
exit /b 0
