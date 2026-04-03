@echo off
set "PATH=%PATH%;C:\Program Files (x86)\Microsoft Visual Studio\Installer"
set "TestName=%~1"
if "%TestName%"=="" set "TestName=NoTestName"
dotnet publish -c Release -p:TestName=%TestName% && bin\Release\net10.0\win-x64\publish\NativeAotQuirks.exe
