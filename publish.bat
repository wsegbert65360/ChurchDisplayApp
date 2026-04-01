@echo off
echo Building Church Display App Release...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true
echo.
echo Publication complete. 
echo EXE location: bin\Release\net10.0-windows\win-x64\publish\ChurchDisplayApp.exe
echo.
pause
