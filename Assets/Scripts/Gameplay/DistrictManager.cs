// Assets/Scripts/Gameplay/DistrictManager.cs
using System;
using UnityEngine;

public class DistrictManager : MonoBehaviour
{
    [SerializeField] private GameState gameState;

    private bool hasLoggedMissingHeatConfig;

    private static readonly DistrictResponseTier FallbackResponseTier = new()
    {
        state = DistrictResponseState.Calm,
        heatThreshold = 0f,
        successChancePenalty = 0f,
        bonusAgentLossChance = 0f,
        missionCureMultiplier = 1f,
        investigationPressurePerSecond = 0f
    };

    [Header("Districts")]
    [Tooltip("Add all districts here. First district starts unlocked.")]
    [SerializeField] private DistrictData[] allDistricts;

    [Header("Heat")]
    [SerializeField] private DistrictHeatConfig heatConfig;

    [Header("Local Simulation Caps")]
    [SerializeField] private float maxLocalChaos = 100f;
    [SerializeField] private float maxLocalCure  = 100f;

    [Header("Local Tick Rates")]
    [Tooltip("Per-district chaos decay per second.")]
    [SerializeField] private float chaosDecayRate = 0.5f;
    [Tooltip("Per-district passive cure fill per second.")]
    [SerializeField] private float cureFillRate = 0.3f;
    [Tooltip("Per-district people growth per unit of local chaos per second.")]
    [SerializeField] private float peopleGrowthMultiplier = 0.1f;

    // ── Events ────────────────────────────────────────────────────────
    public event Action<RuntimeDistrict> OnDistrictUnlocked;
    public event Action<RuntimeDistrict> OnDistrictSelected;

    // ── Runtime ───────────────────────────────────────────────────────
    private RuntimeDistrict[] runtimeDistricts;

    public RuntimeDistrict   SelectedDistrict { get; private set; }
    public DistrictData      ActiveDistrict   => SelectedDistrict?.Data;

    public float MaxLocalChaos => maxLocalChaos;
    public float MaxLocalCure  => maxLocalCure;
    public float MaxLocalHeat  => heatConfig != null ? heatConfig.maxHeat : 100f;

    // ── Lifecycle ─────────────────────────────────────────────────────
    private void Start()
    {
        runtimeDistricts = new RuntimeDistrict[allDistricts.Length];
        for (int i = 0; i < allDistricts.Length; i++)
            runtimeDistricts[i] = new RuntimeDistrict(
                allDistricts[i], maxLocalChaos, maxLocalCure, MaxLocalHeat);

        if (runtimeDistricts.Length > 0)
        {
            runtimeDistricts[0].IsUnlocked = true;
            SelectedDistrict = runtimeDistricts[0];
        }

        if (heatConfig == null && !hasLoggedMissingHeatConfig)
        {
            Debug.LogWarning(
                "DistrictManager: Heat Config is not assigned. District heat will still store values, but response penalties and investigation pressure will stay at zero.",
                this);
            hasLoggedMissingHeatConfig = true;
        }
    }

    private void Update()
    {
        if (gameState.IsRunEnded) return;

        CheckUnlocks();
        TickAllDistricts();
        AggregateToGlobal();
    }

    // ── Public Queries ────────────────────────────────────────────────
    public RuntimeDistrict GetRuntimeDistrict(DistrictData data)
    {
        for (int i = 0; i < allDistricts.Length; i++)
            if (allDistricts[i] == data) return runtimeDistricts[i];
        return null;
    }

    public RuntimeDistrict[] GetAllRuntimeDistricts() => runtimeDistricts;

    public DistrictData[] GetAllDistricts() => allDistricts;

    public bool IsUnlocked(DistrictData data)
    {
        var rd = GetRuntimeDistrict(data);
        return rd != null && rd.IsUnlocked;
    }

    public DistrictResponseTier GetResponseTier(RuntimeDistrict district)
    {
        if (district == null || heatConfig == null)
            return FallbackResponseTier;

        DistrictResponseTier tier = heatConfig.GetTierForHeat(district.LocalHeat);
        return tier ?? FallbackResponseTier;
    }

    public DistrictResponseState GetResponseState(RuntimeDistrict district)
    {
        return GetResponseTier(district).state;
    }

    public float GetSuccessChancePenalty(RuntimeDistrict district)
    {
        return district == null ? 0f : GetResponseTier(district).successChancePenalty;
    }

    public float GetBonusAgentLossChance(RuntimeDistrict district)
    {
        return district == null ? 0f : GetResponseTier(district).bonusAgentLossChance;
    }

