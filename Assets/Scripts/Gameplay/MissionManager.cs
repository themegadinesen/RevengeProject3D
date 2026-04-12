using System;
using System.Collections.Generic;
using UnityEngine;

public class MissionManager : MonoBehaviour
{
    [SerializeField] private GameState gameState;
    [SerializeField] private AgentRoster agentRoster;
    [SerializeField] private DistrictManager districtManager;

    private void Awake()
    {
        if (districtManager == null)
            districtManager = FindFirstObjectByType<DistrictManager>();
    }

    [Header("Base Progression (optional)")]
    [Tooltip("Leave empty if base progression is not yet set up.")]
    [SerializeField] private BaseProgressionManager baseProgression;

    [Header("Recruitment (optional)")]
    [Tooltip("Needed only if some missions should deliver pending recruit candidates.")]
    [SerializeField] private RecruitmentManager recruitmentManager;

    [Header("Score -> Success Chance")]
    [Range(0f, 1f)]
    [SerializeField] private float minSuccessChance = 0.05f;
    [Range(0f, 1f)]
    [SerializeField] private float maxSuccessChance = 0.95f;

    [Header("Score -> Duration")]
    [SerializeField] private float worstDurationMult = 1.5f;
    [SerializeField] private float bestDurationMult = 0.8f;

    [Header("Score -> Rewards")]
    [SerializeField] private float worstRewardMult = 0.5f;
    [SerializeField] private float bestRewardMult = 1.5f;

    public event Action<ActiveMission> OnMissionStarted;
    public event Action<MissionResult> OnMissionResolved;

    private readonly List<ActiveMission> activeMissions = new();
    public IReadOnlyList<ActiveMission> ActiveMissions => activeMissions;
    public int ActiveMissionCount => activeMissions.Count;

    private void Update()
    {
        if (gameState.IsRunEnded) return;
        TickActiveMissions();
    }

    public bool IsMissionActive(MissionData data)
    {
        return IsMissionActive(data, null);
    }

    public bool IsMissionActive(MissionData data, RuntimeDistrict district)
    {
        for (int i = 0; i < activeMissions.Count; i++)
        {
            ActiveMission activeMission = activeMissions[i];
            if (activeMission.Data != data)
                continue;

            if (activeMission.District == district)
                return true;
        }

        return false;
    }

    public ActiveMission GetActiveMission(MissionData data)
    {
        return GetActiveMission(data, null);
    }

    public ActiveMission GetActiveMission(MissionData data, RuntimeDistrict district)
    {
        for (int i = 0; i < activeMissions.Count; i++)
        {
            ActiveMission activeMission = activeMissions[i];
            if (activeMission.Data != data)
                continue;

            if (activeMission.District == district)
                return activeMission;
        }

        return null;
    }

    public int GetActiveMissionCountForDistrict(RuntimeDistrict district)
    {
        int count = 0;

        for (int i = 0; i < activeMissions.Count; i++)
        {
            if (activeMissions[i].District == district)
                count++;
        }

        return count;
    }

    public static float CalculateMissionScore(
        List<RuntimeAgent> team,
        MissionData mission)
    {
        int totalINT = 0;
        int totalSTR = 0;
        int totalAGI = 0;

        foreach (var agent in team)
        {
            totalINT += agent.INT;
            totalSTR += agent.STR;
            totalAGI += agent.AGI;
        }

        return CalculateScoreFromTotals(totalINT, totalSTR, totalAGI, mission);
    }

    public static float CalculateScoreFromTotals(
        int totalINT,
        int totalSTR,
        int totalAGI,
        MissionData mission)
    {
        float sum = 0f;
        int count = 0;

        if (mission.requiredINT > 0)
        {
            sum += Mathf.Clamp01((float)totalINT / mission.requiredINT);
            count++;
        }

        if (mission.requiredSTR > 0)
        {
            sum += Mathf.Clamp01((float)totalSTR / mission.requiredSTR);
            count++;
        }

        if (mission.requiredAGI > 0)
        {
            sum += Mathf.Clamp01((float)totalAGI / mission.requiredAGI);
            count++;
        }

        return count > 0 ? sum / count : 1f;
    }

    public float GetSuccessChance(float score)
    {
        return GetSuccessChance(score, null);
    }

    public float GetBaseSuccessChance(float score)
    {
        float baseChance = Mathf.Lerp(minSuccessChance, maxSuccessChance, score);
        float bonus = baseProgression != null ? baseProgression.TotalMissionSuccessBonus : 0f;
        return Mathf.Clamp01(baseChance + bonus);
    }

    public float GetDistrictSuccessChancePenalty(RuntimeDistrict district)
    {
        return districtManager != null
            ? districtManager.GetSuccessChancePenalty(district)
            : 0f;
    }

