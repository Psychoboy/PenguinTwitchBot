namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public sealed record StarshipShipSystemsSettings
{
    public const int MaximumResource = 100;

    public bool Enabled { get; set; } = true;
    public int MaximumHull { get; set; } = 80;
    public int StartingHull { get; set; } = 80;
    public int StartingSupplies { get; set; } = 55;
    public int TravelSuppliesCost { get; set; } = 10;
    public int EmptySuppliesSuccessPenaltyPercent { get; set; } = 15;
    public int StationResupplyAmount { get; set; } = 40;
    public int StationRepairAmount { get; set; } = 30;
    public int StationRepairSuppliesCost { get; set; } = 10;
    public int ContinueSuccessBonusPercent { get; set; } = 10;
    public int DefaultEventSuccessChancePercent { get; set; } = 80;
    public bool EnableGraduatedOutcomes { get; set; }
    public int CriticalSuccessSharePercent { get; set; } = 15;
    public int CriticalSuccessRewardMultiplierPercent { get; set; } = 150;
    public bool CriticalSuccessBoostsRarity { get; set; } = true;
    public int PartialSuccessBandPercent { get; set; } = 15;
    public int PartialSuccessRewardPercent { get; set; } = 50;
    public int PartialSuccessPenaltyPercent { get; set; } = 50;
    public int CriticalFailureSharePercent { get; set; } = 20;
    public int CriticalFailurePenaltyMultiplierPercent { get; set; } = 150;

    public void Validate()
    {
        if (MaximumHull is < 1 or > MaximumResource || StartingHull < 1 || StartingHull > MaximumHull || StartingSupplies is < 0 or > MaximumResource)
            throw new ArgumentOutOfRangeException(nameof(MaximumHull), "Maximum hull must be 1-100, starting hull cannot exceed it, and starting supplies must be 0-100.");
        if (TravelSuppliesCost is < 0 or > MaximumResource ||
            StationResupplyAmount is < 0 or > MaximumResource || StationRepairAmount is < 0 or > MaximumResource ||
            StationRepairSuppliesCost is < 0 or > MaximumResource)
            throw new ArgumentOutOfRangeException(nameof(TravelSuppliesCost), "Ship resource costs and restores must be 0-100.");
        if (EmptySuppliesSuccessPenaltyPercent is < 0 or > 100 || ContinueSuccessBonusPercent is < 0 or > 100 || DefaultEventSuccessChancePercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(EmptySuppliesSuccessPenaltyPercent), "Ship success modifiers must be 0-100 percentage points.");
        if (CriticalSuccessSharePercent is < 0 or > 100 || PartialSuccessBandPercent is < 0 or > 100 || CriticalFailureSharePercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(CriticalSuccessSharePercent), "Graduated outcome share and band percents must be 0-100.");
        if (CriticalSuccessRewardMultiplierPercent is < 0 or > 500 || PartialSuccessRewardPercent is < 0 or > 500 ||
            PartialSuccessPenaltyPercent is < 0 or > 500 || CriticalFailurePenaltyMultiplierPercent is < 0 or > 500)
            throw new ArgumentOutOfRangeException(nameof(CriticalSuccessRewardMultiplierPercent), "Graduated outcome multipliers must be 0-500%.");
    }
}

public sealed record StarshipShipUpgrade
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int SuccessChanceBonusPercent { get; init; }
    public int TravelSuppliesDiscount { get; init; }
    public int HullDamageReductionPercent { get; init; }
    public int PointLossReductionPercent { get; init; }
    public int RewardBonusPercent { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException("Ship upgrade must have an id and name.");
        if (SuccessChanceBonusPercent is < 0 or > 50 || TravelSuppliesDiscount is < 0 or > 50 ||
            HullDamageReductionPercent is < 0 or > 100 || PointLossReductionPercent is < 0 or > 100 ||
            RewardBonusPercent is < 0 or > 200)
            throw new ArgumentOutOfRangeException(nameof(SuccessChanceBonusPercent), "Ship upgrade modifiers are out of range.");
    }

    public string Describe()
    {
        var parts = new List<string>();
        if (SuccessChanceBonusPercent > 0) parts.Add($"+{SuccessChanceBonusPercent}% success");
        if (TravelSuppliesDiscount > 0) parts.Add($"-{TravelSuppliesDiscount} travel supplies");
        if (HullDamageReductionPercent > 0) parts.Add($"-{HullDamageReductionPercent}% hull damage");
        if (PointLossReductionPercent > 0) parts.Add($"-{PointLossReductionPercent}% point loss");
        if (RewardBonusPercent > 0) parts.Add($"+{RewardBonusPercent}% rewards");
        return parts.Count > 0 ? $"{Name} ({string.Join(", ", parts)})" : Name;
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
    public List<StarshipShipUpgrade> ActiveUpgrades { get; init; } = [];
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
    public int ResourceSuccessModifierPercent { get; set; }
    public StarshipShipEffect? SuccessEffect { get; init; }
    public StarshipShipEffect? FailureEffect { get; init; }
    public StarshipShipEffect? CriticalSuccessEffect { get; init; }
    public StarshipShipEffect? PartialSuccessEffect { get; init; }
    public StarshipShipEffect? CriticalFailureEffect { get; init; }
    public StarshipShipUpgrade? GrantedUpgrade { get; set; }
    public int HullBefore { get; init; }
    public int HullMaximum { get; init; } = StarshipShipSystemsSettings.MaximumResource;
    public int SuppliesBefore { get; init; }
    public int HullAfter { get; set; }
    public int SuppliesAfter { get; set; }

    public void Validate()
    {
        if (BaseSuccessChancePercent is < 0 or > 100 || ResourceSuccessModifierPercent is < -100 or > 100 ||
            HullMaximum is < 1 or > StarshipShipSystemsSettings.MaximumResource ||
            HullBefore is < 0 || HullBefore > HullMaximum || SuppliesBefore is < 0 or > 100 ||
            HullAfter is < 0 || HullAfter > HullMaximum || SuppliesAfter is < 0 or > 100)
            throw new InvalidDataException("Persisted ship event resources and chance modifiers are invalid.");
        SuccessEffect?.Validate();
        FailureEffect?.Validate();
        CriticalSuccessEffect?.Validate();
        PartialSuccessEffect?.Validate();
        CriticalFailureEffect?.Validate();
        GrantedUpgrade?.Validate();
        if (SuccessEffect?.SuppliesCost > 0 || FailureEffect?.SuppliesCost > 0 ||
            CriticalSuccessEffect?.SuppliesCost > 0 || PartialSuccessEffect?.SuppliesCost > 0 ||
            CriticalFailureEffect?.SuppliesCost > 0)
            throw new InvalidDataException("Ship outcome effects cannot require supplies.");
    }
}

public enum StarshipShipEffectResult
{
    Applied,
    AlreadyApplied,
    InsufficientSupplies
}