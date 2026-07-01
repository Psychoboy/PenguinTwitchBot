#!/bin/sh

printf "Enter version number (e.g. 1.2.3 or v1.2.3): "
read -r INPUT_TAG

case "$INPUT_TAG" in
    v[0-9]*)
        VERSION=${INPUT_TAG#v}
        ;;
    *)
        VERSION=$INPUT_TAG
        ;;
esac

dotnet publish PenguinTwitchBot/PenguinTwitchBot.csproj -c Release -r win-x64 --self-contained true "-p:PublishSingleFile=true" "-p:IncludeNativeLibrariesForSelfExtract=true" "-p:Version=$VERSION" "-p:InformationalVersion=$VERSION"

dotnet publish PenguinTwitchBot/PenguinTwitchBot.csproj -c Release -r linux-x64 --self-contained true "-p:PublishSingleFile=true" "-p:IncludeNativeLibrariesForSelfExtract=true" "-p:Version=$VERSION" "-p:InformationalVersion=$VERSION"