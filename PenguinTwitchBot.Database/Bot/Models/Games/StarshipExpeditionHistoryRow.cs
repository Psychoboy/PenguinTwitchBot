using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Bot.Models.Games;

[Table("starship_expedition_history")]
[Index(nameof(ExpeditionId), IsUnique = true)]
[Index(nameof(StartedAtUtc))]
[Index(nameof(EndedAtUtc))]
[Index(nameof(Status))]
public sealed class StarshipExpeditionHistoryRow
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public Guid ExpeditionId { get; set; }

    [MaxLength(100)]
    public string ShipName { get; set; } = string.Empty;

    public int Seed { get; set; }

    [MaxLength(100)]
    public string StartedBy { get; set; } = string.Empty;

    [MaxLength(100)]
    public string StartedByUserId { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; }

    public DateTime? EndedAtUtc { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ReturnReason { get; set; }

    public int TotalLegs { get; set; }

    public int CompletedLegs { get; set; }

    public int CrewCount { get; set; }

    public int EventsSucceeded { get; set; }

    public int EventsFailed { get; set; }

    public long TotalRewardsPaid { get; set; }

    public int? HullRemaining { get; set; }

    public int? HullMaximum { get; set; }

    public int? SuppliesRemaining { get; set; }

    [Required]
    public string SnapshotJson { get; set; } = string.Empty;
}

