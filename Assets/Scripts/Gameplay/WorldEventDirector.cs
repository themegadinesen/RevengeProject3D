using System;
using System.Collections.Generic;
using UnityEngine;

public class WorldEventDirector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameState gameState;
    [SerializeField] private GameCalendar gameCalendar;
    [SerializeField] private DistrictManager districtManager;
    [SerializeField] private MissionManager missionManager;
    [SerializeField] private AgentRoster agentRoster;
    [SerializeField] private RecruitmentManager recruitmentManager;
    [SerializeField] private BaseProgressionManager baseProgression;

    [Header("Scheduled Events")]
    [SerializeField] private ScheduledWorldEventDefinition[] scheduledEvents;

    public event Action<WorldEventContext> OnFlavorEvent;
    public event Action<WorldEventContext> OnImpactEventStarted;
    public event Action<WorldEventContext, ImpactEventChoice> OnImpactEventResolved;
    public event Action OnImpactEventCancelled;

    private readonly Dictionary<FlavorEventDefinition, float> lastFlavorTimes = new();
    private readonly Dictionary<ImpactEventDefinition, float> lastImpactTimes = new();
    private readonly HashSet<ScheduledWorldEventDefinition> firedScheduledEvents = new();

    private WorldEventContext pendingImpactContext;
    private bool subscribedToCalendar;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToCalendar();
    }

    private void OnDisable()
    {
        UnsubscribeFromCalendar();
    }

    private void Start()
    {
        ResolveReferences();
        SubscribeToCalendar();
        CheckScheduledEvents();
    }

    private void Update()
    {
        if (gameState != null && gameState.IsRunEnded)
        {
            CancelPendingImpact();
            return;
        }

        // Scripted events are fired by the calendar or by explicit trigger calls.
    }

    private void OnCalendarChanged(GameCalendar changedCalendar)
    {
        CheckScheduledEvents();
    }

    private void CheckScheduledEvents()
    {
        if (gameState != null && gameState.IsRunEnded)
            return;

        if (gameCalendar == null || scheduledEvents == null)
            return;

        for (int i = 0; i < scheduledEvents.Length; i++)
        {
            ScheduledWorldEventDefinition scheduledEvent = scheduledEvents[i];
            if (scheduledEvent == null || firedScheduledEvents.Contains(scheduledEvent))
                continue;

            if (!scheduledEvent.IsDue(gameCalendar))
                continue;

            if (TryFireScheduledEvent(scheduledEvent))
                firedScheduledEvents.Add(scheduledEvent);
        }
    }

    private bool TryFireScheduledEvent(ScheduledWorldEventDefinition scheduledEvent)
    {
        if (scheduledEvent == null)
            return false;

        switch (scheduledEvent.eventType)
        {
            case ScheduledWorldEventType.Flavor:
                return TryEmitScheduledFlavorEvent(scheduledEvent);

            case ScheduledWorldEventType.Impact:
                return TryStartScheduledImpactEvent(scheduledEvent);

            default:
                return false;
        }
    }

    public bool CanApplyImpactChoice(int choiceIndex)
    {
        if (pendingImpactContext == null || pendingImpactContext.ImpactDefinition == null)
            return false;

        ImpactEventChoice[] choices = pendingImpactContext.ImpactDefinition.choices;
        if (choices == null || choiceIndex < 0 || choiceIndex >= choices.Length)
            return false;

        ImpactEventChoice choice = choices[choiceIndex];
        return choice != null && CanApplyConsequences(choice.consequences);
    }

    public bool TryResolveImpactChoice(int choiceIndex)
    {
        if (pendingImpactContext == null || pendingImpactContext.ImpactDefinition == null)
            return false;

        ImpactEventChoice[] choices = pendingImpactContext.ImpactDefinition.choices;
        if (choices == null || choiceIndex < 0 || choiceIndex >= choices.Length)
            return false;

        ImpactEventChoice choice = choices[choiceIndex];
        if (choice == null)
            return false;

        if (!CanApplyConsequences(choice.consequences))
            return false;

        WorldEventContext resolvedContext = pendingImpactContext;
        ApplyConsequences(choice.consequences, resolvedContext);
        pendingImpactContext = null;

        OnImpactEventResolved?.Invoke(resolvedContext, choice);
        return true;
    }

    public bool TryTriggerFlavorEvent(
        FlavorEventDefinition definition,
        bool ignoreCooldown = false)
    {
        return TryTriggerFlavorEvent(definition, null, ignoreCooldown);
    }

    public bool TryTriggerFlavorEvent(
        FlavorEventDefinition definition,
        DistrictData targetDistrict,
        bool ignoreCooldown = false)
    {
        RuntimeDistrict runtimeDistrict = GetRuntimeDistrict(targetDistrict);
        if (targetDistrict != null && runtimeDistrict == null)
            return false;

        if (!TryBuildFlavorContext(
                definition,
                out WorldEventContext context,
                ignoreCooldown,
                runtimeDistrict))
        {
            return false;
        }

        lastFlavorTimes[definition] = Time.time;
        OnFlavorEvent?.Invoke(context);
        return true;
    }

    private bool TryEmitScheduledFlavorEvent(ScheduledWorldEventDefinition scheduledEvent)
    {
        if (scheduledEvent == null || scheduledEvent.flavorEvent == null)
            return false;

        return TryTriggerFlavorEvent(
            scheduledEvent.flavorEvent,
            scheduledEvent.targetDistrict,
            scheduledEvent.ignoreDefinitionCooldown);
    }

    public bool TryTriggerImpactEvent(
        ImpactEventDefinition definition,
        bool ignoreCooldown = false)
    {
        return TryTriggerImpactEvent(definition, (DistrictData)null, ignoreCooldown);
    }

    public bool TryTriggerImpactEvent(
        ImpactEventDefinition definition,
        DistrictData targetDistrict,
        bool ignoreCooldown = false)
    {
        RuntimeDistrict runtimeDistrict = GetRuntimeDistrict(targetDistrict);
        if (targetDistrict != null && runtimeDistrict == null)
            return false;

        return TryTriggerImpactEvent(definition, runtimeDistrict, ignoreCooldown);
    }

    public bool TryTriggerImpactEvent(
        ImpactEventDefinition definition,
        RuntimeDistrict targetDistrict,
        bool ignoreCooldown = false)
    {
        if (pendingImpactContext != null)
            return false;

        if (OnImpactEventStarted == null)
            return false;

        if (!TryBuildImpactContext(
                definition,
                out WorldEventContext context,
                ignoreCooldown,
                targetDistrict))
        {
            return false;
        }

        lastImpactTimes[definition] = Time.time;
        pendingImpactContext = context;
        OnImpactEventStarted?.Invoke(context);
        return true;
    }

    private bool TryStartScheduledImpactEvent(ScheduledWorldEventDefinition scheduledEvent)
    {
        if (scheduledEvent == null || scheduledEvent.impactEvent == null)
            return false;

        return TryTriggerImpactEvent(
            scheduledEvent.impactEvent,
            scheduledEvent.targetDistrict,
            scheduledEvent.ignoreDefinitionCooldown);
    }

    private bool TryBuildFlavorContext(
        FlavorEventDefinition definition,
        out WorldEventContext context,
        bool ignoreCooldown = false,
        RuntimeDistrict forcedDistrict = null)
    {
        context = null;

        if (definition == null)
            return false;

        if (!ignoreCooldown && IsCoolingDown(lastFlavorTimes, definition, definition.cooldownSeconds))
            return false;

        if (!TryBuildCommonContext(
                definition.targetMode,
                definition.conditions,
                forcedDistrict,
                out RuntimeDistrict targetDistrict,
                out ActiveMission activeMission,
                out RuntimeAgent targetAgent))
        {
            return false;
        }

        context = new WorldEventContext
        {
            FlavorDefinition = definition,
            TargetDistrict = targetDistrict,
            ActiveMission = activeMission,
            TargetAgent = targetAgent
        };
        context.Message = RenderCopy(definition.message, context);
        return true;
    }

    private bool TryBuildImpactContext(
        ImpactEventDefinition definition,
        out WorldEventContext context,
        bool ignoreCooldown = false,
        RuntimeDistrict forcedDistrict = null)
    {
        context = null;

        if (definition == null)
            return false;

        if (definition.choices == null || definition.choices.Length == 0)
            return false;

        if (!HasAnyApplicableChoice(definition.choices))
            return false;

        if (!ignoreCooldown && IsCoolingDown(lastImpactTimes, definition, definition.cooldownSeconds))
            return false;

        WorldEventTargetMode targetMode = definition.targetMode;
        if (targetMode == WorldEventTargetMode.None && ImpactChoicesNeedDistrict(definition.choices))
            targetMode = WorldEventTargetMode.FirstUnlockedDistrict;

        if (!TryBuildCommonContext(
                targetMode,
                definition.conditions,
                forcedDistrict,
                out RuntimeDistrict targetDistrict,
                out ActiveMission activeMission,
                out RuntimeAgent targetAgent))
        {
            return false;
        }

        context = new WorldEventContext
        {
            ImpactDefinition = definition,
            TargetDistrict = targetDistrict,
            ActiveMission = activeMission,
            TargetAgent = targetAgent
        };
        context.Title = RenderCopy(definition.eventTitle, context);
        context.Message = RenderCopy(definition.description, context);
        return true;
    }

    private bool TryBuildCommonContext(
        WorldEventTargetMode targetMode,
        WorldEventConditionSet conditions,
        RuntimeDistrict forcedDistrict,
        out RuntimeDistrict targetDistrict,
        out ActiveMission activeMission,
        out RuntimeAgent targetAgent)
    {
        targetDistrict = null;
        activeMission = null;
        targetAgent = null;

        if (!AreRunConditionsMet(conditions))
            return false;

        bool needsActiveMission = conditions != null && conditions.requiresActiveMission;
        if (needsActiveMission || targetMode == WorldEventTargetMode.ActiveMissionDistrict)
        {
            activeMission = PickFirstActiveMission();
            if (activeMission == null)
                return false;
        }

        bool needsAgent = conditions != null && conditions.requiresAvailableAgent;
        if (needsAgent)
        {
            targetAgent = PickFirstAvailableAgent();
            if (targetAgent == null)
                return false;
        }

        if (forcedDistrict != null)
        {
            targetDistrict = forcedDistrict;
            return targetDistrict.IsUnlocked &&
                   AreDistrictConditionsMet(targetDistrict, conditions);
        }

        WorldEventTargetMode effectiveTargetMode = targetMode;
        if (effectiveTargetMode == WorldEventTargetMode.None && ConditionsNeedDistrict(conditions))
            effectiveTargetMode = WorldEventTargetMode.FirstUnlockedDistrict;

        if (effectiveTargetMode == WorldEventTargetMode.ActiveMissionDistrict)
        {
            targetDistrict = activeMission != null ? activeMission.District : null;
            return targetDistrict != null && AreDistrictConditionsMet(targetDistrict, conditions);
        }

        if (effectiveTargetMode == WorldEventTargetMode.SelectedDistrict)
        {
            targetDistrict = districtManager != null ? districtManager.SelectedDistrict : null;
            return targetDistrict != null &&
                   targetDistrict.IsUnlocked &&
                   AreDistrictConditionsMet(targetDistrict, conditions);
        }

        if (effectiveTargetMode != WorldEventTargetMode.None)
        {
            targetDistrict = PickDistrict(effectiveTargetMode, conditions);
            return targetDistrict != null;
        }

        return AreAnyDistrictConditionsMetIfNeeded(conditions);
    }

    private bool AreRunConditionsMet(WorldEventConditionSet conditions)
    {
        if (conditions == null)
            return true;

        bool act2 = baseProgression != null && baseProgression.IsProgressionActive;
        if (conditions.actRequirement == WorldEventActRequirement.Act1 && act2)
            return false;

        if (conditions.actRequirement == WorldEventActRequirement.Act2 && !act2)
            return false;

        if (conditions.minimumChaos > 0f)
        {
            if (gameState == null || gameState.Chaos < conditions.minimumChaos)
                return false;
        }

        if (conditions.minimumCure > 0f)
        {
            if (gameState == null || gameState.Cure < conditions.minimumCure)
                return false;
        }

        if (conditions.minimumLostAgents > 0)
        {
            if (agentRoster == null || agentRoster.LostCount < conditions.minimumLostAgents)
                return false;
        }

        if (conditions.requiresActiveMission && GetActiveMissionCount() <= 0)
            return false;

        if (conditions.requiresAvailableAgent && GetAvailableAgentCount() <= 0)
            return false;

        return true;
    }

    private bool AreAnyDistrictConditionsMetIfNeeded(WorldEventConditionSet conditions)
    {
        if (!ConditionsNeedDistrict(conditions))
            return true;

        RuntimeDistrict[] districts = GetRuntimeDistricts();
        if (districts == null)
            return false;

        for (int i = 0; i < districts.Length; i++)
        {
            RuntimeDistrict district = districts[i];
            if (district != null &&
                district.IsUnlocked &&
                AreDistrictConditionsMet(district, conditions))
            {
                return true;
            }
        }

        return false;
    }

    private RuntimeDistrict PickDistrict(
        WorldEventTargetMode targetMode,
        WorldEventConditionSet conditions)
    {
        RuntimeDistrict[] districts = GetRuntimeDistricts();
        if (districts == null)
            return null;

        if (targetMode == WorldEventTargetMode.FirstUnlockedDistrict)
            return PickFirstDistrict(districts, conditions);

        RuntimeDistrict best = null;
        float bestValue = float.MinValue;

        for (int i = 0; i < districts.Length; i++)
        {
            RuntimeDistrict district = districts[i];
            if (district == null || !district.IsUnlocked)
                continue;

            if (!AreDistrictConditionsMet(district, conditions))
                continue;

            float value = GetDistrictTargetValue(district, targetMode);
            if (best == null || value > bestValue)
            {
                best = district;
                bestValue = value;
            }
        }

        return best;
    }

    private RuntimeDistrict PickFirstDistrict(
        RuntimeDistrict[] districts,
        WorldEventConditionSet conditions)
    {
        for (int i = 0; i < districts.Length; i++)
        {
            RuntimeDistrict district = districts[i];
            if (district != null &&
                district.IsUnlocked &&
                AreDistrictConditionsMet(district, conditions))
            {
                return district;
            }
        }

        return null;
    }

    private static float GetDistrictTargetValue(
        RuntimeDistrict district,
        WorldEventTargetMode targetMode)
    {
        switch (targetMode)
        {
            case WorldEventTargetMode.HighestHeatDistrict:
                return district.LocalHeat;

            case WorldEventTargetMode.HighestChaosDistrict:
                return district.LocalChaos;

            case WorldEventTargetMode.HighestCureDistrict:
                return district.LocalCure;

            default:
                return 0f;
        }
    }

    private static bool AreDistrictConditionsMet(
        RuntimeDistrict district,
        WorldEventConditionSet conditions)
    {
        if (district == null)
            return false;

        if (conditions == null)
            return true;

        if (conditions.minimumDistrictHeat > 0f &&
            district.LocalHeat < conditions.minimumDistrictHeat)
        {
            return false;
        }

        if (conditions.minimumDistrictChaos > 0f &&
            district.LocalChaos < conditions.minimumDistrictChaos)
        {
            return false;
        }

        if (conditions.minimumDistrictCure > 0f &&
            district.LocalCure < conditions.minimumDistrictCure)
        {
            return false;
        }

        return true;
    }

    private bool CanApplyConsequences(WorldEventConsequenceSet consequences)
    {
        if (consequences == null)
            return true;

        if (consequences.moneyDelta < 0 &&
            consequences.requireMoneyForNegativeDelta &&
            gameState != null &&
            gameState.Money + consequences.moneyDelta < 0)
        {
            return false;
        }

        if (consequences.loseFirstAvailableAgent && GetAvailableAgentCount() <= 0)
            return false;

        if (consequences.candidateArrivalMission != null && recruitmentManager == null)
            return false;

        return true;
    }

    private bool HasAnyApplicableChoice(ImpactEventChoice[] choices)
    {
        if (choices == null)
            return false;

        for (int i = 0; i < choices.Length; i++)
        {
            if (choices[i] != null && CanApplyConsequences(choices[i].consequences))
                return true;
        }

        return false;
    }

    private void ApplyConsequences(
        WorldEventConsequenceSet consequences,
        WorldEventContext context)
    {
        if (consequences == null)
            return;

        if (gameState != null && consequences.moneyDelta != 0)
            gameState.AddMoney(consequences.moneyDelta);

        ApplyGlobalChaos(consequences.globalChaosDelta);
        ApplyGlobalCure(consequences.globalCureDelta);

        RuntimeDistrict district = context != null ? context.TargetDistrict : null;
        if (district != null)
        {
            if (consequences.districtChaosDelta != 0f)
                district.AddChaos(consequences.districtChaosDelta);

            if (consequences.districtCureDelta != 0f)
                district.AddCure(consequences.districtCureDelta);

            if (consequences.districtHeatDelta != 0f)
                district.AddHeat(consequences.districtHeatDelta);
        }

        if (consequences.loseFirstAvailableAgent && agentRoster != null)
        {
            RuntimeAgent agent = PickFirstAvailableAgent();
            if (agent != null)
                agentRoster.LoseAgent(agent);
        }

        if (consequences.candidateArrivalMission != null && recruitmentManager != null)
            recruitmentManager.CreateCandidateFromMission(consequences.candidateArrivalMission, district);
    }

    private void ApplyGlobalChaos(float amount)
    {
        if (Mathf.Approximately(amount, 0f))
            return;

        if (gameState != null && gameState.IsDistrictDriven && ApplyChaosToUnlockedDistricts(amount))
            return;

        if (gameState != null)
            gameState.AddChaos(amount);
    }

    private void ApplyGlobalCure(float amount)
    {
        if (Mathf.Approximately(amount, 0f))
            return;

        if (gameState != null && gameState.IsDistrictDriven && ApplyCureToUnlockedDistricts(amount))
            return;

        if (gameState != null)
            gameState.AddCure(amount);
    }

    private bool ApplyChaosToUnlockedDistricts(float amount)
    {
        RuntimeDistrict[] districts = GetRuntimeDistricts();
        if (districts == null)
            return false;

        bool applied = false;
        for (int i = 0; i < districts.Length; i++)
        {
            RuntimeDistrict district = districts[i];
            if (district == null || !district.IsUnlocked)
                continue;

            district.AddChaos(amount);
            applied = true;
        }

        return applied;
    }

    private bool ApplyCureToUnlockedDistricts(float amount)
    {
        RuntimeDistrict[] districts = GetRuntimeDistricts();
        if (districts == null)
            return false;

        bool applied = false;
        for (int i = 0; i < districts.Length; i++)
        {
            RuntimeDistrict district = districts[i];
            if (district == null || !district.IsUnlocked)
                continue;

            district.AddCure(amount);
            applied = true;
        }

        return applied;
    }

    private ActiveMission PickFirstActiveMission()
    {
        if (missionManager == null || missionManager.ActiveMissionCount <= 0)
            return null;

        IReadOnlyList<ActiveMission> activeMissions = missionManager.ActiveMissions;
        if (activeMissions == null || activeMissions.Count <= 0)
            return null;

        return activeMissions[0];
    }

    private RuntimeAgent PickFirstAvailableAgent()
    {
        if (agentRoster == null || agentRoster.AvailableCount <= 0)
            return null;

        List<RuntimeAgent> agents = agentRoster.GetAvailableAgents();
        if (agents == null || agents.Count <= 0)
            return null;

        return agents[0];
    }

    private RuntimeDistrict GetRuntimeDistrict(DistrictData districtData)
    {
        if (districtData == null || districtManager == null)
            return null;

        return districtManager.GetRuntimeDistrict(districtData);
    }

    private RuntimeDistrict[] GetRuntimeDistricts()
    {
        return districtManager != null ? districtManager.GetAllRuntimeDistricts() : null;
    }

    private int GetActiveMissionCount()
    {
        return missionManager != null ? missionManager.ActiveMissionCount : 0;
    }

    private int GetAvailableAgentCount()
    {
        return agentRoster != null ? agentRoster.AvailableCount : 0;
    }

    private void ResolveReferences()
    {
        if (gameState == null)
            gameState = FindFirstObjectByType<GameState>();

        if (gameCalendar == null)
            gameCalendar = FindFirstObjectByType<GameCalendar>();

        if (districtManager == null)
            districtManager = FindFirstObjectByType<DistrictManager>();

        if (missionManager == null)
            missionManager = FindFirstObjectByType<MissionManager>();

        if (agentRoster == null)
            agentRoster = FindFirstObjectByType<AgentRoster>();

        if (recruitmentManager == null)
            recruitmentManager = FindFirstObjectByType<RecruitmentManager>();

        if (baseProgression == null)
            baseProgression = FindFirstObjectByType<BaseProgressionManager>();
    }

    private void SubscribeToCalendar()
    {
        if (subscribedToCalendar || gameCalendar == null)
            return;

        gameCalendar.OnCalendarChanged += OnCalendarChanged;
        subscribedToCalendar = true;
    }

    private void UnsubscribeFromCalendar()
    {
        if (!subscribedToCalendar || gameCalendar == null)
            return;

        gameCalendar.OnCalendarChanged -= OnCalendarChanged;
        subscribedToCalendar = false;
    }

    private string RenderCopy(string source, WorldEventContext context)
    {
        if (string.IsNullOrEmpty(source))
            return string.Empty;

        string districtName = context?.TargetDistrict?.Data != null
            ? context.TargetDistrict.Data.districtName
            : "the city";

        string missionName = context?.ActiveMission?.Data != null
            ? context.ActiveMission.Data.missionName
            : "an active operation";

        string agentName = context?.TargetAgent != null
            ? context.TargetAgent.Name
            : "an agent";

        float districtHeat = context?.TargetDistrict != null
            ? context.TargetDistrict.LocalHeat
            : 0f;

        string rendered = source;
        rendered = rendered.Replace("{district}", districtName);
        rendered = rendered.Replace("{mission}", missionName);
        rendered = rendered.Replace("{agent}", agentName);
        rendered = rendered.Replace("{chaos}", gameState != null ? gameState.Chaos.ToString("F0") : "0");
        rendered = rendered.Replace("{cure}", gameState != null ? gameState.Cure.ToString("F0") : "0");
        rendered = rendered.Replace("{money}", gameState != null ? gameState.Money.ToString() : "0");
        rendered = rendered.Replace("{heat}", districtHeat.ToString("F0"));
        rendered = rendered.Replace("{lostAgents}", agentRoster != null ? agentRoster.LostCount.ToString() : "0");
        rendered = rendered.Replace("{activeMissions}", GetActiveMissionCount().ToString());
        return rendered;
    }

    private void CancelPendingImpact()
    {
        if (pendingImpactContext == null)
            return;

        pendingImpactContext = null;
        OnImpactEventCancelled?.Invoke();
    }

    private static bool IsCoolingDown<T>(
        Dictionary<T, float> lastTimes,
        T definition,
        float cooldownSeconds)
    {
        if (cooldownSeconds <= 0f)
            return false;

        if (!lastTimes.TryGetValue(definition, out float lastTime))
            return false;

        return Time.time - lastTime < cooldownSeconds;
    }

    private static bool ConditionsNeedDistrict(WorldEventConditionSet conditions)
    {
        return conditions != null &&
               (conditions.minimumDistrictHeat > 0f ||
                conditions.minimumDistrictChaos > 0f ||
                conditions.minimumDistrictCure > 0f);
    }

    private static bool ImpactChoicesNeedDistrict(ImpactEventChoice[] choices)
    {
        if (choices == null)
            return false;

        for (int i = 0; i < choices.Length; i++)
        {
            WorldEventConsequenceSet consequences = choices[i]?.consequences;
            if (consequences == null)
                continue;

            if (!Mathf.Approximately(consequences.districtChaosDelta, 0f) ||
                !Mathf.Approximately(consequences.districtCureDelta, 0f) ||
                !Mathf.Approximately(consequences.districtHeatDelta, 0f))
            {
                return true;
            }
        }

        return false;
    }
}
