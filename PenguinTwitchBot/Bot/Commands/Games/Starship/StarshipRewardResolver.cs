namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

internal static class StarshipRewardResolver
{
    internal static void ResolveOutcome(
        StarshipExpeditionSnapshot snapshot,
        StarshipExpeditionEventSnapshot resolvedEvent,
        StarshipRewardProfile? profile)
    {
        var finalChoice = FindResolvedChoice(resolvedEvent);
        var committed = finalChoice?.IsRisky == true;
        var offersCommit = AllChoices(resolvedEvent).Any(choice => choice.IsRisky);
        if (finalChoice?.EndsWithoutReward == true || (!committed && offersCommit))
        {
            resolvedEvent.OutcomeResult = StarshipExpeditionNarrative.OutcomePassed;
            return;
        }

        var chance = GetEffectiveSuccessChance(profile, resolvedEvent, finalChoice);
        var succeeded = StarshipExpeditionStory.StableIndex(snapshot.Seed, resolvedEvent.Sequence,
            $"reward-success:{resolvedEvent.EventId:N}", 100) < chance;
        var tier = profile == null ? null : ChooseEventTier(snapshot, resolvedEvent, profile);
        resolvedEvent.OutcomeResult = succeeded ? StarshipExpeditionNarrative.OutcomeSuccess : StarshipExpeditionNarrative.OutcomeFailure;
        resolvedEvent.OutcomeRarity = tier?.Name ?? string.Empty;
        resolvedEvent.OutcomeRarityColor = tier?.Color ?? string.Empty;
    }

    internal static void ApplyRewardProfile(
        StarshipExpeditionSnapshot snapshot,
        StarshipExpeditionEventSnapshot resolvedEvent,
        StarshipRewardProfile profile,
        IReadOnlySet<string> eligibleUserIds)
    {
        if (string.IsNullOrEmpty(resolvedEvent.OutcomeResult)) ResolveOutcome(snapshot, resolvedEvent, profile);
        if (resolvedEvent.OutcomeResult == StarshipExpeditionNarrative.OutcomePassed) return;
        var finalChoice = FindResolvedChoice(resolvedEvent);
        var committed = finalChoice?.IsRisky == true;
        var chance = GetEffectiveSuccessChance(profile, resolvedEvent, finalChoice);
        var succeeded = resolvedEvent.OutcomeResult == StarshipExpeditionNarrative.OutcomeSuccess;
        var tier = profile.GetTiers().FirstOrDefault(item => item.Name == resolvedEvent.OutcomeRarity);
        var rewardPercent = CombinePercent(resolvedEvent.RewardMultiplierPercent, finalChoice?.RewardMultiplierPercent ?? 100);
        var riskPercent = CombinePercent(resolvedEvent.RiskMultiplierPercent, finalChoice?.RiskMultiplierPercent ?? 100);

        if (committed)
        {
            var voterIds = resolvedEvent.Votes
                .Where(vote => string.Equals(vote.ChoiceId, resolvedEvent.ResolvedChoiceId, StringComparison.OrdinalIgnoreCase))
                .Select(vote => vote.UserId).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var voterId in voterIds)
            {
                var member = snapshot.Crew.FirstOrDefault(candidate => string.Equals(candidate.UserId, voterId, StringComparison.OrdinalIgnoreCase));
                if (member != null)
                    ApplyRewardToParticipant(snapshot, resolvedEvent, profile, member, true, eligibleUserIds.Contains(member.UserId),
                        succeeded, tier, chance, rewardPercent, riskPercent);
            }
            return;
        }

