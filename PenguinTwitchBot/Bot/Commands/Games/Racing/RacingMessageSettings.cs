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
    public const string DefaultChampionshipFinalComplete = "🏆 Championship Final Complete! Results: {results}";
    public const string DefaultDerbyFinalComplete = "🏆 Demolition Championship Final Complete! Results: {results}";
    public const string DefaultRaceComplete = "🏁 Race Complete! Results: {results}";
    public const string DefaultRaceWin = "🏆 {user} took 1ST PLACE ({time}s)! +{points} points!";
    public const string DefaultDerbyChampion = "🏆 {user} is the LAST KART STANDING in the Demolition Derby with {kills} knockouts! +{points} points!";
    public const string DefaultDerbyKnockout = "💥 {attacker} totaled {victim}! ({attackerKills} KOs)";
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
    public const string DefaultOverlayDerbyPodiumTitle = "🏆 DEMOLITION DERBY CHAMPION 🏆";
    public const string DefaultOverlayTimeTrialTitle = "⏱️ TIME TRIAL RESULT";
    public const string DefaultOverlayNewRecord = "🏆 NEW TRACK RECORD! 🏆";

    // Broadcast & Announcement Templates
    public string SessionStart { get; set; } = DefaultSessionStart;
    public string SessionEnd { get; set; } = DefaultSessionEnd;
    public string RaceStart { get; set; } = DefaultRaceStart;
    public string HeatComplete { get; set; } = DefaultHeatComplete;
    public string ChampionshipFinalStart { get; set; } = DefaultChampionshipFinalStart;
    public string ChampionshipFinalComplete { get; set; } = DefaultChampionshipFinalComplete;
    public string DerbyFinalComplete { get; set; } = DefaultDerbyFinalComplete;
    public string RaceComplete { get; set; } = DefaultRaceComplete;
    public string RaceWin { get; set; } = DefaultRaceWin;
    public string DerbyChampion { get; set; } = DefaultDerbyChampion;
    public string DerbyKnockout { get; set; } = DefaultDerbyKnockout;
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
    public string OverlayDerbyPodiumTitle { get; set; } = DefaultOverlayDerbyPodiumTitle;
    public string OverlayTimeTrialTitle { get; set; } = DefaultOverlayTimeTrialTitle;
    public string OverlayNewRecord { get; set; } = DefaultOverlayNewRecord;

    public static string Format(string? template, string fallback, IReadOnlyDictionary<string, string> replacements)
    {
        var result = template ?? fallback;
        if (string.IsNullOrWhiteSpace(result))
        {
            return string.Empty;
        }

        foreach (var (placeholder, value) in replacements)
        {
            result = result.Replace("{" + placeholder + "}", value, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }

    public static RacingMessageSettings Normalize(RacingMessageSettings? settings)
    {
        settings ??= new RacingMessageSettings();
        settings.SessionStart = settings.SessionStart == null ? DefaultSessionStart : settings.SessionStart.Trim();
        settings.SessionEnd = settings.SessionEnd == null ? DefaultSessionEnd : settings.SessionEnd.Trim();
        settings.RaceStart = settings.RaceStart == null ? DefaultRaceStart : settings.RaceStart.Trim();
        settings.HeatComplete = settings.HeatComplete == null ? DefaultHeatComplete : settings.HeatComplete.Trim();
        settings.ChampionshipFinalStart = settings.ChampionshipFinalStart == null ? DefaultChampionshipFinalStart : settings.ChampionshipFinalStart.Trim();
        settings.ChampionshipFinalComplete = settings.ChampionshipFinalComplete == null ? DefaultChampionshipFinalComplete : settings.ChampionshipFinalComplete.Trim();
        settings.DerbyFinalComplete = settings.DerbyFinalComplete == null ? DefaultDerbyFinalComplete : settings.DerbyFinalComplete.Trim();
        settings.RaceComplete = settings.RaceComplete == null ? DefaultRaceComplete : settings.RaceComplete.Trim();
        settings.RaceWin = settings.RaceWin == null ? DefaultRaceWin : settings.RaceWin.Trim();
        settings.DerbyChampion = settings.DerbyChampion == null ? DefaultDerbyChampion : settings.DerbyChampion.Trim();
        settings.DerbyKnockout = settings.DerbyKnockout == null ? DefaultDerbyKnockout : settings.DerbyKnockout.Trim();
        settings.TimeTrialComplete = settings.TimeTrialComplete == null ? DefaultTimeTrialComplete : settings.TimeTrialComplete.Trim();
        settings.RaceInProgress = settings.RaceInProgress == null ? DefaultRaceInProgress : settings.RaceInProgress.Trim();
        settings.NoLobbyOpen = settings.NoLobbyOpen == null ? DefaultNoLobbyOpen : settings.NoLobbyOpen.Trim();
        settings.GridFull = settings.GridFull == null ? DefaultGridFull : settings.GridFull.Trim();
        settings.AlreadyJoined = settings.AlreadyJoined == null ? "" : settings.AlreadyJoined.Trim();
        settings.UserJoined = settings.UserJoined == null ? "" : settings.UserJoined.Trim();
        settings.OverlayLobbyTitle = settings.OverlayLobbyTitle == null ? DefaultOverlayLobbyTitle : settings.OverlayLobbyTitle.Trim();
        settings.OverlayLobbySubtitle = settings.OverlayLobbySubtitle == null ? DefaultOverlayLobbySubtitle : settings.OverlayLobbySubtitle.Trim();
        settings.OverlayPodiumTitle = settings.OverlayPodiumTitle == null ? DefaultOverlayPodiumTitle : settings.OverlayPodiumTitle.Trim();
        settings.OverlayDerbyPodiumTitle = settings.OverlayDerbyPodiumTitle == null ? DefaultOverlayDerbyPodiumTitle : settings.OverlayDerbyPodiumTitle.Trim();
        settings.OverlayTimeTrialTitle = settings.OverlayTimeTrialTitle == null ? DefaultOverlayTimeTrialTitle : settings.OverlayTimeTrialTitle.Trim();
        settings.OverlayNewRecord = settings.OverlayNewRecord == null ? DefaultOverlayNewRecord : settings.OverlayNewRecord.Trim();
        return settings;
    }
}

