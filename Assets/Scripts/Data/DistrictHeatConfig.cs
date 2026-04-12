using System;
using UnityEngine;

public enum DistrictResponseState
{
    Calm,
    Watched,
    Crackdown,
    Lockdown
}

[Serializable]
public class DistrictResponseTier
{
    public DistrictResponseState state = DistrictResponseState.Calm;
    [Min(0f)] public float heatThreshold = 0f;
    [Range(0f, 1f)] public float successChancePenalty = 0f;
    [Range(0f, 1f)] public float bonusAgentLossChance = 0f;
    [Min(0f)] public float missionCureMultiplier = 1f;
    [Min(0f)] public float investigationPressurePerSecond = 0f;
}

[CreateAssetMenu(fileName = "DistrictHeatConfig", menuName = "Gameplay/District Heat Config")]
public class DistrictHeatConfig : ScriptableObject
{
    [Header("Heat")]
    [Min(1f)] public float maxHeat = 100f;
    [Min(0f)] public float heatDecayPerSecond = 0.75f;
    [Min(0f)] public float heatPerMissionLaunch = 3f;
    [Min(0f)] public float heatPerConcurrentMission = 2f;
    [Min(0f)] public float heatPerLostAgent = 8f;

    [Header("Response Tiers")]
    public DistrictResponseTier[] responseTiers =
    {
        new DistrictResponseTier
        {
            state = DistrictResponseState.Calm,
            heatThreshold = 0f,
            successChancePenalty = 0f,
            bonusAgentLossChance = 0f,
            missionCureMultiplier = 1f,
            investigationPressurePerSecond = 0f
        },
        new DistrictResponseTier
        {
            state = DistrictResponseState.Watched,
            heatThreshold = 25f,
            successChancePenalty = 0.08f,
            bonusAgentLossChance = 0.1f,
            missionCureMultiplier = 0.9f,
            investigationPressurePerSecond = 0.15f
        },
        new DistrictResponseTier
        {
            state = DistrictResponseState.Crackdown,
            heatThreshold = 50f,
            successChancePenalty = 0.16f,
            bonusAgentLossChance = 0.2f,
            missionCureMultiplier = 0.75f,
            investigationPressurePerSecond = 0.35f
        },
        new DistrictResponseTier
        {
            state = DistrictResponseState.Lockdown,
            heatThreshold = 75f,
            successChancePenalty = 0.28f,
            bonusAgentLossChance = 0.35f,
            missionCureMultiplier = 0.55f,
            investigationPressurePerSecond = 0.75f
        }
    };

    public DistrictResponseTier GetTierForHeat(float heat)
    {
        if (responseTiers == null || responseTiers.Length == 0)
            return null;

        float clampedHeat = Mathf.Max(0f, heat);
        DistrictResponseTier bestTier = responseTiers[0];

        for (int i = 0; i < responseTiers.Length; i++)
        {
            DistrictResponseTier tier = responseTiers[i];
            if (tier == null) continue;

            if (clampedHeat >= tier.heatThreshold
                && (bestTier == null || tier.heatThreshold >= bestTier.heatThreshold))
            {
                bestTier = tier;
            }
        }

        return bestTier;
    }

    private void OnValidate()
    {
        maxHeat = Mathf.Max(1f, maxHeat);

        if (responseTiers == null || responseTiers.Length == 0)
            return;

        Array.Sort(responseTiers, CompareTiers);

        for (int i = 0; i < responseTiers.Length; i++)
        {
            DistrictResponseTier tier = responseTiers[i];
            if (tier == null) continue;

            tier.heatThreshold = Mathf.Clamp(tier.heatThreshold, 0f, maxHeat);
            tier.missionCureMultiplier = Mathf.Max(0f, tier.missionCureMultiplier);
            tier.investigationPressurePerSecond = Mathf.Max(0f, tier.investigationPressurePerSecond);
        }
    }

    private static int CompareTiers(DistrictResponseTier a, DistrictResponseTier b)
    {
        if (ReferenceEquals(a, b)) return 0;
        if (a == null) return 1;
        if (b == null) return -1;
        return a.heatThreshold.CompareTo(b.heatThreshold);
    }
}