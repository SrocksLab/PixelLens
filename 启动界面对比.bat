@echo off
setlocal
cd /d "%~dp0"

where py >nul 2>nul
if %errorlevel%==0 (
    py -3 ui_compare_app.py
    exit /b %errorlevel%
)

where python >nul 2>nul
if %errorlevel%==0 (
    python ui_compare_app.py
    exit /b %errorlevel%
)

echo 未找到 Python 3。请先安装 Python 3.10 或更高版本，并勾选“Add Python to PATH”。
pause
