﻿using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public interface ITicketsFeature
    {
        Task<int> GetPointsForActiveUsers();
        Task<int> GetPointsForEveryone();
        Task<int> GetPointsForSubs();
        Task GiveTicketsToActiveAndSubsOnlineWithBonus(long amount, long bonusAmount);
        Task GiveTicketsToActiveUsers(long amount);
        Task<long> GiveTicketsToViewerByUserId(string userid, long amount);
        Task<long> GiveTicketsToViewerByUsername(string username, long amount);
        Task GiveTicketsWithBonusToViewers(IEnumerable<string> viewers, long amount, long subBonusAmount);
        Task OnCommand(object? sender, CommandEventArgs e);
        Task Register();
        Task SetPointsForActiveUsers(int points);
        Task SetPointsForEveryone(int points);
        Task SetPointsForSubs(int points);
    }
}