@echo off
echo Building Church Display App Release...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=false -p:DebugType=none -p:DebugSymbols=false -o bin\Publish\win-x64
echo.
echo Publication complete. 
echo EXE location: bin\Publish\win-x64\ChurchDisplayApp.exe
echo.
pause
