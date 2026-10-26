using System;
using System.Collections.Generic;

namespace PenguinTwitchBot.Bot.Commands.Games.Racing;

public sealed class RacingMessageSettings
{
    // Defaults matching standard Racing Game announcements & responses
    public const string DefaultSessionStart = "Race lobby is now OPEN for {duration}s! Mode: {mode} | Laps: {laps}. Type !race [emote] to line up on the starting grid!";
    public const string DefaultSessionEnd = "Race has finished! {count} racers crossed the line!";
    public const string DefaultRaceStart = "🚦 The green flag is OUT! {heat} with {count} racers!";
    public const string DefaultHeatComplete = "🏁 Heat {heat}/{total} Complete! Advancing to Final: {qualifiers}!";
    public const string DefaultChampionshipFinalStart = "🏆 The Championship Final is starting with {count} finalists! Green flag is OUT!";
    public const string DefaultRaceWin = "🏆 {user} took 1ST PLACE ({time}s)! +{points} points!";
    public const string DefaultTimeTrialComplete = "⏱️ {user} completed their Time Trial hot lap in {time}s with {wrecks} collisions!";
    public const string DefaultRaceInProgress = "@{user}, a race is currently in progress! Wait for the checkered flag.";
    public const string DefaultNoLobbyOpen = "@{user}, no race lobby is open right now. Type !race start to open registration!";
    public const string DefaultGridFull = "@{user}, the starting grid is full for this race ({max} max racers)!";
    public const string DefaultAlreadyJoined = "@{user}, you are already registered on the starting grid!";
    public const string DefaultUserJoined = "@{user} lined up on the starting grid in position #{position}!";

    // Overlay Defaults
    public const string DefaultOverlayLobbyTitle = "🏁 RACE LOBBY: {mode}";
    public const string DefaultOverlayLobbySubtitle = "Starting in {seconds}s  •  Type !race [emote] to join! ({count}/{max})";
    public const string DefaultOverlayPodiumTitle = "🏆 CHAMPIONSHIP FINAL PODIUM 🏆";
    public const string DefaultOverlayTimeTrialTitle = "⏱️ TIME TRIAL RESULT";
    public const string DefaultOverlayNewRecord = "🏆 NEW TRACK RECORD! 🏆";

    // Broadcast & Announcement Templates
    public string SessionStart { get; set; } = DefaultSessionStart;
    public string SessionEnd { get; set; } = DefaultSessionEnd;
    public string RaceStart { get; set; } = DefaultRaceStart;
    public string HeatComplete { get; set; } = DefaultHeatComplete;
    public string ChampionshipFinalStart { get; set; } = DefaultChampionshipFinalStart;
    public string RaceWin { get; set; } = DefaultRaceWin;
    public string TimeTrialComplete { get; set; } = DefaultTimeTrialComplete;

    // Command Error & Status Responses
    public string RaceInProgress { get; set; } = DefaultRaceInProgress;
    public string NoLobbyOpen { get; set; } = DefaultNoLobbyOpen;
    public string GridFull { get; set; } = DefaultGridFull;
    public string AlreadyJoined { get; set; } = "";
    public string UserJoined { get; set; } = "";

    // Overlay On-Screen Display Templates
    public string OverlayLobbyTitle { get; set; } = DefaultOverlayLobbyTitle;
    public string OverlayLobbySubtitle { get; set; } = DefaultOverlayLobbySubtitle;
    public string OverlayPodiumTitle { get; set; } = DefaultOverlayPodiumTitle;
    public string OverlayTimeTrialTitle { get; set; } = DefaultOverlayTimeTrialTitle;
    public string OverlayNewRecord { get; set; } = DefaultOverlayNewRecord;

    public static string Format(string? template, string fallback, IReadOnlyDictionary<string, string> replacements)
    {
        var result = string.IsNullOrWhiteSpace(template) ? fallback : template;
        foreach (var (placeholder, value) in replacements)
        {
            result = result.Replace("{" + placeholder + "}", value, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }

    public static RacingMessageSettings Normalize(RacingMessageSettings? settings)
    {
        settings ??= new RacingMessageSettings();
        settings.SessionStart = string.IsNullOrWhiteSpace(settings.SessionStart) ? DefaultSessionStart : settings.SessionStart;
        settings.SessionEnd = string.IsNullOrWhiteSpace(settings.SessionEnd) ? DefaultSessionEnd : settings.SessionEnd;
        settings.RaceStart = string.IsNullOrWhiteSpace(settings.RaceStart) ? DefaultRaceStart : settings.RaceStart;
        settings.HeatComplete = string.IsNullOrWhiteSpace(settings.HeatComplete) ? DefaultHeatComplete : settings.HeatComplete;
        settings.ChampionshipFinalStart = string.IsNullOrWhiteSpace(settings.ChampionshipFinalStart) ? DefaultChampionshipFinalStart : settings.ChampionshipFinalStart;
        settings.RaceWin = string.IsNullOrWhiteSpace(settings.RaceWin) ? DefaultRaceWin : settings.RaceWin;
        settings.TimeTrialComplete = string.IsNullOrWhiteSpace(settings.TimeTrialComplete) ? DefaultTimeTrialComplete : settings.TimeTrialComplete;
        settings.RaceInProgress = string.IsNullOrWhiteSpace(settings.RaceInProgress) ? DefaultRaceInProgress : settings.RaceInProgress;
        settings.NoLobbyOpen = string.IsNullOrWhiteSpace(settings.NoLobbyOpen) ? DefaultNoLobbyOpen : settings.NoLobbyOpen;
        settings.GridFull = string.IsNullOrWhiteSpace(settings.GridFull) ? DefaultGridFull : settings.GridFull;
        settings.OverlayLobbyTitle = string.IsNullOrWhiteSpace(settings.OverlayLobbyTitle) ? DefaultOverlayLobbyTitle : settings.OverlayLobbyTitle;
        settings.OverlayLobbySubtitle = string.IsNullOrWhiteSpace(settings.OverlayLobbySubtitle) ? DefaultOverlayLobbySubtitle : settings.OverlayLobbySubtitle;
        settings.OverlayPodiumTitle = string.IsNullOrWhiteSpace(settings.OverlayPodiumTitle) ? DefaultOverlayPodiumTitle : settings.OverlayPodiumTitle;
        settings.OverlayTimeTrialTitle = string.IsNullOrWhiteSpace(settings.OverlayTimeTrialTitle) ? DefaultOverlayTimeTrialTitle : settings.OverlayTimeTrialTitle;
        settings.OverlayNewRecord = string.IsNullOrWhiteSpace(settings.OverlayNewRecord) ? DefaultOverlayNewRecord : settings.OverlayNewRecord;
        return settings;
    }
}

