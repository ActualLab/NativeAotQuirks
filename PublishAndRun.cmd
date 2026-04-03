@echo off
set "PATH=%PATH%;C:\Program Files (x86)\Microsoft Visual Studio\Installer"
dotnet publish -c Release && bin\Release\net10.0\win-x64\publish\NativeAotQuirks.exe
