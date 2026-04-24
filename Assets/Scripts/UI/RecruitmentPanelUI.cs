using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RecruitmentPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RecruitmentManager recruitmentManager;
    [SerializeField] private GameState gameState;

    [Header("List")]
    [SerializeField] private TextMeshProUGUI txtTitle;
    [SerializeField] private Transform candidateListContent;
    [SerializeField] private RecruitCandidateSlotUI candidateSlotPrefab;
    [SerializeField] private Button btnClose;

    [Header("Profile")]
    [SerializeField] private TextMeshProUGUI txtCandidateName;
    [SerializeField] private Image imgPortrait;
    [SerializeField] private TextMeshProUGUI txtStats;
    [SerializeField] private TextMeshProUGUI txtSource;
    [SerializeField] private TextMeshProUGUI txtBio;

    [Header("Dossier")]
    [SerializeField] private TextMeshProUGUI txtVerifiedIntelTitle;
    [SerializeField] private Transform verifiedIntelContent;
    [SerializeField] private TextMeshProUGUI txtClaimsTitle;
    [SerializeField] private Transform claimsContent;
    [SerializeField] private RecruitmentDossierEntryUI dossierEntryPrefab;

    [Header("Review")]
    [SerializeField] private TextMeshProUGUI txtReviewStatus;
    [SerializeField] private Button btnMarkLoyal;
    [SerializeField] private Button btnMarkImplant;

    [Header("Copy")]
    [TextArea(2, 4)]
    [SerializeField] private string defaultReviewMessage =
        "Compare the verified intel against the candidate's claims, then mark Loyal or Implant.";

    [SerializeField] private string emptyBioText =
        "No biography has been authored for this candidate yet.";

    [SerializeField] private string emptyVerifiedIntelText =
        "No authored verified intel is available for this candidate.";

    [SerializeField] private string emptyClaimsText =
        "No authored interview answers or documents are available for this candidate.";

    [Header("Entry Colors")]
    [SerializeField] private Color verifiedEntryColor = new Color(0.17f, 0.26f, 0.20f, 0.95f);
    [SerializeField] private Color verifiedAccentColor = new Color(0.35f, 0.75f, 0.45f, 1f);
    [SerializeField] private Color hiddenIntelEntryColor = new Color(0.22f, 0.22f, 0.22f, 0.95f);
    [SerializeField] private Color hiddenIntelAccentColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color interviewEntryColor = new Color(0.16f, 0.22f, 0.30f, 0.95f);
    [SerializeField] private Color interviewAccentColor = new Color(0.40f, 0.63f, 0.88f, 1f);
    [SerializeField] private Color documentEntryColor = new Color(0.28f, 0.22f, 0.16f, 0.95f);
    [SerializeField] private Color documentAccentColor = new Color(0.82f, 0.66f, 0.34f, 1f);

    private readonly List<RecruitCandidateSlotUI> spawnedSlots = new();
    private readonly List<RecruitmentDossierEntryUI> spawnedVerifiedEntries = new();
    private readonly List<RecruitmentDossierEntryUI> spawnedClaimEntries = new();

    private PendingRecruitCandidate selectedCandidate;
    private string lastResultMessage = string.Empty;

    private void OnEnable()
    {
        if (btnClose != null)
            btnClose.onClick.AddListener(Close);

        if (btnMarkLoyal != null)
            btnMarkLoyal.onClick.AddListener(OnMarkLoyalClicked);

        if (btnMarkImplant != null)
            btnMarkImplant.onClick.AddListener(OnMarkImplantClicked);

        if (recruitmentManager != null)
        {
            recruitmentManager.OnPendingCandidatesChanged += OnPendingCandidatesChanged;
            recruitmentManager.OnRecruitmentIntelChanged += OnRecruitmentIntelChanged;
        }

        RebuildList();
    }

    private void OnDisable()
    {
        if (btnClose != null)
            btnClose.onClick.RemoveListener(Close);

        if (btnMarkLoyal != null)
            btnMarkLoyal.onClick.RemoveListener(OnMarkLoyalClicked);

        if (btnMarkImplant != null)
            btnMarkImplant.onClick.RemoveListener(OnMarkImplantClicked);

        if (recruitmentManager != null)
        {
            recruitmentManager.OnPendingCandidatesChanged -= OnPendingCandidatesChanged;
            recruitmentManager.OnRecruitmentIntelChanged -= OnRecruitmentIntelChanged;
        }
    }

    public void Open()
    {
        if (gameState != null && gameState.IsRunEnded)
            return;

        gameObject.SetActive(true);
        RebuildList();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    private void OnPendingCandidatesChanged()
    {
        RebuildList();
    }

    private void OnRecruitmentIntelChanged()
    {
        RefreshDetails();
    }

    private void RebuildList()
    {
        DestroySpawned(spawnedSlots);

        int pendingCount = recruitmentManager != null ? recruitmentManager.PendingCount : 0;
        if (txtTitle != null)
            txtTitle.text = pendingCount > 0
                ? $"Recruitment Center ({pendingCount})"
                : "Recruitment Center";

        if (recruitmentManager == null)
        {
            selectedCandidate = null;
            RefreshDetails();
            return;
        }

        IReadOnlyList<PendingRecruitCandidate> candidates = recruitmentManager.PendingCandidates;

        if (selectedCandidate != null && !ContainsCandidate(candidates, selectedCandidate))
            selectedCandidate = null;

        if (candidateSlotPrefab != null && candidateListContent != null)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                PendingRecruitCandidate candidate = candidates[i];
                RecruitCandidateSlotUI slot = Instantiate(candidateSlotPrefab, candidateListContent);
                slot.Setup(candidate, candidate == selectedCandidate, OnCandidateSelected);
                spawnedSlots.Add(slot);
            }
        }

        if (selectedCandidate == null && candidates.Count > 0)
            selectedCandidate = candidates[0];

        RefreshSlotSelection();
        RefreshDetails();
    }

    private static bool ContainsCandidate(
        IReadOnlyList<PendingRecruitCandidate> candidates,
        PendingRecruitCandidate target)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] == target)
                return true;
        }

        return false;
    }

    private void OnCandidateSelected(PendingRecruitCandidate candidate)
    {
        selectedCandidate = candidate;
        RefreshSlotSelection();
        RefreshDetails();
    }

    private void RefreshSlotSelection()
    {
        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            if (spawnedSlots[i] != null)
                spawnedSlots[i].SetSelected(spawnedSlots[i].Candidate == selectedCandidate);
        }
    }

    private void RefreshDetails()
    {
        bool hasCandidate = selectedCandidate != null;

        if (txtCandidateName != null)
            txtCandidateName.text = hasCandidate
                ? selectedCandidate.CandidateName
                : "No pending candidates";

        if (imgPortrait != null)
        {
            bool hasPortrait = hasCandidate && selectedCandidate.Portrait != null;
            imgPortrait.sprite = hasPortrait ? selectedCandidate.Portrait : null;
            imgPortrait.enabled = hasPortrait;
        }

        if (txtStats != null)
        {
            if (hasCandidate)
            {
                bool hasTraitName = !string.IsNullOrWhiteSpace(selectedCandidate.FlavorTrait);
                bool hasTraitEffect = selectedCandidate.TraitGameplayEffect != RecruitmentTraitGameplayEffect.None;

                string trait = hasTraitName
                    ? selectedCandidate.FlavorTrait
                    : hasTraitEffect
                        ? "Trait"
                        : "No authored trait";

                string traitGameplay = hasTraitEffect
                    ? selectedCandidate.TraitGameplaySummary
                    : "No gameplay effect";

                string rookieSummary = recruitmentManager != null
                    ? GetCandidateRookieSummary()
                    : "If cleared: joins as a rookie.";

                txtStats.text =
                    $"INT {selectedCandidate.INT}  |  STR {selectedCandidate.STR}  |  AGI {selectedCandidate.AGI}\n" +
                    $"Trait: {trait} ({traitGameplay})\n" +
                    rookieSummary;
            }
            else
            {
                txtStats.text = "No one is waiting for review.";
            }
        }

        if (txtSource != null)
        {
            if (hasCandidate)
            {
                string districtName =
                    selectedCandidate.SourceDistrict != null &&
                    selectedCandidate.SourceDistrict.Data != null
                        ? selectedCandidate.SourceDistrict.Data.districtName
                        : "Unknown District";

                string missionName =
                    selectedCandidate.SourceMission != null
                        ? selectedCandidate.SourceMission.missionName
                        : "Unknown Mission";

                int revealCount = recruitmentManager != null
                    ? recruitmentManager.VerifiedFactRevealCount
                    : 0;

                int centerLevel = recruitmentManager != null
                    ? recruitmentManager.RecruitmentCenterLevel
                    : 0;

                int revealedFacts = selectedCandidate.GetCurrentlyRevealedTrueFactCount(revealCount);

                txtSource.text =
                    $"From {districtName} via {missionName}\n" +
                    $"Center Lv {centerLevel}  |  Verified {revealedFacts}/{selectedCandidate.TrueFactCount}  |  Claims {selectedCandidate.PresentedFactCount}";
            }
            else
            {
                txtSource.text = string.Empty;
            }
        }

        if (txtBio != null)
        {
            txtBio.text = hasCandidate && !string.IsNullOrWhiteSpace(selectedCandidate.Bio)
                ? selectedCandidate.Bio
                : hasCandidate
                    ? emptyBioText
                    : string.Empty;
        }

        RebuildVerifiedIntel(hasCandidate ? selectedCandidate : null);
        RebuildClaims(hasCandidate ? selectedCandidate : null);

        if (txtReviewStatus != null)
        {
            if (hasCandidate)
            {
                txtReviewStatus.text = string.IsNullOrWhiteSpace(lastResultMessage)
                    ? defaultReviewMessage
                    : $"{lastResultMessage}\n\n{defaultReviewMessage}";
            }
            else
            {
                txtReviewStatus.text = string.IsNullOrWhiteSpace(lastResultMessage)
                    ? "No pending candidates."
                    : lastResultMessage;
            }
        }

        bool canJudge = hasCandidate && (gameState == null || !gameState.IsRunEnded);

        if (btnMarkLoyal != null)
            btnMarkLoyal.interactable = canJudge;

        if (btnMarkImplant != null)
            btnMarkImplant.interactable = canJudge;
    }

    private void RebuildVerifiedIntel(PendingRecruitCandidate candidate)
    {
        DestroySpawned(spawnedVerifiedEntries);

        if (txtVerifiedIntelTitle != null)
            txtVerifiedIntelTitle.text = "Verified Intel";

        if (candidate == null)
        {
            RefreshDossierLayout(verifiedIntelContent);
            return;
        }

        RecruitmentVerifiedFactView[] intel = recruitmentManager != null
            ? recruitmentManager.GetVerifiedIntelFor(candidate)
            : candidate.GetVerifiedIntel(0);

        int revealedCount = 0;
        for (int i = 0; i < intel.Length; i++)
        {
            if (intel[i] != null && intel[i].isRevealed)
                revealedCount++;
        }

        if (txtVerifiedIntelTitle != null)
            txtVerifiedIntelTitle.text = $"Verified Intel ({revealedCount}/{intel.Length})";

        if (intel.Length == 0)
        {
            SpawnDossierEntry(
                verifiedIntelContent,
                spawnedVerifiedEntries,
                "Verified Intel",
                emptyVerifiedIntelText,
                "Empty",
                hiddenIntelEntryColor,
                hiddenIntelAccentColor);

            RefreshDossierLayout(verifiedIntelContent);
            return;
        }

        for (int i = 0; i < intel.Length; i++)
        {
            RecruitmentVerifiedFactView fact = intel[i];

            string label = fact != null && !string.IsNullOrWhiteSpace(fact.label)
                ? fact.label
                : $"Fact {i + 1}";

            string body = fact != null && !string.IsNullOrWhiteSpace(fact.text)
                ? fact.text
                : "-";

            bool isRevealed = fact != null && fact.isRevealed;

            SpawnDossierEntry(
                verifiedIntelContent,
                spawnedVerifiedEntries,
                label,
                body,
                isRevealed ? "Verified" : "Pending",
                isRevealed ? verifiedEntryColor : hiddenIntelEntryColor,
                isRevealed ? verifiedAccentColor : hiddenIntelAccentColor);
        }

        RefreshDossierLayout(verifiedIntelContent);
    }

    private void RebuildClaims(PendingRecruitCandidate candidate)
    {
        DestroySpawned(spawnedClaimEntries);

        if (txtClaimsTitle != null)
            txtClaimsTitle.text = "Claims";

        if (candidate == null)
        {
            RefreshDossierLayout(claimsContent);
            return;
        }

        RecruitmentClaim[] claims = candidate.PresentedFacts;
        int interviewCount = candidate.GetPresentedClaimCount(RecruitmentClaimSource.Interview);
        int documentCount = candidate.GetPresentedClaimCount(RecruitmentClaimSource.Document);

        if (txtClaimsTitle != null)
            txtClaimsTitle.text =
                $"Claims ({claims.Length})  |  Interview {interviewCount}  |  Documents {documentCount}";

        if (claims.Length == 0)
        {
            SpawnDossierEntry(
                claimsContent,
                spawnedClaimEntries,
                "Claims",
                emptyClaimsText,
                "Empty",
                interviewEntryColor,
                interviewAccentColor);

            RefreshDossierLayout(claimsContent);
            return;
        }

        for (int i = 0; i < claims.Length; i++)
        {
            RecruitmentClaim claim = claims[i];
            RecruitmentClaimSource source = claim != null
                ? claim.source
                : RecruitmentClaimSource.Interview;

            bool isDocument = source == RecruitmentClaimSource.Document;

            string label = claim != null && !string.IsNullOrWhiteSpace(claim.label)
                ? claim.label
                : $"Claim {i + 1}";

            string body = claim != null && !string.IsNullOrWhiteSpace(claim.text)
                ? claim.text
                : "-";

            SpawnDossierEntry(
                claimsContent,
                spawnedClaimEntries,
                label,
                body,
                isDocument ? "Document" : "Interview",
                isDocument ? documentEntryColor : interviewEntryColor,
                isDocument ? documentAccentColor : interviewAccentColor);
        }

        RefreshDossierLayout(claimsContent);
    }

    private void SpawnDossierEntry(
        Transform parent,
        List<RecruitmentDossierEntryUI> cache,
        string label,
        string body,
        string tag,
        Color backgroundColor,
        Color accentColor)
    {
        if (parent == null || dossierEntryPrefab == null)
            return;

        RecruitmentDossierEntryUI entry = Instantiate(dossierEntryPrefab, parent);
        entry.Setup(label, body, tag, backgroundColor, accentColor);
        cache.Add(entry);
    }

    private void OnMarkLoyalClicked()
    {
        ResolveSelectedCandidate(CandidateVettingOutcome.Loyal);
    }

    private void OnMarkImplantClicked()
    {
        ResolveSelectedCandidate(CandidateVettingOutcome.Implant);
    }

    private void ResolveSelectedCandidate(CandidateVettingOutcome judgment)
    {
        if (selectedCandidate == null || recruitmentManager == null)
            return;

        PendingRecruitCandidate candidate = selectedCandidate;
        RecruitmentResolutionOutcome outcome = recruitmentManager.JudgeCandidate(candidate, judgment);

        lastResultMessage = outcome switch
        {
            RecruitmentResolutionOutcome.LoyalAccepted =>
                BuildLoyalAcceptedMessage(candidate),
            RecruitmentResolutionOutcome.LoyalRejected =>
                $"{candidate.CandidateName} was loyal, but you rejected them. They are gone.",
            RecruitmentResolutionOutcome.ImplantRejected =>
                $"{candidate.CandidateName} was an implant and was safely rejected.",
            RecruitmentResolutionOutcome.ImplantAccepted =>
                BuildImplantAcceptedMessage(candidate),
            _ =>
                "Candidate review could not be completed."
        };

        selectedCandidate = null;
        RebuildList();
    }

    private string BuildLoyalAcceptedMessage(PendingRecruitCandidate candidate)
    {
        string rookieLine = recruitmentManager != null
            ? GetAcceptedRookieSummary()
            : "Joined the roster as a rookie.";

        string traitLine = BuildTraitLine(candidate);
        return $"{candidate.CandidateName} was loyal and joined the roster.\n{rookieLine}{traitLine}";
    }

    private string BuildImplantAcceptedMessage(PendingRecruitCandidate candidate)
    {
        string sabotageSummary = recruitmentManager != null
            ? recruitmentManager.GetActiveImplantPenaltySummary()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(sabotageSummary))
            sabotageSummary = "Mission success has been reduced temporarily.";

        return
            $"{candidate.CandidateName} was an implant and slipped through vetting.\nSabotage active: {sabotageSummary}.";
    }

    private static string BuildTraitLine(PendingRecruitCandidate candidate)
    {
        if (candidate == null)
            return string.Empty;

        bool hasTraitName = !string.IsNullOrWhiteSpace(candidate.FlavorTrait);
        bool hasTraitEffect = candidate.TraitGameplayEffect != RecruitmentTraitGameplayEffect.None;
        if (!hasTraitName && !hasTraitEffect)
            return string.Empty;

        string gameplay = hasTraitEffect
            ? candidate.TraitGameplaySummary
            : "no gameplay effect";

        string traitName = hasTraitName ? candidate.FlavorTrait : "Trait";
        return $"\nTrait ready: {traitName} ({gameplay})";
    }

    private string GetCandidateRookieSummary()
    {
        if (recruitmentManager == null)
            return "If cleared: joins as a rookie.";

        if (recruitmentManager.RookieMissionStatPenalty <= 0)
            return $"If cleared: rookie status for {GetMissionCountLabel(recruitmentManager.RookieMissionsToGraduate)}.";

        return
            $"If cleared: rookie (-{recruitmentManager.RookieMissionStatPenalty} all mission stats for {GetMissionCountLabel(recruitmentManager.RookieMissionsToGraduate)})";
    }

    private string GetAcceptedRookieSummary()
    {
        if (recruitmentManager == null)
            return "Joined the roster as a rookie.";

        if (recruitmentManager.RookieMissionStatPenalty <= 0)
            return $"Joined the roster as a rookie for {GetMissionCountLabel(recruitmentManager.RookieMissionsToGraduate)}.";

        return
            $"Joined the roster as a rookie: -{recruitmentManager.RookieMissionStatPenalty} all mission stats for {GetMissionCountLabel(recruitmentManager.RookieMissionsToGraduate)}.";
    }

    private static string GetMissionCountLabel(int missionCount)
    {
        int safeCount = Mathf.Max(1, missionCount);
        string missionLabel = safeCount == 1 ? "1 mission" : $"{safeCount} missions";
        return missionLabel;
    }

    private void DestroySpawned<T>(List<T> items) where T : Component
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null)
                Destroy(items[i].gameObject);
        }

        items.Clear();
    }

    private static void RefreshDossierLayout(Transform contentRoot)
    {
        if (contentRoot is not RectTransform rectTransform)
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
    }
}