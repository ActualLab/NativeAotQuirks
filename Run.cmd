@echo off
set "PATH=%PATH%;C:\Program Files (x86)\Microsoft Visual Studio\Installer"
set "TestName=%~1"
if "%TestName%"=="" (
    echo No test selected. Usage: Run.cmd ^<TestName^>
    echo.
    echo Available tests:
    for %%f in (tests\*.cs) do (
        set "name=%%~nf"
        setlocal enabledelayedexpansion
        if not "!name!"=="_Helpers" if not "!name!"=="NoTestName" echo   !name!
        endlocal
    )
    exit /b 0
)
dotnet publish -c Release -p:TestName=%TestName% && bin\Release\net10.0\win-x64\publish\NativeAotQuirks.exe
