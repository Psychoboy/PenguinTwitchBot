namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public sealed record StarshipShipSystemsSettings
{
    public const int MaximumResource = 100;

    public bool Enabled { get; set; } = true;
    public int StartingHull { get; set; } = 80;
    public int StartingSupplies { get; set; } = 55;
    public int TravelSuppliesCost { get; set; } = 12;
    public int EmptySuppliesSuccessPenaltyPercent { get; set; } = 15;
    public int StationResupplyAmount { get; set; } = 40;
    public int StationRepairAmount { get; set; } = 30;
    public int StationRepairSuppliesCost { get; set; } = 15;
    public int ContinueSuccessBonusPercent { get; set; } = 10;
    public int DefaultEventSuccessChancePercent { get; set; } = 80;

    public void Validate()
    {
        if (StartingHull is < 1 or > MaximumResource || StartingSupplies is < 0 or > MaximumResource)
            throw new ArgumentOutOfRangeException(nameof(StartingHull), "Starting hull must be 1-100 and starting supplies 0-100.");
        if (TravelSuppliesCost is < 0 or > MaximumResource ||
            StationResupplyAmount is < 0 or > MaximumResource || StationRepairAmount is < 0 or > MaximumResource ||
            StationRepairSuppliesCost is < 0 or > MaximumResource)
            throw new ArgumentOutOfRangeException(nameof(TravelSuppliesCost), "Ship resource costs and restores must be 0-100.");
        if (EmptySuppliesSuccessPenaltyPercent is < 0 or > 100 || ContinueSuccessBonusPercent is < 0 or > 100 || DefaultEventSuccessChancePercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(EmptySuppliesSuccessPenaltyPercent), "Ship success modifiers must be 0-100 percentage points.");
    }
}

public sealed class StarshipShipSystemsSnapshot
{
    public bool IntegrationEnabled { get; init; }
    public StarshipShipSystemsSettings Rules { get; init; } = new();
    public int Hull { get; set; }
    public int Supplies { get; set; }
    public int PendingSuccessBonusPercent { get; set; }
    public Guid? ActiveBonusEventId { get; set; }
    public int ActiveSuccessBonusPercent { get; set; }
    public List<string> AppliedOperationIds { get; init; } = [];
}

public sealed record StarshipShipEffect
{
    public int HullDelta { get; init; }
    public int SuppliesDelta { get; init; }
    public int SuppliesCost { get; init; }

    public void Validate()
    {
        if (HullDelta is < -100 or > 100 || SuppliesDelta is < -100 or > 100 || SuppliesCost is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(HullDelta), "Ship effects must use -100 to 100 deltas and a 0-100 supply cost.");
    }
}

public sealed class StarshipShipEventContext
{
    public bool HasRewards { get; init; }
    public int BaseSuccessChancePercent { get; init; } = 80;
    public int ResourceSuccessModifierPercent { get; init; }
    public StarshipShipEffect? SuccessEffect { get; init; }
    public StarshipShipEffect? FailureEffect { get; init; }
    public int HullBefore { get; init; }
    public int SuppliesBefore { get; init; }
    public int HullAfter { get; set; }
    public int SuppliesAfter { get; set; }

    public void Validate()
    {
        if (BaseSuccessChancePercent is < 0 or > 100 || ResourceSuccessModifierPercent is < -100 or > 100 ||
            HullBefore is < 0 or > 100 || SuppliesBefore is < 0 or > 100 || HullAfter is < 0 or > 100 || SuppliesAfter is < 0 or > 100)
            throw new InvalidDataException("Persisted ship event resources and chance modifiers are invalid.");
        SuccessEffect?.Validate();
        FailureEffect?.Validate();
        if (SuccessEffect?.SuppliesCost > 0 || FailureEffect?.SuppliesCost > 0)
            throw new InvalidDataException("Ship outcome effects cannot require supplies.");
    }
}

public enum StarshipShipEffectResult
{
    Applied,
    AlreadyApplied,
    InsufficientSupplies
}