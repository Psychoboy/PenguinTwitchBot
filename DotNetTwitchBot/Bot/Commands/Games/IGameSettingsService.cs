﻿using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Models.Games;
using DotNetTwitchBot.Bot.Models.Points;
using System.Runtime.CompilerServices;

namespace DotNetTwitchBot.Bot.Commands.Games
{
    public interface IGameSettingsService
    {
        Task<bool> GetBoolSetting(string gameName, string settingName, bool defaultValue);
        Task<double> GetDoubleSetting(string gameName, string settingName, double defaultValue);
        Task<int> GetIntSetting(string gameName, string settingName, int defaultValue);
        Task<List<string>> GetStringListSetting(string gameName, string settingName, List<string> defaultValue);
        Task<string> GetStringSetting(string gameName, string settingName, string defaultValue);
        Task SetBoolSetting(string gameName, string settingName, bool value);
        Task SetDoubleSetting(string gameName, string settingName, double value);
        Task SetIntSetting(string gameName, string settingName, int value);
        Task SetStringSetting(string gameName, string settingName, string value);
        Task<PointType> GetPointTypeForGame(string gameName);
        Task SetPointTypeForGame(string gameName, int pointTypeId);
        Task RegisterDefaultPointForGame(string gameName);
        Task<List<PointGamePair>> GetAllPointTypes();
        Task<long> GetLongSetting(string gameName, string settingName, long defaultValue);
        Task SetLongSetting(string gameName, string settingName, long value);

        Task SaveSetting(string gameName, string settingName, string value);
        Task SaveSetting(string gameName, string settingName, int value);
        Task SaveSetting(string gameName, string settingName, double value);
        Task SaveSetting(string gameName, string settingName, bool value);
        Task SaveSetting(string gameName, string settingName, long value);
    }
}