        foreach (var member in snapshot.Crew.Where(member => eligibleUserIds.Contains(member.UserId)))
            ApplyRewardToParticipant(snapshot, resolvedEvent, profile, member, false, true,
                succeeded, tier, chance, rewardPercent, riskPercent);
    }

    internal static int GetEffectiveSuccessChance(
        StarshipRewardProfile? profile,
        StarshipExpeditionEventSnapshot expeditionEvent,
        StarshipEventChoiceSnapshot? choice = null) =>
        Math.Clamp((expeditionEvent.ShipContext?.BaseSuccessChancePercent ?? profile?.SuccessChancePercent ?? 0) +
            (expeditionEvent.ShipContext?.ResourceSuccessModifierPercent ?? 0) + expeditionEvent.SuccessChanceModifierPercent +
            (choice?.SuccessChanceModifierPercent ?? 0), 0, 100);

    internal static int CombinePercent(int first, int second) => Math.Clamp((int)((long)first * second / 100), 0, 1000);

    private static IEnumerable<StarshipEventChoiceSnapshot> AllChoices(StarshipExpeditionEventSnapshot expeditionEvent) =>
        expeditionEvent.StepDefinitions.Count > 0
            ? expeditionEvent.StepDefinitions.SelectMany(step => step.Choices)
            : expeditionEvent.ChoicePool.Concat(expeditionEvent.Choices);

    private static StarshipEventChoiceSnapshot? FindResolvedChoice(StarshipExpeditionEventSnapshot expeditionEvent)
    {
        var choices = string.IsNullOrEmpty(expeditionEvent.ResolvedStepId)
            ? AllChoices(expeditionEvent)
            : expeditionEvent.StepDefinitions.FirstOrDefault(step => step.StepId == expeditionEvent.ResolvedStepId)?.Choices ?? [];
        return choices.FirstOrDefault(choice =>
            string.Equals(choice.ChoiceId, expeditionEvent.ResolvedChoiceId, StringComparison.OrdinalIgnoreCase));
    }

    private static void ApplyRewardToParticipant(
        StarshipExpeditionSnapshot snapshot,
        StarshipExpeditionEventSnapshot resolvedEvent,
        StarshipRewardProfile profile,
        StarshipCrewMemberSnapshot member,
        bool isRisky,
        bool isEligible,
        bool succeeded,
        StarshipRewardTier? tier,
        int successChancePercent,
        int rewardPercent,
        int riskPercent)
    {
        var rarity = tier?.Name ?? string.Empty;
        var primaryTypeId = profile.GetPrimaryPointTypeId();
        if (isRisky && !isEligible)
        {
            if (primaryTypeId > 0)
                resolvedEvent.RewardOutcomes.Add(new StarshipRewardOutcomeSnapshot
                {
                    UserId = member.UserId,
                    DisplayName = member.DisplayName,
                    PointTypeId = primaryTypeId,
                    PointTypeName = profile.GetPointTypeName(primaryTypeId),
                    Outcome = "Ineligible",
                    WasRiskyAction = true,
                    SuccessChancePercent = successChancePercent
                });
            return;
        }

        var typeIds = new List<int>();
        if (succeeded)
        {
            if (tier != null) typeIds.AddRange(tier.Rewards.Select(reward => reward.PointTypeId));
        }
        else
        {
            typeIds.AddRange(profile.GetLossRules().Select(rule => rule.PointTypeId));
        }
        typeIds = typeIds.Where(id => id > 0).Distinct().ToList();
        if (typeIds.Count == 0 && primaryTypeId > 0) typeIds.Add(primaryTypeId);

        var rolledByType = new Dictionary<int, long>();
        if (succeeded && isEligible && tier != null)
        {
            for (var index = 0; index < tier.Rewards.Count; index++)
            {
                var reward = tier.Rewards[index];
                if (reward.PointTypeId <= 0) continue;
                if (reward.ChancePercent < 100 && StarshipExpeditionStory.StableIndex(snapshot.Seed, resolvedEvent.Sequence,
                        $"reward-chance:{resolvedEvent.EventId:N}:{member.UserId}:{rarity}:{index}", 100) >= reward.ChancePercent)
                    continue;
                var amountKey = $"reward-amount:{resolvedEvent.EventId:N}:{member.UserId}:{rarity}";
                if (index > 0) amountKey += $":{index}";
                var rolledAmount = StarshipExpeditionStory.StableLongRange(snapshot.Seed, resolvedEvent.Sequence,
                    amountKey, reward.Minimum, reward.Maximum);
                rolledAmount = ScaleRewardAmount(rolledAmount, rewardPercent);
                rolledByType[reward.PointTypeId] = AddSaturating(rolledByType.GetValueOrDefault(reward.PointTypeId), rolledAmount);
            }
        }

        foreach (var typeId in typeIds)
        {
            long earnedAmount = 0;
            long lostAmount = 0;
            var outcome = succeeded ? "Success" : "Failure";
            var typeName = profile.GetPointTypeName(typeId);
            var currentBalance = member.UnbankedBalances.FirstOrDefault(balance => balance.PointTypeId == typeId);
            var lossRule = profile.GetLossRules().FirstOrDefault(rule => rule.PointTypeId == typeId);
            if (!succeeded && currentBalance != null && lossRule != null)
            {
                var (proportionalLoss, maximumLoss) = ScaleRiskLoss(lossRule, currentBalance.Amount, riskPercent);
                lostAmount = Math.Min(currentBalance.Amount, Math.Min(proportionalLoss, maximumLoss));
                currentBalance.Amount -= lostAmount;
            }
            if (succeeded && isEligible)
            {
                var cap = profile.GetCap(typeId);
                var earnedThisVoyage = snapshot.CompletedEvents
                    .SelectMany(expeditionEvent => expeditionEvent.RewardOutcomes)
                    .Where(previous => string.Equals(previous.UserId, member.UserId, StringComparison.OrdinalIgnoreCase) &&
                                       previous.PointTypeId == typeId)
                    .Aggregate(0L, (total, previous) => AddSaturating(total, previous.EarnedAmount));
                var availableVoyageCap = cap.MaximumUnbankedPerVoyage is { } voyageLimit ? Math.Max(0, voyageLimit - earnedThisVoyage) : long.MaxValue;
                earnedAmount = Math.Min(rolledByType.GetValueOrDefault(typeId), Math.Min(cap.MaximumPerEvent ?? long.MaxValue, availableVoyageCap));
                if (earnedAmount > 0)
                {
                    currentBalance ??= AddUnbankedBalance(member, typeId, typeName);
                    currentBalance.Amount = AddSaturating(currentBalance.Amount, earnedAmount);
                }
            }
            else if (succeeded)
            {
                outcome = "Ineligible";
            }
            resolvedEvent.RewardOutcomes.Add(new StarshipRewardOutcomeSnapshot
            {
                UserId = member.UserId,
                DisplayName = member.DisplayName,
                PointTypeId = typeId,
                PointTypeName = typeName,
                Outcome = outcome,
                Rarity = rarity,
                WasRiskyAction = isRisky,
                SuccessChancePercent = successChancePercent,
                EarnedAmount = earnedAmount,
                LostAmount = lostAmount
            });
        }
    }

    internal static (long Proportional, long Maximum) ScaleRiskLoss(StarshipLossRule rule, long balance, int riskPercent)
    {
        var lossPercent = Math.Min(100m, rule.Percent * riskPercent / 100m);
        var maximum = rule.MaximumPerEvent is { } configuredMaximum
            ? (long)Math.Min(long.MaxValue, (decimal)configuredMaximum * riskPercent / 100m)
            : long.MaxValue;
        return ((long)(balance * lossPercent / 100m), maximum);
    }

    private static long ScaleRewardAmount(long amount, int rewardPercent) =>
        (long)Math.Min(long.MaxValue, (decimal)amount * rewardPercent / 100m);

    private static long AddSaturating(long first, long second) =>
        second > long.MaxValue - first ? long.MaxValue : first + second;

    internal static long SumSaturating(IEnumerable<long> values) =>
        values.Aggregate(0L, AddSaturating);

    internal static StarshipRewardTier? ChooseEventTier(
        StarshipExpeditionSnapshot snapshot,
        StarshipExpeditionEventSnapshot expeditionEvent,
        StarshipRewardProfile profile)
    {
        var tiers = profile.GetTiers().Where(tier => tier.Enabled).ToList();
        if (tiers.Count == 0) return null;
        var weights = GetVoyageAdjustedTierWeights(tiers, expeditionEvent.VoyageProgressPercent);
        var total = weights.Sum();
        if (total <= 0) return tiers[^1];
        var roll = StarshipExpeditionStory.StableIndex(snapshot.Seed, expeditionEvent.Sequence, $"reward-rarity:{expeditionEvent.EventId:N}", total);
        var cumulative = 0;
        for (var index = 0; index < tiers.Count; index++)
        {
            cumulative += weights[index];
            if (roll < cumulative) return tiers[index];
        }
        return tiers[^1];
    }

    internal static IReadOnlyList<int> GetVoyageAdjustedTierWeights(IReadOnlyList<StarshipRewardTier> tiers, int progressPercent)
    {
        var progress = Math.Clamp(progressPercent, 0, 100) / 100m;
        var highestTierIndex = Math.Max(1, tiers.Count - 1);
        return tiers.Select((tier, index) => Math.Max(0, (int)Math.Round(
            tier.Weight * (1m + progress * index / highestTierIndex), MidpointRounding.AwayFromZero))).ToList();
    }

    private static StarshipUnbankedBalanceSnapshot AddUnbankedBalance(StarshipCrewMemberSnapshot member, int pointTypeId, string pointTypeName)
    {
        var balance = new StarshipUnbankedBalanceSnapshot
        {
            PointTypeId = pointTypeId,
            PointTypeName = pointTypeName,
            Amount = 0
        };
        member.UnbankedBalances.Add(balance);
        return balance;
    }
}