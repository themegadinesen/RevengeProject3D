// Assets/Scripts/Gameplay/ActiveMission.cs
using System.Collections.Generic;

/// <summary>
/// Runtime data for a mission currently in progress.
/// Plain C# class — not a MonoBehaviour or ScriptableObject.
/// </summary>
[System.Serializable]
public class ActiveMission
{
    public MissionData      Data;
    public RuntimeDistrict  District;               // which district this runs in (null = global fallback)
    public float            Duration;               // effective (after score multiplier)
    public float            TimeRemaining;
    public List<RuntimeAgent> AssignedAgents;
    public float            MissionScore;           // 0–1
    public float            RewardMultiplier;       // computed at launch
    public float            SlowBurnChaosApplied;
    public float            SuccessChanceAtLaunch;
    public float            DistrictCureMultiplier;
    public float            BonusAgentLossChance;
    public float            DistrictHeatAtLaunch;
    public DistrictResponseState ResponseStateAtLaunch;

    /// <summary>0 → 1 progress.</summary>
    public float Progress => Duration > 0f ? 1f - (TimeRemaining / Duration) : 1f;

    /// <summary>True when the timer has expired.</summary>
    public bool IsComplete => TimeRemaining <= 0f;

    /// <summary>Number of agents on this mission.</summary>
    public int AssignedCount => AssignedAgents != null ? AssignedAgents.Count : 0;
}