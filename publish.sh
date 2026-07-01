#!/bin/bash

read -p "Enter version number (e.g. 1.2.3 or v1.2.3): " INPUT_TAG

if [[ "$INPUT_TAG" =~ ^v[0-9] ]]; then
    VERSION="${INPUT_TAG#v}"
else
    VERSION="$INPUT_TAG"
fi

dotnet publish PenguinTwitchBot/PenguinTwitchBot.csproj -c Release -r win-x64 --self-contained true "-p:PublishSingleFile=true" "-p:IncludeNativeLibrariesForSelfExtract=true" "-p:Version=$VERSION" "-p:InformationalVersion=$VERSION"

dotnet publish PenguinTwitchBot/PenguinTwitchBot.csproj -c Release -r linux-x64 --self-contained true "-p:PublishSingleFile=true" "-p:IncludeNativeLibrariesForSelfExtract=true" "-p:Version=$VERSION" "-p:InformationalVersion=$VERSION"