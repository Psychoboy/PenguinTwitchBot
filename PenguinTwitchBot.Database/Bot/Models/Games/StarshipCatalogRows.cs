using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Bot.Models.Games;

[Table("starship_events")]
[Index(nameof(Name))]
[Index(nameof(Enabled), nameof(Weight))]
public sealed class StarshipEventRow
{
    [Key, MaxLength(80)] public string Id { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int Weight { get; set; } = 1;
    [Required] public string DocumentJson { get; set; } = string.Empty;
}

[Table("starship_reward_profiles")]
[Index(nameof(Name))]
[Index(nameof(Enabled), nameof(Weight))]
public sealed class StarshipRewardProfileRow
{
    [Key, MaxLength(80)] public string Id { get; set; } = string.Empty;
    [Required, MaxLength(80)] public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int Weight { get; set; } = 1;
    [Required] public string DocumentJson { get; set; } = string.Empty;
}