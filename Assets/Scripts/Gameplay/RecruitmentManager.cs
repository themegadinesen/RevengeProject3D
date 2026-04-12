// Assets/Scripts/Gameplay/RecruitmentManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public class RecruitmentManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AgentRoster agentRoster;
    [SerializeField] private GameState gameState;

    [Header("Fallback Candidate Pool")]
    [Tooltip("Used when a recruit-style mission succeeds but the mission itself has no candidate pool assigned.")]
    [SerializeField] private AgentData[] fallbackCandidatePool;

    private readonly List<PendingRecruitCandidate> pendingCandidates = new();

    public IReadOnlyList<PendingRecruitCandidate> PendingCandidates => pendingCandidates;
    public int PendingCount => pendingCandidates.Count;
    public bool HasPendingCandidates => pendingCandidates.Count > 0;

    public event Action OnPendingCandidatesChanged;
    public event Action<PendingRecruitCandidate> OnCandidateArrived;
    public event Action<PendingRecruitCandidate, CandidateVettingOutcome> OnCandidateResolved;

    public PendingRecruitCandidate CreateCandidateFromMission(
        MissionData mission,
        RuntimeDistrict district)
    {
        if (mission == null)
            return null;

        AgentData template = PickCandidateTemplate(mission);
        if (template == null)
        {
            Debug.LogWarning(
                "RecruitmentManager: Recruit mission succeeded but no candidate template was available.",
                this);
            return null;
        }

        var candidate = new PendingRecruitCandidate(template, mission, district);
        pendingCandidates.Add(candidate);

        OnPendingCandidatesChanged?.Invoke();
        OnCandidateArrived?.Invoke(candidate);

        return candidate;
    }

    public CandidateVettingOutcome EvaluateCandidatePlaceholder(
        PendingRecruitCandidate candidate)
    {
        if (candidate == null)
            return CandidateVettingOutcome.None;

        if (gameState != null && gameState.IsRunEnded)
            return CandidateVettingOutcome.None;

        bool isLoyal = UnityEngine.Random.value < 0.5f;
        return ResolveCandidate(candidate, isLoyal);
    }

    public CandidateVettingOutcome ResolveCandidate(
        PendingRecruitCandidate candidate,
        bool isLoyal)
    {
        if (candidate == null)
            return CandidateVettingOutcome.None;

        if (!pendingCandidates.Remove(candidate))
            return CandidateVettingOutcome.None;

        CandidateVettingOutcome outcome = isLoyal
            ? CandidateVettingOutcome.Loyal
            : CandidateVettingOutcome.Implant;

        if (outcome == CandidateVettingOutcome.Loyal && agentRoster != null)
            agentRoster.RecruitCandidate(candidate);

        OnPendingCandidatesChanged?.Invoke();
        OnCandidateResolved?.Invoke(candidate, outcome);

        return outcome;
    }

    private AgentData PickCandidateTemplate(MissionData mission)
    {
        AgentData[] sourcePool =
            mission.candidatePool != null && mission.candidatePool.Length > 0
                ? mission.candidatePool
                : fallbackCandidatePool;

        if (sourcePool == null || sourcePool.Length == 0)
            return null;

        var validPool = new List<AgentData>();
        foreach (var candidate in sourcePool)
        {
            if (candidate != null)
                validPool.Add(candidate);
        }

        if (validPool.Count == 0)
            return null;

        int index = UnityEngine.Random.Range(0, validPool.Count);
        return validPool[index];
    }
}