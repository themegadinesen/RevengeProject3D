// Assets/Scripts/Gameplay/MissionManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public class MissionManager : MonoBehaviour
{
    [SerializeField] private GameState   gameState;
    [SerializeField] private AgentRoster agentRoster;

    [Header("Base Progression (optional)")]
    [Tooltip("Leave empty if base progression is not yet set up.")]
    [SerializeField] private BaseProgressionManager baseProgression;

    [Header("Score → Success Chance")]
    [Range(0f, 1f)]
    [SerializeField] private float minSuccessChance = 0.05f;
    [Range(0f, 1f)]
    [SerializeField] private float maxSuccessChance = 0.95f;

    [Header("Score → Duration")]
    [SerializeField] private float worstDurationMult = 1.5f;
    [SerializeField] private float bestDurationMult  = 0.8f;

    [Header("Score → Rewards")]
    [SerializeField] private float worstRewardMult = 0.5f;
    [SerializeField] private float bestRewardMult  = 1.5f;

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

    public int GetActiveMissionCountForDistrict(RuntimeDistrict district)
    {
        int count = 0;
        for (int i = 0; i < activeMissions.Count; i++)
            if (activeMissions[i].District == district) count++;
        return count;
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

        if (m.requiredINT > 0) { sum += Mathf.Clamp01((float)tINT / m.requiredINT); count++; }
        if (m.requiredSTR > 0) { sum += Mathf.Clamp01((float)tSTR / m.requiredSTR); count++; }
        if (m.requiredAGI > 0) { sum += Mathf.Clamp01((float)tAGI / m.requiredAGI); count++; }

        return count > 0 ? sum / count : 1f;
    }

    public float GetSuccessChance(float score)
    {
        float baseChance = Mathf.Lerp(minSuccessChance, maxSuccessChance, score);
        float bonus = baseProgression != null ? baseProgression.TotalMissionSuccessBonus : 0f;
        return Mathf.Clamp01(baseChance + bonus);
    }

    public float GetDurationMultiplier(float score) =>
        Mathf.Lerp(worstDurationMult, bestDurationMult, score);

    public float GetRewardMultiplier(float score)
    {
        float baseMult = Mathf.Lerp(worstRewardMult, bestRewardMult, score);
        float bonus = baseProgression != null ? baseProgression.TotalMoneyGainBonus : 0f;
        return baseMult + bonus;
    }

    // ── Launch (backward-compatible overload) ─────────────────────────
    public bool TryLaunchMission(MissionData mission, List<RuntimeAgent> team)
        => TryLaunchMission(mission, team, null);

    public bool TryLaunchMission(
        MissionData mission, List<RuntimeAgent> team, RuntimeDistrict district)
    {
        if (mission == null || team == null || team.Count == 0) return false;
        if (gameState.IsRunEnded)     return false;
        if (IsMissionActive(mission)) return false;
        if (gameState.Money < mission.moneyCost) return false;

        // Domain / building gate.
        if (baseProgression != null && !baseProgression.AreMissionRequirementsMet(mission))
            return false;

        foreach (var agent in team)
            if (agent.Status != AgentStatus.Available) return false;

        // Deduct money.
        gameState.AddMoney(-mission.moneyCost);

        // Mark agents busy.
        foreach (var agent in team)
            agentRoster.SetBusy(agent);

        // Compute score.
        float score   = CalculateMissionScore(team, mission);
        float rewMult = GetRewardMultiplier(score);

        // Instant resolve.
        if (mission.duration <= 0f)
        {
            ResolveInstant(mission, team, score, rewMult, district);
            return true;
        }

        float durMult           = GetDurationMultiplier(score);
        float effectiveDuration = mission.duration * durMult;

        var active = new ActiveMission
        {
            Data                 = mission,
            District             = district,
            Duration             = effectiveDuration,
            TimeRemaining        = effectiveDuration,
            AssignedAgents       = new List<RuntimeAgent>(team),
            MissionScore         = score,
            RewardMultiplier     = rewMult,
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

            // Slow-burn: drip chaos to the district (or global fallback).
            if (m.Data.behavior == MissionBehavior.SlowBurn)
            {
                float totalChaos = m.Data.chaosOnSuccess;
                if (totalChaos > 0f && m.SlowBurnChaosApplied < totalChaos)
                {
                    float tick = (totalChaos / m.Duration) * Time.deltaTime;
                    tick = Mathf.Min(tick, totalChaos - m.SlowBurnChaosApplied);

                    if (m.District != null)
                        m.District.AddChaos(tick);
                    else
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
        float score, float rewardMult, RuntimeDistrict district)
    {
        float chance  = GetSuccessChance(score);
        bool  success = UnityEngine.Random.value <= chance;

        MissionResult result = ApplyOutcome(
            mission, team, success, score, rewardMult, district);
        OnMissionResolved?.Invoke(result);
    }

    private void ResolveTimed(ActiveMission active)
    {
        float chance  = GetSuccessChance(active.MissionScore);
        bool  success = UnityEngine.Random.value <= chance;

        MissionResult result = ApplyOutcome(
            active.Data, active.AssignedAgents,
            success, active.MissionScore, active.RewardMultiplier,
            active.District);
        OnMissionResolved?.Invoke(result);
    }

    private MissionResult ApplyOutcome(
        MissionData mission, List<RuntimeAgent> team,
        bool success, float score, float rewardMult,
        RuntimeDistrict district)
    {
        var result = new MissionResult
        {
            Data     = mission,
            District = district,
            Success  = success,
            Score    = score,
        };

        if (success)
        {
            // Chaos → district (or global fallback). SlowBurn already dripped.
            if (mission.behavior == MissionBehavior.Instant)
            {
                if (district != null) district.AddChaos(mission.chaosOnSuccess);
                else                  gameState.AddChaos(mission.chaosOnSuccess);
            }

            // Cure → district (or global fallback).
            if (district != null) district.AddCure(mission.cureOnSuccess);
            else                  gameState.AddCure(mission.cureOnSuccess);

            // Money is always global.
            int moneyReward = Mathf.RoundToInt(mission.moneyOnSuccess * rewardMult);
            gameState.AddMoney(moneyReward);
            result.ActualMoneyReward = moneyReward;

            foreach (var agent in team)
                agentRoster.SetAvailable(agent);
        }
        else
        {
            // Failure effects → district (or global fallback).
            if (district != null)
            {
                district.AddChaos(mission.chaosOnFailure);
                district.AddCure(mission.cureOnFailure);
            }
            else
            {
                gameState.AddChaos(mission.chaosOnFailure);
                gameState.AddCure(mission.cureOnFailure);
            }

            // Return surviving agents, randomly lose some.
            var lostAgents = new List<RuntimeAgent>();
            var teamCopy   = new List<RuntimeAgent>(team);

            for (int j = 0; j < mission.agentsLostOnFailure && teamCopy.Count > 0; j++)
            {
                int idx = UnityEngine.Random.Range(0, teamCopy.Count);
                lostAgents.Add(teamCopy[idx]);
                agentRoster.LoseAgent(teamCopy[idx]);
                teamCopy.RemoveAt(idx);
            }

            foreach (var agent in teamCopy)
                agentRoster.SetAvailable(agent);

            result.LostAgents = lostAgents;
        }

        return result;
    }
}