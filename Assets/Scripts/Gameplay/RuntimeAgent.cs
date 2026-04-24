using UnityEngine;

public enum AgentStatus
{
    Available,
    Busy,
    Lost
}

[System.Serializable]
public class RuntimeAgent
{
    public RecruitmentCandidateDefinition Definition;
    public string Name;
    public int INT;
    public int STR;
    public int AGI;
    public AgentStatus Status;
    public string FlavorTrait { get; private set; }
    public RecruitmentTraitGameplayEffect TraitGameplayEffect { get; private set; }
    public int TraitStatBonus { get; private set; }
    public float TraitSuccessChanceBonus { get; private set; }
    public int CompletedMissionCount { get; private set; }
    public int RookieMissionsRequired { get; private set; }

    public bool IsRookie => RookieMissionsRequired > 0 && CompletedMissionCount < RookieMissionsRequired;

    public string TraitGameplaySummary =>
        RecruitmentTraitGameplayUtility.GetEffectSummary(
            TraitGameplayEffect,
            TraitStatBonus,
            TraitSuccessChanceBonus);

    public RuntimeAgent(RecruitmentCandidateDefinition definition)
        : this(
            definition,
            definition != null ? definition.candidateName : "Unnamed Agent",
            definition != null ? definition.startingINT : 1,
            definition != null ? definition.startingSTR : 1,
            definition != null ? definition.startingAGI : 1,
            false,
            0)
    {
    }

    public RuntimeAgent(
        RecruitmentCandidateDefinition definition,
        string agentName,
        int intelligence,
        int strength,
        int agility,
        bool startsAsRookie,
        int rookieMissionsRequired)
    {
        Definition = definition;
        Name = string.IsNullOrWhiteSpace(agentName) ? "Unnamed Agent" : agentName;
        INT = Mathf.Max(1, intelligence);
        STR = Mathf.Max(1, strength);
        AGI = Mathf.Max(1, agility);
        Status = AgentStatus.Available;
        FlavorTrait = definition != null ? definition.flavorTrait : string.Empty;
        TraitGameplayEffect = definition != null
            ? definition.traitGameplayEffect
            : RecruitmentTraitGameplayEffect.None;
        TraitStatBonus = definition != null ? Mathf.Max(0, definition.traitStatBonus) : 0;
        TraitSuccessChanceBonus = definition != null
            ? Mathf.Max(0f, definition.traitSuccessChanceBonus)
            : 0f;
        RookieMissionsRequired = startsAsRookie ? Mathf.Max(1, rookieMissionsRequired) : 0;
        CompletedMissionCount = 0;
    }

    public int GetMissionINT(int rookieStatPenalty)
    {
        RecruitmentTraitGameplayUtility.GetStatBonuses(
            TraitGameplayEffect,
            TraitStatBonus,
            out int intelligenceBonus,
            out _,
            out _);

        return GetEffectiveStat(INT, intelligenceBonus, rookieStatPenalty);
    }

    public int GetMissionSTR(int rookieStatPenalty)
    {
        RecruitmentTraitGameplayUtility.GetStatBonuses(
            TraitGameplayEffect,
            TraitStatBonus,
            out _,
            out int strengthBonus,
            out _);

        return GetEffectiveStat(STR, strengthBonus, rookieStatPenalty);
    }

    public int GetMissionAGI(int rookieStatPenalty)
    {
        RecruitmentTraitGameplayUtility.GetStatBonuses(
            TraitGameplayEffect,
            TraitStatBonus,
            out _,
            out _,
            out int agilityBonus);

        return GetEffectiveStat(AGI, agilityBonus, rookieStatPenalty);
    }

    public float GetMissionSuccessChanceBonus()
    {
        return RecruitmentTraitGameplayUtility.GetSuccessChanceBonus(
            TraitGameplayEffect,
            TraitSuccessChanceBonus);
    }

    public void RecordMissionCompleted()
    {
        CompletedMissionCount++;
    }

    public string GetMissionModifierSummary(int rookieStatPenalty)
    {
        string rookieSummary = string.Empty;
        if (IsRookie && rookieStatPenalty > 0)
            rookieSummary = $"Rookie -{rookieStatPenalty} all";

        bool hasTraitName = !string.IsNullOrWhiteSpace(FlavorTrait);
        bool hasTraitEffect = TraitGameplayEffect != RecruitmentTraitGameplayEffect.None;

        string traitSummary = string.Empty;
        if (hasTraitName || hasTraitEffect)
        {
            string traitName = hasTraitName ? FlavorTrait : "Trait";
            traitSummary = hasTraitEffect
                ? $"{traitName}: {TraitGameplaySummary}"
                : traitName;
        }

        if (!string.IsNullOrWhiteSpace(rookieSummary) && !string.IsNullOrWhiteSpace(traitSummary))
            return $"{rookieSummary}  |  {traitSummary}";

        return !string.IsNullOrWhiteSpace(rookieSummary) ? rookieSummary : traitSummary;
    }

    private int GetEffectiveStat(int baseStat, int traitBonus, int rookieStatPenalty)
    {
        int value = Mathf.Max(1, baseStat) + Mathf.Max(0, traitBonus);

        if (IsRookie)
            value -= Mathf.Max(0, rookieStatPenalty);

        return Mathf.Max(1, value);
    }
}