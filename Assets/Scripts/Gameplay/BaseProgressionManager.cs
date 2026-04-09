// Assets/Scripts/Gameplay/BaseProgressionManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public class BaseProgressionManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameState gameState;

    [Header("Buildings")]
    [Tooltip("All buildings in the game, in display order.")]
    [SerializeField] private BuildingData[] allBuildings;

    [Header("Starting State")]
    [Tooltip("Buildings that start as Built (e.g. HQ). These skip Locked/Available/Constructing.")]
    [SerializeField] private BuildingData[] startingBuiltBuildings;

    [Tooltip("When false, the entire progression system is dormant (Act 1). Set true when Act 2 begins.")]
    [SerializeField] private bool progressionActive = false;

    // ── Events ────────────────────────────────────────────────────────
    public event Action<RuntimeBuilding> OnBuildingStateChanged;
    public event Action<InfrastructureDomain> OnDomainUnlocked;

    // ── Runtime ───────────────────────────────────────────────────────
    private RuntimeBuilding[] runtimeBuildings;
    private readonly HashSet<InfrastructureDomain> unlockedDomains = new();

    // ── Cached Bonuses ────────────────────────────────────────────────
    public int   TotalAgentCapacityBonus { get; private set; }
    public float TotalMissionSuccessBonus { get; private set; }
    public float TotalMoneyGainBonus { get; private set; }

    public bool IsProgressionActive => progressionActive;

    // ── Lifecycle ─────────────────────────────────────────────────────
    private void Start()
    {
        InitBuildings();
    }

    private void Update()
    {
        if (!progressionActive) return;
        if (gameState.IsRunEnded) return;

        UpdatePrerequisites();
        TickConstruction();
    }

    // ── Initialization ────────────────────────────────────────────────
    private void InitBuildings()
    {
        runtimeBuildings = new RuntimeBuilding[allBuildings.Length];

        for (int i = 0; i < allBuildings.Length; i++)
            runtimeBuildings[i] = new RuntimeBuilding(allBuildings[i]);

        // Mark starting buildings as Built.
        if (startingBuiltBuildings != null)
        {
            foreach (var startBuilt in startingBuiltBuildings)
            {
                RuntimeBuilding rb = GetRuntimeBuilding(startBuilt);
                if (rb != null)
                {
                    rb.State = BuildingState.Built;
                    rb.BuildTimeRemaining = 0f;
                    RegisterBuiltBuilding(rb);
                }
            }
        }

        // Initial prerequisite check.
        UpdatePrerequisites();
    }

    // ── Prerequisite Check ────────────────────────────────────────────
    private void UpdatePrerequisites()
    {
        for (int i = 0; i < runtimeBuildings.Length; i++)
        {
            RuntimeBuilding rb = runtimeBuildings[i];
            if (rb.State != BuildingState.Locked) continue;

            if (ArePrerequisitesMet(rb.Data))
            {
                rb.State = BuildingState.Available;
                OnBuildingStateChanged?.Invoke(rb);
            }
        }
    }

    private bool ArePrerequisitesMet(BuildingData data)
    {
        if (data.prerequisites == null || data.prerequisites.Length == 0)
            return true;

        foreach (var prereq in data.prerequisites)
        {
            RuntimeBuilding rb = GetRuntimeBuilding(prereq);
            if (rb == null || rb.State != BuildingState.Built)
                return false;
        }
        return true;
    }

    // ── Construction ──────────────────────────────────────────────────
    private void TickConstruction()
    {
        for (int i = 0; i < runtimeBuildings.Length; i++)
        {
            RuntimeBuilding rb = runtimeBuildings[i];
            if (rb.State != BuildingState.Constructing) continue;

            rb.BuildTimeRemaining -= Time.deltaTime;

            if (rb.BuildTimeRemaining <= 0f)
            {
                rb.BuildTimeRemaining = 0f;
                rb.State = BuildingState.Built;
                RegisterBuiltBuilding(rb);
                OnBuildingStateChanged?.Invoke(rb);
            }
        }
    }

    /// <summary>
    /// Called when a building finishes: registers domain unlock and recalculates bonuses.
    /// </summary>
    private void RegisterBuiltBuilding(RuntimeBuilding rb)
    {
        // Domain unlock.
        if (rb.Data.unlocksDomain != InfrastructureDomain.None)
        {
            if (unlockedDomains.Add(rb.Data.unlocksDomain))
                OnDomainUnlocked?.Invoke(rb.Data.unlocksDomain);
        }

        RecalculateBonuses();
    }

    private void RecalculateBonuses()
    {
        int   cap   = 0;
        float succ  = 0f;
        float money = 0f;

        for (int i = 0; i < runtimeBuildings.Length; i++)
        {
            if (runtimeBuildings[i].State != BuildingState.Built) continue;
            BuildingData d = runtimeBuildings[i].Data;
            cap   += d.agentCapacityBonus;
            succ  += d.missionSuccessBonus;
            money += d.moneyGainBonus;
        }

        TotalAgentCapacityBonus  = cap;
        TotalMissionSuccessBonus = succ;
        TotalMoneyGainBonus      = money;
    }

    // ── Public: Start Building ────────────────────────────────────────
    /// <summary>
    /// Attempt to start constructing a building. Deducts money, sets state.
    /// Returns true if construction started.
    /// </summary>
    public bool TryStartConstruction(BuildingData data)
    {
        if (!progressionActive) return false;
        if (gameState.IsRunEnded) return false;

        RuntimeBuilding rb = GetRuntimeBuilding(data);
        if (rb == null) return false;
        if (rb.State != BuildingState.Available) return false;
        if (gameState.Money < data.moneyCost) return false;

        gameState.AddMoney(-data.moneyCost);

        if (data.buildTime <= 0f)
        {
            // Instant build.
            rb.State = BuildingState.Built;
            rb.BuildTimeRemaining = 0f;
            RegisterBuiltBuilding(rb);
        }
        else
        {
            rb.State = BuildingState.Constructing;
            rb.BuildTimeRemaining = data.buildTime;
        }

        OnBuildingStateChanged?.Invoke(rb);
        return true;
    }

    // ── Public: Queries ───────────────────────────────────────────────

    public bool IsDomainUnlocked(InfrastructureDomain domain)
    {
        if (domain == InfrastructureDomain.None) return true;
        return unlockedDomains.Contains(domain);
    }

    public bool IsBuildingBuilt(BuildingData data)
    {
        if (data == null) return true; // no requirement
        RuntimeBuilding rb = GetRuntimeBuilding(data);
        return rb != null && rb.State == BuildingState.Built;
    }

    /// <summary>
    /// Can this mission be launched based on domain/building requirements?
    /// </summary>
    public bool AreMissionRequirementsMet(MissionData mission)
    {
        if (!IsDomainUnlocked(mission.requiredDomain)) return false;
        if (!IsBuildingBuilt(mission.requiredBuilding)) return false;
        return true;
    }

    public RuntimeBuilding GetRuntimeBuilding(BuildingData data)
    {
        if (data == null) return null;
        for (int i = 0; i < allBuildings.Length; i++)
            if (allBuildings[i] == data) return runtimeBuildings[i];
        return null;
    }

    public RuntimeBuilding[] GetAllRuntimeBuildings() => runtimeBuildings;

    public BuildingData[] GetAllBuildings() => allBuildings;

    // ── Act Flow ──────────────────────────────────────────────────────
    /// <summary>
    /// Call this when transitioning to Act 2 (e.g. from ActSwitcher).
    /// Activates the progression system.
    /// </summary>
    public void ActivateProgression()
    {
        progressionActive = true;
        UpdatePrerequisites();
    }
}