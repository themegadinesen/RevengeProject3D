using System;
using UnityEngine;

public enum RecruitmentCandidateLoyalty
{
    Loyal,
    Implant
}

public enum RecruitmentResolutionOutcome
{
    None,
    LoyalAccepted,
    LoyalRejected,
    ImplantRejected,
    ImplantAccepted,
    RosterFull
}

public enum RecruitmentClaimSource
{
    Interview,
    Document
}

public enum RecruitmentTraitGameplayEffect
{
    None,
    BonusINT,
    BonusSTR,
    BonusAGI,
    BonusSuccessChance
}

[Serializable]
public class RecruitmentFact
{
    public string label = "Fact";

    [TextArea(2, 5)]
    public string text = "";
}

[Serializable]
public class RecruitmentClaim
{
    public RecruitmentClaimSource source = RecruitmentClaimSource.Interview;
    public string label = "Claim";

    [TextArea(2, 5)]
    public string text = "";
}

[Serializable]
public class RecruitmentVerifiedFactView
{
    public string label;
    public string text;
    public bool isRevealed;

    public RecruitmentVerifiedFactView(string label, string text, bool isRevealed)
    {
        this.label = label;
        this.text = text;
        this.isRevealed = isRevealed;
    }
}

public static class RecruitmentVettingUtility
{
    public static RecruitmentResolutionOutcome ResolveOutcome(
        RecruitmentCandidateLoyalty hiddenLoyalty,
        CandidateVettingOutcome judgment)
    {
        if (judgment == CandidateVettingOutcome.None)
            return RecruitmentResolutionOutcome.None;

        bool judgedLoyal = judgment == CandidateVettingOutcome.Loyal;
        bool isActuallyLoyal = hiddenLoyalty == RecruitmentCandidateLoyalty.Loyal;

        if (judgedLoyal && isActuallyLoyal)
            return RecruitmentResolutionOutcome.LoyalAccepted;

        if (!judgedLoyal && isActuallyLoyal)
            return RecruitmentResolutionOutcome.LoyalRejected;

        if (!judgedLoyal)
            return RecruitmentResolutionOutcome.ImplantRejected;

        return RecruitmentResolutionOutcome.ImplantAccepted;
    }
}

public static class RecruitmentTraitGameplayUtility
{
    public static void GetStatBonuses(
        RecruitmentTraitGameplayEffect effect,
        int statBonus,
        out int intelligenceBonus,
        out int strengthBonus,
        out int agilityBonus)
    {
        intelligenceBonus = 0;
        strengthBonus = 0;
        agilityBonus = 0;

        int clampedBonus = Mathf.Max(0, statBonus);

        switch (effect)
        {
            case RecruitmentTraitGameplayEffect.BonusINT:
                intelligenceBonus = clampedBonus;
                break;

            case RecruitmentTraitGameplayEffect.BonusSTR:
                strengthBonus = clampedBonus;
                break;

            case RecruitmentTraitGameplayEffect.BonusAGI:
                agilityBonus = clampedBonus;
                break;
        }
    }

    public static float GetSuccessChanceBonus(
        RecruitmentTraitGameplayEffect effect,
        float successChanceBonus)
    {
        return effect == RecruitmentTraitGameplayEffect.BonusSuccessChance
            ? Mathf.Max(0f, successChanceBonus)
            : 0f;
    }

    public static string GetEffectSummary(
        RecruitmentTraitGameplayEffect effect,
        int statBonus,
        float successChanceBonus)
    {
        return effect switch
        {
            RecruitmentTraitGameplayEffect.BonusINT =>
                $"+{Mathf.Max(0, statBonus)} INT on missions",
            RecruitmentTraitGameplayEffect.BonusSTR =>
                $"+{Mathf.Max(0, statBonus)} STR on missions",
            RecruitmentTraitGameplayEffect.BonusAGI =>
                $"+{Mathf.Max(0, statBonus)} AGI on missions",
            RecruitmentTraitGameplayEffect.BonusSuccessChance =>
                $"+{Mathf.Max(0f, successChanceBonus) * 100f:F0}% mission success chance",
            _ => "No gameplay effect"
        };
    }
}