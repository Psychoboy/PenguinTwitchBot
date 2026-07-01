@echo off
set /p VERSION=Enter version number (e.g. 1.2.3): 

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=%VERSION% -p:InformationalVersion=%VERSION%