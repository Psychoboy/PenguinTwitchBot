[![.NET Linux](https://github.com/Psychoboy/PenguinTwitchBot/actions/workflows/dotnet.yml/badge.svg)](https://github.com/Psychoboy/PenguinTwitchBot/actions/workflows/dotnet.yml) 
[![.NET Windows](https://github.com/Psychoboy/PenguinTwitchBot/actions/workflows/dotnet-win.yml/badge.svg)](https://github.com/Psychoboy/PenguinTwitchBot/actions/workflows/dotnet-win.yml)
[![CodeQL](https://github.com/Psychoboy/PenguinTwitchBot/actions/workflows/github-code-scanning/codeql/badge.svg)](https://github.com/Psychoboy/PenguinTwitchBot/actions/workflows/github-code-scanning/codeql) 
[![CodeFactor](https://www.codefactor.io/repository/github/psychoboy/penguintwitchbot/badge)](https://www.codefactor.io/repository/github/psychoboy/penguintwitchbot)
[![Codacy Badge](https://app.codacy.com/project/badge/Grade/3e4574fdb5b9423fb850c40b5d4a14aa)](https://app.codacy.com/gh/Psychoboy/PenguinTwitchBot/dashboard?utm_source=gh&utm_medium=referral&utm_content=&utm_campaign=Badge_grade)

This is a twitch bought written in .NET Core 7 using TwitchLib and a MariaDB backend. Currently it is written specifically my use but am currently making it more generic for others to use. 

Here are just some of things I need to re-work to make it so others can use:

* Customize response strings (Allows streamers to set custom responses and support different languages)
* Enable/Disable entire modules
* Individual module settings for their own uniqueness (This will be slowly done over time)
* Registering credentials easier
* Allow custom Discord commands
* Make Discord commands configurable
* Documentation
* And more items I probably couldn't think of at the time of this writing

The Twitch Connectivity is thanks to https://github.com/TwitchLib/TwitchLib