    public float GetSuccessChance(float score, RuntimeDistrict district)
    {
        return Mathf.Clamp01(GetBaseSuccessChance(score) - GetDistrictSuccessChancePenalty(district));
    }

    public float GetDurationMultiplier(float score)
    {
        return Mathf.Lerp(worstDurationMult, bestDurationMult, score);
    }

    public float GetRewardMultiplier(float score)
    {
        float baseMult = Mathf.Lerp(worstRewardMult, bestRewardMult, score);
        float bonus = baseProgression != null ? baseProgression.TotalMoneyGainBonus : 0f;
        return baseMult + bonus;
    }

    public float GetDistrictCureMultiplier(RuntimeDistrict district)
    {
        return districtManager != null
            ? districtManager.GetMissionCureMultiplier(district)
            : 1f;
    }

    public float GetDistrictBonusAgentLossChance(RuntimeDistrict district)
    {
        return districtManager != null
            ? districtManager.GetBonusAgentLossChance(district)
            : 0f;
    }

    public float GetDistrictInvestigationPressure(RuntimeDistrict district)
    {
        return districtManager != null
            ? districtManager.GetInvestigationPressure(district)
            : 0f;
    }

    public DistrictResponseState GetDistrictResponseState(RuntimeDistrict district)
    {
        return districtManager != null
            ? districtManager.GetResponseState(district)
            : DistrictResponseState.Calm;
    }

    public bool TryLaunchMission(MissionData mission, List<RuntimeAgent> team)
    {
        return TryLaunchMission(mission, team, null);
    }

    public bool TryLaunchMission(
        MissionData mission,
        List<RuntimeAgent> team,
        RuntimeDistrict district)
    {
        if (mission == null || team == null || team.Count == 0) return false;
        if (gameState.IsRunEnded) return false;
        if (IsMissionActive(mission, district)) return false;
        if (gameState.Money < mission.moneyCost) return false;

        if (baseProgression != null && !baseProgression.AreMissionRequirementsMet(mission))
            return false;

        foreach (var agent in team)
        {
            if (agent.Status != AgentStatus.Available)
                return false;
        }

        if (district != null && districtManager != null)
            districtManager.ApplyMissionLaunchHeat(district, GetActiveMissionCountForDistrict(district));

        gameState.AddMoney(-mission.moneyCost);

        foreach (var agent in team)
            agentRoster.SetBusy(agent);

        float score = CalculateMissionScore(team, mission);
        float rewardMult = GetRewardMultiplier(score);
        float successChance = GetSuccessChance(score, district);
        float cureMultiplier = GetDistrictCureMultiplier(district);
        float bonusLossChance = GetDistrictBonusAgentLossChance(district);
        float launchHeat = district != null ? district.LocalHeat : 0f;
        DistrictResponseState responseState = GetDistrictResponseState(district);

        if (mission.duration <= 0f)
        {
            ResolveInstant(
                mission,
                team,
                score,
                rewardMult,
                district,
                successChance,
                cureMultiplier,
                bonusLossChance,
                launchHeat,
                responseState);
            return true;
        }

        float durationMult = GetDurationMultiplier(score);
        float effectiveDuration = mission.duration * durationMult;

        var active = new ActiveMission
        {
            Data = mission,
            District = district,
            Duration = effectiveDuration,
            TimeRemaining = effectiveDuration,
            AssignedAgents = new List<RuntimeAgent>(team),
            MissionScore = score,
            RewardMultiplier = rewardMult,
            SlowBurnChaosApplied = 0f,
            SuccessChanceAtLaunch = successChance,
            DistrictCureMultiplier = cureMultiplier,
            BonusAgentLossChance = bonusLossChance,
            DistrictHeatAtLaunch = launchHeat,
            ResponseStateAtLaunch = responseState
        };

        activeMissions.Add(active);
        OnMissionStarted?.Invoke(active);
        return true;
    }

    private void TickActiveMissions()
    {
        for (int i = activeMissions.Count - 1; i >= 0; i--)
        {
            ActiveMission active = activeMissions[i];
            active.TimeRemaining -= Time.deltaTime;

            if (active.Data.behavior == MissionBehavior.SlowBurn)
            {
                float totalChaos = active.Data.chaosOnSuccess;
                if (totalChaos > 0f && active.SlowBurnChaosApplied < totalChaos)
                {
                    float tick = (totalChaos / active.Duration) * Time.deltaTime;
                    tick = Mathf.Min(tick, totalChaos - active.SlowBurnChaosApplied);

                    if (active.District != null)
                        active.District.AddChaos(tick);
                    else
                        gameState.AddChaos(tick);

                    active.SlowBurnChaosApplied += tick;
                }
            }

            if (active.IsComplete)
            {
                ResolveTimed(active);
                activeMissions.RemoveAt(i);
            }
        }
    }

