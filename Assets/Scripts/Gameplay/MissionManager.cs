// Assets/Scripts/Gameplay/MissionManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public class MissionManager : MonoBehaviour
{
    [SerializeField] private GameState   gameState;
    [SerializeField] private AgentRoster agentRoster;

    [Header("Score → Success Chance")]
    [Tooltip("Success chance when team coverage = 0%.")]
    [Range(0f, 1f)]
    [SerializeField] private float minSuccessChance = 0.05f;

    [Tooltip("Success chance when team coverage = 100%.")]
    [Range(0f, 1f)]
    [SerializeField] private float maxSuccessChance = 0.95f;

    [Header("Score → Duration")]
    [Tooltip("Duration multiplier at worst coverage (>1 = slower).")]
    [SerializeField] private float worstDurationMult = 1.5f;

    [Tooltip("Duration multiplier at best coverage (<1 = faster).")]
    [SerializeField] private float bestDurationMult = 0.8f;

    [Header("Score → Rewards")]
    [Tooltip("Reward multiplier at worst coverage.")]
    [SerializeField] private float worstRewardMult = 0.5f;

    [Tooltip("Reward multiplier at best coverage.")]
    [SerializeField] private float bestRewardMult = 1.5f;

    // ── Events ────────────────────────────────────────────────────────
    public event Action<ActiveMission> OnMissionStarted;
    public event Action<MissionResult> OnMissionResolved;

    // ── Active Missions ───────────────────────────────────────────────
    private readonly List<ActiveMission> activeMissions = new();
    public IReadOnlyList<ActiveMission> ActiveMissions => activeMissions;
    public int ActiveMissionCount => activeMissions.Count;

    // ── Lifecycle ─────────────────────────────────────────────────────
    private void Update()
    {
        if (gameState.IsRunEnded) return;
        TickActiveMissions();
    }

    // ── Queries ───────────────────────────────────────────────────────
    public bool IsMissionActive(MissionData data)
    {
        for (int i = 0; i < activeMissions.Count; i++)
            if (activeMissions[i].Data == data) return true;
        return false;
    }

    public ActiveMission GetActiveMission(MissionData data)
    {
        for (int i = 0; i < activeMissions.Count; i++)
            if (activeMissions[i].Data == data) return activeMissions[i];
        return null;
    }

    // ── Scoring (public — UI calls these for live preview) ────────────
    public static float CalculateMissionScore(
        List<RuntimeAgent> team, MissionData mission)
    {
        int tINT = 0, tSTR = 0, tAGI = 0;
        foreach (var a in team)
        {
            tINT += a.INT;
            tSTR += a.STR;
            tAGI += a.AGI;
        }
        return CalculateScoreFromTotals(tINT, tSTR, tAGI, mission);
    }

    public static float CalculateScoreFromTotals(
        int tINT, int tSTR, int tAGI, MissionData m)
    {
        float sum   = 0f;
        int   count = 0;

        if (m.requiredINT > 0)
        {
            sum += Mathf.Clamp01((float)tINT / m.requiredINT);
            count++;
        }
        if (m.requiredSTR > 0)
        {
            sum += Mathf.Clamp01((float)tSTR / m.requiredSTR);
            count++;
        }
        if (m.requiredAGI > 0)
        {
            sum += Mathf.Clamp01((float)tAGI / m.requiredAGI);
            count++;
        }

        return count > 0 ? sum / count : 1f;
    }

    /// <summary>UI calls this to preview success chance for a given score.</summary>
    public float GetSuccessChance(float score) =>
        Mathf.Lerp(minSuccessChance, maxSuccessChance, score);

    /// <summary>UI calls this to preview effective duration.</summary>
    public float GetDurationMultiplier(float score) =>
        Mathf.Lerp(worstDurationMult, bestDurationMult, score);

    /// <summary>UI calls this to preview reward scaling.</summary>
    public float GetRewardMultiplier(float score) =>
        Mathf.Lerp(worstRewardMult, bestRewardMult, score);

    // ── Launch ────────────────────────────────────────────────────────
    /// <summary>
    /// Attempts to launch a mission with the given team.
    /// Returns false if validation fails.
    /// </summary>
    public bool TryLaunchMission(MissionData mission, List<RuntimeAgent> team)
    {
        if (mission == null || team == null || team.Count == 0) return false;
        if (gameState.IsRunEnded)    return false;
        if (IsMissionActive(mission)) return false;
        if (gameState.Money < mission.moneyCost) return false;

        // Validate every selected agent is actually available.
        foreach (var agent in team)
            if (agent.Status != AgentStatus.Available) return false;

        // Deduct money.
        gameState.AddMoney(-mission.moneyCost);

        // Mark agents busy.
        foreach (var agent in team)
            agentRoster.SetBusy(agent);

        // Compute score and derived multipliers.
        float score    = CalculateMissionScore(team, mission);
        float rewMult  = GetRewardMultiplier(score);

        // Duration 0 = instant resolve (backward-compatible).
        if (mission.duration <= 0f)
        {
            ResolveInstant(mission, team, score, rewMult);
            return true;
        }

        float durMult           = GetDurationMultiplier(score);
        float effectiveDuration = mission.duration * durMult;

        var active = new ActiveMission
        {
            Data               = mission,
            Duration           = effectiveDuration,
            TimeRemaining      = effectiveDuration,
            AssignedAgents     = new List<RuntimeAgent>(team),
            MissionScore       = score,
            RewardMultiplier   = rewMult,
            SlowBurnChaosApplied = 0f
        };

        activeMissions.Add(active);
        OnMissionStarted?.Invoke(active);
        return true;
    }

    // ── Ticking ───────────────────────────────────────────────────────
    private void TickActiveMissions()
    {
        for (int i = activeMissions.Count - 1; i >= 0; i--)
        {
            ActiveMission m = activeMissions[i];
            m.TimeRemaining -= Time.deltaTime;

            // Slow-burn: drip chaos proportionally.
            if (m.Data.behavior == MissionBehavior.SlowBurn)
            {
                float totalChaos = m.Data.chaosOnSuccess;
                if (totalChaos > 0f && m.SlowBurnChaosApplied < totalChaos)
                {
                    float tick = (totalChaos / m.Duration) * Time.deltaTime;
                    tick = Mathf.Min(tick, totalChaos - m.SlowBurnChaosApplied);
                    gameState.AddChaos(tick);
                    m.SlowBurnChaosApplied += tick;
                }
            }

            if (m.IsComplete)
            {
                ResolveTimed(m);
                activeMissions.RemoveAt(i);
            }
        }
    }

    // ── Resolution ────────────────────────────────────────────────────
    private void ResolveInstant(
        MissionData mission, List<RuntimeAgent> team,
        float score, float rewardMult)
    {
        float chance = GetSuccessChance(score);
        bool  success = UnityEngine.Random.value <= chance;

        MissionResult result = ApplyOutcome(mission, team, success, score, rewardMult);
        OnMissionResolved?.Invoke(result);
    }

    private void ResolveTimed(ActiveMission active)
    {
        float chance = GetSuccessChance(active.MissionScore);
        bool  success = UnityEngine.Random.value <= chance;

        MissionResult result = ApplyOutcome(
            active.Data, active.AssignedAgents,
            success, active.MissionScore, active.RewardMultiplier);

        OnMissionResolved?.Invoke(result);
    }

    private MissionResult ApplyOutcome(
        MissionData mission, List<RuntimeAgent> team,
        bool success, float score, float rewardMult)
    {
        var result = new MissionResult
        {
            Data    = mission,
            Success = success,
            Score   = score,
        };

        if (success)
        {
            // Instant missions apply chaos now; SlowBurn already applied during ticking.
            if (mission.behavior == MissionBehavior.Instant)
                gameState.AddChaos(mission.chaosOnSuccess);

            gameState.AddCure(mission.cureOnSuccess);

            int moneyReward = Mathf.RoundToInt(mission.moneyOnSuccess * rewardMult);
            gameState.AddMoney(moneyReward);
            result.ActualMoneyReward = moneyReward;

            // All agents return safely.
            foreach (var agent in team)
                agentRoster.SetAvailable(agent);
        }
        else
        {
            gameState.AddChaos(mission.chaosOnFailure);
            gameState.AddCure(mission.cureOnFailure);

            // Randomly lose agents from the team.
            int toLose      = Mathf.Min(mission.agentsLostOnFailure, team.Count);
            var lostAgents  = new List<RuntimeAgent>();
            var candidates  = new List<RuntimeAgent>(team);

            for (int i = 0; i < toLose; i++)
            {
                int idx  = UnityEngine.Random.Range(0, candidates.Count);
                var lost = candidates[idx];
                agentRoster.LoseAgent(lost);
                lostAgents.Add(lost);
                candidates.RemoveAt(idx);
            }

            // Return survivors.
            foreach (var agent in candidates)
                agentRoster.SetAvailable(agent);

            result.LostAgents       = lostAgents;
            result.ActualMoneyReward = 0;
        }

        return result;
    }
}