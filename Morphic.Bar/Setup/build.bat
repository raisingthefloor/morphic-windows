@echo off

setlocal
cd %~dp0

:: This requires Inno Setup: https://jrsoftware.org/isdl.php
set iscc="C:\Program Files (x86)\Inno Setup 6\iscc.exe"

dotnet publish .. --output=build --runtime=win-x64 --configuration=Release --self-contained

%iscc% morphic-bar.iss

endlocal