using System;
using UnityEngine;
using UnityEngine.Serialization;

public enum WorldEventActRequirement
{
    Any,
    Act1,
    Act2
}

public enum WorldEventTargetMode
{
    None,
    FirstUnlockedDistrict,
    HighestHeatDistrict,
    HighestChaosDistrict,
    HighestCureDistrict,
    ActiveMissionDistrict,
    SelectedDistrict
}

[Serializable]
public class WorldEventConditionSet
{
    [Header("Run State")]
    public WorldEventActRequirement actRequirement = WorldEventActRequirement.Any;
    [Min(0f)] public float minimumChaos;
    [Min(0f)] public float minimumCure;
    [Min(0)] public int minimumLostAgents;

    [Header("District State")]
    [Min(0f)] public float minimumDistrictHeat;
    [Min(0f)] public float minimumDistrictChaos;
    [Min(0f)] public float minimumDistrictCure;

    [Header("Requirements")]
    public bool requiresActiveMission;
    public bool requiresAvailableAgent;
}

[Serializable]
public class WorldEventConsequenceSet
{
    [Header("Global")]
    public int moneyDelta;
    public float globalChaosDelta;
    public float globalCureDelta;

    [Header("Target District")]
    public float districtChaosDelta;
    public float districtCureDelta;
    public float districtHeatDelta;

    [Header("Agents")]
    [FormerlySerializedAs("loseRandomAvailableAgent")]
    [Tooltip("Loses the first currently available agent. Busy mission agents are not selected.")]
    public bool loseFirstAvailableAgent;

    [Header("Recruitment")]
    [Tooltip("Optional. Uses this mission's reward candidate to create a pending recruit.")]
    public MissionData candidateArrivalMission;

    [Header("Choice Rules")]
    [Tooltip("When true, a negative money choice is disabled unless the player can pay it.")]
    public bool requireMoneyForNegativeDelta = true;
}

[Serializable]
public class ImpactEventChoice
{
    public string label = "Choice";
    [TextArea(2, 4)]
    public string description = "";
    [Tooltip("Optional short player-facing summary of the consequences.")]
    public string consequencePreview = "";
    public WorldEventConsequenceSet consequences = new();
}

public class WorldEventContext
{
    public FlavorEventDefinition FlavorDefinition;
    public ImpactEventDefinition ImpactDefinition;
    public RuntimeDistrict TargetDistrict;
    public ActiveMission ActiveMission;
    public RuntimeAgent TargetAgent;
    public string Title;
    public string Message;
}