    public float GetMissionCureMultiplier(RuntimeDistrict district)
    {
        return district == null ? 1f : Mathf.Max(0f, GetResponseTier(district).missionCureMultiplier);
    }

    public float GetInvestigationPressure(RuntimeDistrict district)
    {
        return district == null ? 0f : GetResponseTier(district).investigationPressurePerSecond;
    }

    public void ApplyMissionLaunchHeat(RuntimeDistrict district, int concurrentMissionCount)
    {
        if (district == null || heatConfig == null) return;

        float amount = heatConfig.heatPerMissionLaunch
                     + Mathf.Max(0, concurrentMissionCount) * heatConfig.heatPerConcurrentMission;

        district.AddHeat(amount);
    }

    public void ApplyMissionOutcomeHeat(RuntimeDistrict district, MissionData mission, bool success)
    {
        if (district == null || mission == null) return;
        district.AddHeat(success ? mission.heatOnSuccess : mission.heatOnFailure);
    }

    public void ApplyAgentLossHeat(RuntimeDistrict district, int lostAgentCount)
    {
        if (district == null || heatConfig == null || lostAgentCount <= 0) return;
        district.AddHeat(lostAgentCount * heatConfig.heatPerLostAgent);
    }

    // ── Selection (called by DistrictMapInput) ────────────────────────
    public void SelectDistrict(RuntimeDistrict district)
    {
        if (district != null && !district.IsUnlocked) return;
        SelectedDistrict = district;
        OnDistrictSelected?.Invoke(district);
    }

    public void ClearSelection()
    {
        SelectedDistrict = null;
        OnDistrictSelected?.Invoke(null);
    }

    // ── Unlock Check ──────────────────────────────────────────────────
    private void CheckUnlocks()
    {
        for (int i = 0; i < runtimeDistricts.Length; i++)
        {
            RuntimeDistrict rd = runtimeDistricts[i];
            if (!rd.IsUnlocked && gameState.PeopleAffected >= rd.Data.peopleAffectedUnlockThreshold)
            {
                rd.IsUnlocked = true;
                OnDistrictUnlocked?.Invoke(rd);
            }
        }
    }

    // ── Per-District Tick ─────────────────────────────────────────────
    private void TickAllDistricts()
    {
        float dt = Time.deltaTime;
        float heatDecayRate = heatConfig != null ? heatConfig.heatDecayPerSecond : 0f;

        for (int i = 0; i < runtimeDistricts.Length; i++)
        {
            RuntimeDistrict rd = runtimeDistricts[i];
            if (!rd.IsUnlocked) continue;

            if (rd.LocalHeat > 0f && heatDecayRate > 0f)
                rd.LocalHeat = Mathf.Max(0f, rd.LocalHeat - heatDecayRate * dt);

            // Chaos decays locally.
            if (rd.LocalChaos > 0f)
                rd.LocalChaos = Mathf.Max(0f, rd.LocalChaos - chaosDecayRate * dt);

            // Cure fills locally.
            float cureRate = cureFillRate + GetInvestigationPressure(rd);
            if (rd.LocalCure < maxLocalCure && cureRate > 0f)
                rd.LocalCure = Mathf.Min(maxLocalCure, rd.LocalCure + cureRate * dt);

            // People grow based on local chaos.
            float growth = rd.LocalChaos * peopleGrowthMultiplier * dt;
            if (growth > 0f)
                rd.LocalPeopleAffected += Mathf.CeilToInt(growth);
        }
    }

    // ── Aggregation → GameState ───────────────────────────────────────
    private void AggregateToGlobal()
    {
        float weightedChaos = 0f;
        float weightedCure  = 0f;
        float totalWeight   = 0f;
        int   totalPeople   = 0;

        for (int i = 0; i < runtimeDistricts.Length; i++)
        {
            RuntimeDistrict rd = runtimeDistricts[i];
            if (!rd.IsUnlocked) continue;

            float w = rd.Data.populationWeight;
            weightedChaos += rd.LocalChaos * w;
            weightedCure  += rd.LocalCure  * w;
            totalWeight   += w;
            totalPeople   += rd.LocalPeopleAffected;
        }

        float globalChaos = totalWeight > 0f ? weightedChaos / totalWeight : 0f;
        float globalCure  = totalWeight > 0f ? weightedCure  / totalWeight : 0f;

        gameState.SetDistrictValues(globalChaos, globalCure, totalPeople);
    }
}