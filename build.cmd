@echo off
REM Voice Detector Build Script
REM Tạo bởi: Copilot Assistant
REM Hỗ trợ: build release và build debug

setlocal enabledelayedexpansion
cls

echo.
echo ╔════════════════════════════════════════════════╗
echo ║   🎙 Voice Detector - Build Script             ║
echo ╚════════════════════════════════════════════════╝
echo.

if "%1"=="" (
    echo Cách dùng: build.cmd [option]
    echo.
    echo Options:
    echo   build     - Xây dựng ứng dụng Release
    echo   debug     - Xây dựng & chạy Debug
    echo   run       - Chạy ứng dụng
    echo   clean     - Xóa build files
    echo.
    exit /b 0
)

if /i "%1"=="build" (
    echo [*] Xây dựng Release...
    dotnet build -c Release
    if !errorlevel! equ 0 (
        echo [✓] Build thành công!
        echo [*] File: bin\Release\net10.0-windows\VoiceDetector.exe
    ) else (
        echo [✗] Build thất bại!
        exit /b 1
    )
    goto :eof
)

if /i "%1"=="debug" (
    echo [*] Xây dựng Debug...
    dotnet build -c Debug
    if !errorlevel! equ 0 (
        echo [✓] Build thành công!
        echo [*] Chạy ứng dụng Debug...
        echo [*] Log file: logs\VoiceDetector_Debug.log
        echo [*] Logs sẽ hiển thị trong console này...
        echo.
        timeout /t 2 /nobreak
        start "Voice Detector - Debug Console" cmd /k "title Voice Detector Debug Console && cd /d %cd% && dotnet run --no-build && pause"
    ) else (
        echo [✗] Build thất bại!
        exit /b 1
    )
    goto :eof
)

if /i "%1"=="run" (
    echo [*] Chạy ứng dụng...
    echo [*] Log file: logs\VoiceDetector_Debug.log
    echo.
    dotnet run --no-build
    goto :eof
)

if /i "%1"=="clean" (
    echo [*] Xóa build files...
    rmdir /s /q bin 2>nul
    rmdir /s /q obj 2>nul
    echo [✓] Xóa xong!
    goto :eof
)

echo [✗] Option không hợp lệ: %1
exit /b 1
