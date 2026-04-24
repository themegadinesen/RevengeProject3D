using System;
using UnityEngine;

[System.Serializable]
public class PendingRecruitCandidate
{
    public RecruitmentCandidateDefinition Definition { get; private set; }
    public string CandidateName { get; private set; }
    public Sprite Portrait { get; private set; }
    public string Bio { get; private set; }
    public string FlavorTrait { get; private set; }
    public int INT { get; private set; }
    public int STR { get; private set; }
    public int AGI { get; private set; }
    public RecruitmentTraitGameplayEffect TraitGameplayEffect { get; private set; }
    public int TraitStatBonus { get; private set; }
    public float TraitSuccessChanceBonus { get; private set; }
    public MissionData SourceMission { get; private set; }
    public RuntimeDistrict SourceDistrict { get; private set; }
    public float ArrivalTime { get; private set; }

    public RecruitmentCandidateLoyalty HiddenLoyalty =>
        Definition != null ? Definition.loyalty : RecruitmentCandidateLoyalty.Loyal;

    public RecruitmentFact[] TrueFacts =>
        Definition != null && Definition.trueFacts != null
            ? Definition.trueFacts
            : Array.Empty<RecruitmentFact>();

    public RecruitmentClaim[] PresentedFacts =>
        Definition != null && Definition.presentedFacts != null
            ? Definition.presentedFacts
            : Array.Empty<RecruitmentClaim>();

    public int TrueFactCount => TrueFacts.Length;
    public int PresentedFactCount => PresentedFacts.Length;

    public bool IsActuallyLoyal => HiddenLoyalty == RecruitmentCandidateLoyalty.Loyal;
    public string TraitGameplaySummary =>
        RecruitmentTraitGameplayUtility.GetEffectSummary(
            TraitGameplayEffect,
            TraitStatBonus,
            TraitSuccessChanceBonus);

    public PendingRecruitCandidate(
        RecruitmentCandidateDefinition definition,
        MissionData sourceMission,
        RuntimeDistrict sourceDistrict)
    {
        Definition = definition;
        SourceMission = sourceMission;
        SourceDistrict = sourceDistrict;
        ArrivalTime = Time.time;

        if (definition == null)
        {
            CandidateName = "Unknown Candidate";
            Bio = "";
            FlavorTrait = "";
            INT = 1;
            STR = 1;
            AGI = 1;
            TraitGameplayEffect = RecruitmentTraitGameplayEffect.None;
            TraitStatBonus = 0;
            TraitSuccessChanceBonus = 0f;
            return;
        }

        CandidateName = !string.IsNullOrWhiteSpace(definition.candidateName)
            ? definition.candidateName
            : "Unnamed Candidate";
        Portrait = definition.portrait;
        Bio = definition.bio;
        FlavorTrait = definition.flavorTrait;
        INT = definition.startingINT;
        STR = definition.startingSTR;
        AGI = definition.startingAGI;
        TraitGameplayEffect = definition.traitGameplayEffect;
        TraitStatBonus = definition.traitStatBonus;
        TraitSuccessChanceBonus = definition.traitSuccessChanceBonus;
    }

    public RecruitmentResolutionOutcome GetResolutionOutcome(CandidateVettingOutcome judgment)
    {
        return RecruitmentVettingUtility.ResolveOutcome(HiddenLoyalty, judgment);
    }

    public int GetCurrentlyRevealedTrueFactCount(int maxRevealedFacts)
    {
        return Mathf.Clamp(maxRevealedFacts, 0, TrueFacts.Length);
    }

    public int GetPresentedClaimCount(RecruitmentClaimSource source)
    {
        RecruitmentClaim[] claims = PresentedFacts;
        int count = 0;

        for (int i = 0; i < claims.Length; i++)
        {
            RecruitmentClaim claim = claims[i];
            if (claim != null && claim.source == source)
                count++;
        }

        return count;
    }

    public RecruitmentVerifiedFactView[] GetVerifiedIntel(
        int maxRevealedFacts,
        string hiddenPlaceholder = "???")
    {
        RecruitmentFact[] facts = TrueFacts;
        if (facts.Length == 0)
            return Array.Empty<RecruitmentVerifiedFactView>();

        int revealedCount = Mathf.Clamp(maxRevealedFacts, 0, facts.Length);

        if (string.IsNullOrWhiteSpace(hiddenPlaceholder))
            hiddenPlaceholder = "???";

        RecruitmentVerifiedFactView[] result = new RecruitmentVerifiedFactView[facts.Length];

        for (int i = 0; i < facts.Length; i++)
        {
            RecruitmentFact fact = facts[i];
            bool isRevealed = i < revealedCount;

            string label = fact != null && !string.IsNullOrWhiteSpace(fact.label)
                ? fact.label
                : $"Fact {i + 1}";

            string text = isRevealed &&
                          fact != null &&
                          !string.IsNullOrWhiteSpace(fact.text)
                ? fact.text
                : hiddenPlaceholder;

            result[i] = new RecruitmentVerifiedFactView(label, text, isRevealed);
        }

        return result;
    }
}