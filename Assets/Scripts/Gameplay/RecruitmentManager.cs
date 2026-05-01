using System;
using System.Collections.Generic;
using UnityEngine;

public class RecruitmentManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AgentRoster agentRoster;
    [SerializeField] private GameState gameState;

    [Header("Recruitment Intel")]
    [SerializeField] private BaseProgressionManager baseProgression;
    [SerializeField] private RecruitmentDossierConfig dossierConfig;

    [Header("Phase 4 Vetting")]
    [Min(0)]
    [SerializeField] private int rookieMissionStatPenalty = 1;
    [Min(1)]
    [SerializeField] private int rookieMissionsToGraduate = 1;
    [Range(0f, 0.75f)]
    [SerializeField] private float implantMissionSuccessPenalty = 0.2f;
    [Min(0)]
    [SerializeField] private int implantPenaltyMissionCount = 3;

    private readonly List<PendingRecruitCandidate> pendingCandidates = new();
    private readonly HashSet<RecruitmentCandidateDefinition> surfacedThisRun = new();

    private int lastKnownRecruitmentCenterLevel = -1;
    private int remainingImplantPenaltyMissions;

    public IReadOnlyList<PendingRecruitCandidate> PendingCandidates => pendingCandidates;
    public int PendingCount => pendingCandidates.Count;
    public bool HasPendingCandidates => pendingCandidates.Count > 0;
    public AgentRoster AgentRoster => agentRoster;
    public bool HasCapacityForLoyalRecruit =>
        agentRoster == null || agentRoster.HasCapacityForRecruit;
    public int RookieMissionStatPenalty => Mathf.Max(0, rookieMissionStatPenalty);
    public int RookieMissionsToGraduate => Mathf.Max(1, rookieMissionsToGraduate);
    public int RemainingImplantPenaltyMissions => remainingImplantPenaltyMissions;
    public bool HasActiveImplantPenalty =>
        implantMissionSuccessPenalty > 0f && remainingImplantPenaltyMissions > 0;

    public float CurrentImplantMissionSuccessPenalty =>
        HasActiveImplantPenalty
            ? Mathf.Max(0f, implantMissionSuccessPenalty)
            : 0f;

    public int RecruitmentCenterLevel =>
        dossierConfig != null
            ? dossierConfig.GetRecruitmentCenterLevel(baseProgression)
            : 0;

    public int VerifiedFactRevealCount =>
        dossierConfig != null
            ? dossierConfig.GetVerifiedFactRevealCount(baseProgression)
            : 0;

    public string HiddenFactPlaceholder =>
        dossierConfig != null
            ? dossierConfig.GetHiddenFactPlaceholder()
            : "???";

    public event Action OnPendingCandidatesChanged;
    public event Action OnRecruitmentIntelChanged;
    public event Action<PendingRecruitCandidate> OnCandidateArrived;
    public event Action<PendingRecruitCandidate, RecruitmentResolutionOutcome> OnCandidateResolved;

    private void OnEnable()
    {
        if (baseProgression != null)
            baseProgression.OnBuildingStateChanged += OnBuildingStateChanged;

        lastKnownRecruitmentCenterLevel = RecruitmentCenterLevel;
    }

    private void OnDisable()
    {
        if (baseProgression != null)
            baseProgression.OnBuildingStateChanged -= OnBuildingStateChanged;
    }

    public PendingRecruitCandidate CreateCandidateFromMission(
        MissionData mission,
        RuntimeDistrict district)
    {
        if (mission == null)
            return null;

        RecruitmentCandidateDefinition definition = PickCandidateDefinition(mission);
        if (definition == null)
        {
            Debug.LogWarning(
                $"RecruitmentManager: Recruit mission '{mission.missionName}' did not surface a valid authored candidate.",
                this);
            return null;
        }

        var candidate = new PendingRecruitCandidate(definition, mission, district);
        pendingCandidates.Add(candidate);
        surfacedThisRun.Add(definition);

        OnPendingCandidatesChanged?.Invoke();
        OnCandidateArrived?.Invoke(candidate);

        return candidate;
    }

    public RecruitmentResolutionOutcome JudgeCandidate(
        PendingRecruitCandidate candidate,
        CandidateVettingOutcome judgment)
    {
        if (candidate == null)
            return RecruitmentResolutionOutcome.None;

        if (judgment == CandidateVettingOutcome.None)
            return RecruitmentResolutionOutcome.None;

        if (gameState != null && gameState.IsRunEnded)
            return RecruitmentResolutionOutcome.None;

        return ResolveCandidate(candidate, judgment);
    }

    public RecruitmentVerifiedFactView[] GetVerifiedIntelFor(PendingRecruitCandidate candidate)
    {
        if (candidate == null)
            return Array.Empty<RecruitmentVerifiedFactView>();

        return candidate.GetVerifiedIntel(VerifiedFactRevealCount, HiddenFactPlaceholder);
    }

    public void ClearRunState()
    {
        pendingCandidates.Clear();
        surfacedThisRun.Clear();
        remainingImplantPenaltyMissions = 0;
        OnPendingCandidatesChanged?.Invoke();
    }

    public float ConsumeImplantMissionSuccessPenalty()
    {
        if (!HasActiveImplantPenalty)
            return 0f;

        remainingImplantPenaltyMissions = Mathf.Max(0, remainingImplantPenaltyMissions - 1);
        return Mathf.Max(0f, implantMissionSuccessPenalty);
    }

    public string GetActiveImplantPenaltySummary()
    {
        if (!HasActiveImplantPenalty)
            return string.Empty;

        string missionLabel = remainingImplantPenaltyMissions == 1 ? "mission" : "missions";
        return
            $"-{CurrentImplantMissionSuccessPenalty * 100f:F0}% mission success for the next {remainingImplantPenaltyMissions} {missionLabel}";
    }

    private RecruitmentResolutionOutcome ResolveCandidate(
        PendingRecruitCandidate candidate,
        CandidateVettingOutcome judgment)
    {
        if (candidate == null)
            return RecruitmentResolutionOutcome.None;

        RecruitmentResolutionOutcome outcome = candidate.GetResolutionOutcome(judgment);
        if (outcome == RecruitmentResolutionOutcome.LoyalAccepted &&
            agentRoster != null &&
            !agentRoster.HasCapacityForRecruit)
        {
            OnCandidateResolved?.Invoke(candidate, RecruitmentResolutionOutcome.RosterFull);
            return RecruitmentResolutionOutcome.RosterFull;
        }

        if (!pendingCandidates.Remove(candidate))
            return RecruitmentResolutionOutcome.None;

        switch (outcome)
        {
            case RecruitmentResolutionOutcome.LoyalAccepted:
                if (agentRoster != null)
                    agentRoster.RecruitCandidate(candidate, RookieMissionsToGraduate);
                break;

            case RecruitmentResolutionOutcome.ImplantAccepted:
                ApplyImplantPenalty();
                break;
        }

        OnPendingCandidatesChanged?.Invoke();
        OnCandidateResolved?.Invoke(candidate, outcome);

        return outcome;
    }

    private void OnBuildingStateChanged(RuntimeBuilding runtimeBuilding)
    {
        int currentLevel = RecruitmentCenterLevel;
        if (currentLevel == lastKnownRecruitmentCenterLevel)
            return;

        lastKnownRecruitmentCenterLevel = currentLevel;
        OnRecruitmentIntelChanged?.Invoke();
    }

    private RecruitmentCandidateDefinition PickCandidateDefinition(MissionData mission)
    {
        if (mission == null)
            return null;

        RecruitmentCandidateDefinition candidate = mission.rewardCandidate;
        if (candidate == null)
            return null;

        if (!candidate.isAvailable)
            return null;

        if (IsPending(candidate))
            return null;

        if (surfacedThisRun.Contains(candidate))
            return null;

        return candidate;
    }

    private bool IsPending(RecruitmentCandidateDefinition definition)
    {
        for (int i = 0; i < pendingCandidates.Count; i++)
        {
            if (pendingCandidates[i].Definition == definition)
                return true;
        }

        return false;
    }

    private void ApplyImplantPenalty()
    {
        if (implantPenaltyMissionCount <= 0 || implantMissionSuccessPenalty <= 0f)
            return;

        remainingImplantPenaltyMissions += implantPenaltyMissionCount;
    }

    private void OnValidate()
    {
        rookieMissionStatPenalty = Mathf.Max(0, rookieMissionStatPenalty);
        rookieMissionsToGraduate = Mathf.Max(1, rookieMissionsToGraduate);
        implantMissionSuccessPenalty = Mathf.Clamp(implantMissionSuccessPenalty, 0f, 0.75f);
        implantPenaltyMissionCount = Mathf.Max(0, implantPenaltyMissionCount);
    }
}