    private void ResolveInstant(
        MissionData mission,
        List<RuntimeAgent> team,
        float score,
        float rewardMult,
        RuntimeDistrict district,
        float successChance,
        float districtCureMultiplier,
        float bonusAgentLossChance,
        float launchHeat,
        DistrictResponseState responseState)
    {
        bool success = UnityEngine.Random.value <= successChance;

        MissionResult result = ApplyOutcome(
            mission,
            team,
            success,
            score,
            rewardMult,
            district,
            successChance,
            districtCureMultiplier,
            bonusAgentLossChance,
            launchHeat,
            responseState);

        OnMissionResolved?.Invoke(result);
    }

    private void ResolveTimed(ActiveMission active)
    {
        bool success = UnityEngine.Random.value <= active.SuccessChanceAtLaunch;

        MissionResult result = ApplyOutcome(
            active.Data,
            active.AssignedAgents,
            success,
            active.MissionScore,
            active.RewardMultiplier,
            active.District,
            active.SuccessChanceAtLaunch,
            active.DistrictCureMultiplier,
            active.BonusAgentLossChance,
            active.DistrictHeatAtLaunch,
            active.ResponseStateAtLaunch);

        OnMissionResolved?.Invoke(result);
    }

    private MissionResult ApplyOutcome(
        MissionData mission,
        List<RuntimeAgent> team,
        bool success,
        float score,
        float rewardMult,
        RuntimeDistrict district,
        float successChance,
        float districtCureMultiplier,
        float bonusAgentLossChance,
        float launchHeat,
        DistrictResponseState responseState)
    {
        var result = new MissionResult
        {
            Data = mission,
            District = district,
            Success = success,
            Score = score,
            SuccessChance = successChance,
            DistrictCureMultiplier = districtCureMultiplier,
            BonusAgentLossChance = bonusAgentLossChance,
            DistrictHeatBeforeOutcome = district != null ? district.LocalHeat : launchHeat,
            ResponseState = responseState
        };

        if (success)
        {
            if (mission.behavior == MissionBehavior.Instant)
            {
                if (district != null)
                    district.AddChaos(mission.chaosOnSuccess);
                else
                    gameState.AddChaos(mission.chaosOnSuccess);
            }

            if (district != null && districtManager != null)
                districtManager.ApplyMissionOutcomeHeat(district, mission, true);

            if (district != null)
                district.AddCure(mission.cureOnSuccess * districtCureMultiplier);
            else
                gameState.AddCure(mission.cureOnSuccess);

            int moneyReward = Mathf.RoundToInt(mission.moneyOnSuccess * rewardMult);
            gameState.AddMoney(moneyReward);
            result.ActualMoneyReward = moneyReward;

            foreach (var agent in team)
                agentRoster.SetAvailable(agent);

            if (mission.specialReward == MissionSpecialReward.CandidateArrival)
            {
                if (recruitmentManager != null)
                {
                    result.ArrivedCandidate =
                        recruitmentManager.CreateCandidateFromMission(mission, district);
                }
                else
                {
                    Debug.LogWarning(
                        "MissionManager: Mission is set to CandidateArrival but no RecruitmentManager is assigned.",
                        this);
                }
            }
        }
        else
        {
            if (district != null && districtManager != null)
                districtManager.ApplyMissionOutcomeHeat(district, mission, false);

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

            var lostAgents = new List<RuntimeAgent>();
            var teamCopy = new List<RuntimeAgent>(team);

            for (int i = 0; i < mission.agentsLostOnFailure && teamCopy.Count > 0; i++)
            {
                int index = UnityEngine.Random.Range(0, teamCopy.Count);
                RuntimeAgent lostAgent = teamCopy[index];

                lostAgents.Add(lostAgent);
                agentRoster.LoseAgent(lostAgent);
                teamCopy.RemoveAt(index);
            }

            if (bonusAgentLossChance > 0f && teamCopy.Count > 0)
            {
                if (UnityEngine.Random.value <= bonusAgentLossChance)
                {
                    int index = UnityEngine.Random.Range(0, teamCopy.Count);
                    RuntimeAgent lostAgent = teamCopy[index];

                    lostAgents.Add(lostAgent);
                    agentRoster.LoseAgent(lostAgent);
                    teamCopy.RemoveAt(index);
                }
            }

            foreach (var agent in teamCopy)
                agentRoster.SetAvailable(agent);

            result.LostAgents = lostAgents;

            if (district != null && districtManager != null)
                districtManager.ApplyAgentLossHeat(district, lostAgents.Count);
        }

        result.DistrictHeatAfterOutcome = district != null ? district.LocalHeat : 0f;

        return result;
    }
}