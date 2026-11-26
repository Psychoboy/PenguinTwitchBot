using Microsoft.AspNetCore.Mvc;
using PenguinTwitchBot.Database.Bot.Models.Points;

namespace PenguinTwitchBot.Controllers;

public partial class TwitchExtensionController
{
    #region Commands Lookup

    [HttpGet("commands")]
    public async Task<IActionResult> GetCommands([FromQuery] string? category = null, [FromQuery] string? search = null)
    {
        var allCommands = new List<ExtensionCommandResponse>();
        await AppendDefaultCommandsAsync(allCommands);
        await AppendPointCommandsAsync(allCommands);
        await AppendActionCommandsAsync(allCommands);
        await AppendExternalCommandsAsync(allCommands);

        var distinct = allCommands.DistinctBy(c => c.Command, StringComparer.OrdinalIgnoreCase).AsEnumerable();

        if (!string.IsNullOrWhiteSpace(category) && !string.Equals(category, "all", StringComparison.OrdinalIgnoreCase))
        {
            distinct = distinct.Where(c => string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            distinct = distinct.Where(c =>
                c.Command.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                c.Description.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                c.Category.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                (c.Rank != null && c.Rank.Contains(s, StringComparison.OrdinalIgnoreCase)));
        }

        return Ok(distinct.OrderBy(c => c.Command).ToList());
    }

    private async Task AppendDefaultCommandsAsync(List<ExtensionCommandResponse> allCommands)
    {
        var defaultCommands = await _commandHandler.GetDefaultCommandsFromDb();
        foreach (var cmd in defaultCommands.Where(x => !x.Disabled && !x.ExcludeFromUi && x.MinimumRank < Rank.Moderator))
        {
            allCommands.Add(new ExtensionCommandResponse(
                Command: "!" + cmd.CustomCommandName,
                Category: string.IsNullOrWhiteSpace(cmd.Category) ? (string.IsNullOrWhiteSpace(cmd.ModuleName) ? "General" : cmd.ModuleName) : cmd.Category,
                Description: cmd.Description ?? string.Empty,
                Cost: cmd.Cost,
                UserCooldown: cmd.UserCooldown,
                GlobalCooldown: cmd.GlobalCooldown,
                Rank: cmd.MinimumRank.ToString()));
        }
    }

    private async Task AppendPointCommandsAsync(List<ExtensionCommandResponse> allCommands)
    {
        var pointTypes = await _pointsSystem.GetPointTypes();
        foreach (var pt in pointTypes)
        {
            foreach (var cmd in pt.PointCommands.Where(x => !x.Disabled && !x.ExcludeFromUi && x.MinimumRank < Rank.Moderator))
            {
                var desc = cmd.CommandType == PointCommandType.Get
                    ? $"Checks your {pt.Name} balance."
                    : string.Empty;

                allCommands.Add(new ExtensionCommandResponse(
                    Command: "!" + cmd.CommandName,
                    Category: pt.Name,
                    Description: desc,
                    Cost: cmd.Cost,
                    UserCooldown: cmd.UserCooldown,
                    GlobalCooldown: cmd.GlobalCooldown,
                    Rank: cmd.MinimumRank.ToString()));
            }
        }
    }

    private async Task AppendActionCommandsAsync(List<ExtensionCommandResponse> allCommands)
    {
        var actionCommands = await _actionCommandService.GetAllAsync();
        foreach (var cmd in actionCommands.Where(x => !x.Disabled && !x.ExcludeFromUi && x.MinimumRank < Rank.Moderator))
        {
            allCommands.Add(new ExtensionCommandResponse(
                Command: "!" + cmd.CommandName,
                Category: string.IsNullOrWhiteSpace(cmd.Category) ? "Actions" : cmd.Category,
                Description: cmd.Description ?? string.Empty,
                Cost: cmd.Cost,
                UserCooldown: cmd.UserCooldown,
                GlobalCooldown: cmd.GlobalCooldown,
                Rank: cmd.MinimumRank.ToString()));
        }
    }

    private async Task AppendExternalCommandsAsync(List<ExtensionCommandResponse> allCommands)
    {
        var externalCommands = await _commandHandler.GetExternalCommands();
        foreach (var cmd in externalCommands.Where(x => !x.Disabled && !x.ExcludeFromUi && x.MinimumRank < Rank.Moderator))
        {
            allCommands.Add(new ExtensionCommandResponse(
                Command: "!" + cmd.CommandName,
                Category: string.IsNullOrWhiteSpace(cmd.Category) ? "Custom" : cmd.Category,
                Description: cmd.Description ?? string.Empty,
                Cost: cmd.Cost,
                UserCooldown: cmd.UserCooldown,
                GlobalCooldown: cmd.GlobalCooldown,
                Rank: cmd.MinimumRank.ToString()));
        }
    }

    [HttpGet("commands/categories")]
    public async Task<IActionResult> GetCommandCategories()
    {
        var commandsResult = await GetCommands();
        if (commandsResult is OkObjectResult ok && ok.Value is List<ExtensionCommandResponse> list)
        {
            var categories = list
                .Select(c => c.Category)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList();

            return Ok(categories);
        }

        return Ok(new List<string>());
    }

    #endregion
}
