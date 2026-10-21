namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

internal static class StarshipRewardResolver
{
    internal static void ResolveOutcome(
        StarshipExpeditionSnapshot snapshot,
        StarshipExpeditionEventSnapshot resolvedEvent,
        StarshipRewardProfile? profile)
    {
        var finalChoice = resolvedEvent.Choices.FirstOrDefault(choice =>
            string.Equals(choice.ChoiceId, resolvedEvent.ResolvedChoiceId, StringComparison.OrdinalIgnoreCase));
        var committed = finalChoice?.IsRisky == true;
        var offersCommit = resolvedEvent.ChoicePool.Concat(resolvedEvent.Choices).Any(choice => choice.IsRisky);
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
        var finalChoice = resolvedEvent.Choices.FirstOrDefault(choice =>
            string.Equals(choice.ChoiceId, resolvedEvent.ResolvedChoiceId, StringComparison.OrdinalIgnoreCase));
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
                var range = checked((int)Math.Min(int.MaxValue, reward.Maximum - reward.Minimum + 1));
                var amountKey = $"reward-amount:{resolvedEvent.EventId:N}:{member.UserId}:{rarity}";
                if (index > 0) amountKey += $":{index}";
                var rolledAmount = reward.Minimum + StarshipExpeditionStory.StableIndex(snapshot.Seed, resolvedEvent.Sequence, amountKey, Math.Max(1, range));
                rolledAmount = (long)((decimal)rolledAmount * rewardPercent / 100m);
                rolledByType[reward.PointTypeId] = rolledByType.GetValueOrDefault(reward.PointTypeId) + rolledAmount;
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
                    .Sum(previous => previous.EarnedAmount);
                var availableVoyageCap = cap.MaximumUnbankedPerVoyage is { } voyageLimit ? Math.Max(0, voyageLimit - earnedThisVoyage) : long.MaxValue;
                earnedAmount = Math.Min(rolledByType.GetValueOrDefault(typeId), Math.Min(cap.MaximumPerEvent ?? long.MaxValue, availableVoyageCap));
                if (earnedAmount > 0)
                {
                    currentBalance ??= AddUnbankedBalance(member, typeId, typeName);
                    currentBalance.Amount += earnedAmount;
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
        return ((long)(balance * lossPercent / 100m), rule.MaximumPerEvent is { } maximum ? (long)((decimal)maximum * riskPercent / 100m) : long.MaxValue);
    }

    internal static StarshipRewardTier? ChooseEventTier(
        StarshipExpeditionSnapshot snapshot,
        StarshipExpeditionEventSnapshot expeditionEvent,
        StarshipRewardProfile profile)
    {
        var tiers = profile.GetTiers().Where(tier => tier.Enabled).ToList();
        if (tiers.Count == 0) return null;
        var total = profile.UsesLegacyFields ? 100 : tiers.Sum(tier => Math.Max(0, tier.Weight));
        if (total <= 0) return tiers[^1];
        var roll = StarshipExpeditionStory.StableIndex(snapshot.Seed, expeditionEvent.Sequence, $"reward-rarity:{expeditionEvent.EventId:N}", total);
        var cumulative = 0;
        foreach (var tier in tiers)
        {
            cumulative += Math.Max(0, tier.Weight);
            if (roll < cumulative) return tier;
        }
        return tiers[^1];
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