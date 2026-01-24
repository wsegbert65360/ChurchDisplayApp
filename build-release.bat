@echo off
REM Build script for ChurchDisplayApp Windows Installer

echo Building ChurchDisplayApp Release...
dotnet build -c Release

echo Publishing self-contained executable...
dotnet publish -c Release --self-contained --runtime win-x64 -o bin\Publish\win-x64

echo Build complete! 
echo Executable located at: bin\Publish\win-x64\ChurchDisplayApp.exe
pause
