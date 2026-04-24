using UnityEngine;

[CreateAssetMenu(
    fileName = "RecruitmentCandidate",
    menuName = "Gameplay/Recruitment/Candidate Definition")]
public class RecruitmentCandidateDefinition : ScriptableObject
{
    [Header("Identity")]
    public string candidateName = "New Candidate";
    public Sprite portrait;

    [TextArea(4, 8)]
    public string bio = "";

    [Tooltip("Single authored flavor trait for Phase 1 and later dossier UI.")]
    public string flavorTrait = "";

    [Header("Trait Gameplay")]
    [Tooltip("Optional lightweight gameplay effect tied to the authored flavor trait.")]
    public RecruitmentTraitGameplayEffect traitGameplayEffect = RecruitmentTraitGameplayEffect.None;

    [Min(0)]
    [Tooltip("Used by INT / STR / AGI trait bonuses.")]
    public int traitStatBonus = 1;

    [Range(0f, 0.5f)]
    [Tooltip("Used only when the trait adds flat mission success chance.")]
    public float traitSuccessChanceBonus = 0.05f;

    [Header("Hidden Truth")]
    public RecruitmentCandidateLoyalty loyalty = RecruitmentCandidateLoyalty.Loyal;
    public RecruitmentFact[] trueFacts = new RecruitmentFact[0];

    [Header("Presented Information")]
    [Tooltip("Interview claims and document-backed claims shown to the player in later phases.")]
    public RecruitmentClaim[] presentedFacts = new RecruitmentClaim[0];

    [Header("Agent Stats")]
    [Min(1)] public int startingINT = 1;
    [Min(1)] public int startingSTR = 1;
    [Min(1)] public int startingAGI = 1;

    [Header("Later Recruitment Metadata")]
    [Tooltip("Freeform metadata reserved for later recruitment phases.")]
    public string rookieTag = "";
    [TextArea(2, 4)] public string rookieNotes = "";

    [Header("Availability")]
    [Tooltip("Turn this off to prevent this candidate from being surfaced by missions.")]
    public bool isAvailable = true;

    private void OnValidate()
    {
        startingINT = Mathf.Max(1, startingINT);
        startingSTR = Mathf.Max(1, startingSTR);
        startingAGI = Mathf.Max(1, startingAGI);
        traitStatBonus = Mathf.Max(0, traitStatBonus);
        traitSuccessChanceBonus = Mathf.Clamp(traitSuccessChanceBonus, 0f, 0.5f);
    }
}