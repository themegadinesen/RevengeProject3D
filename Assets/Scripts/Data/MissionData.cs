// Assets/Scripts/Data/MissionData.cs
using UnityEngine;

public enum MissionBehavior
{
    /// <summary>All effects applied on resolution.</summary>
    Instant,
    /// <summary>Chaos drips in gradually over the mission duration.</summary>
    SlowBurn
}

[CreateAssetMenu(fileName = "NewMission", menuName = "Gameplay/Mission")]
public class MissionData : ScriptableObject
{
    [Header("Info")]
    public string missionName = "New Mission";
    [TextArea(2, 4)]
    public string description = "";

    [Header("Costs")]
    [Min(0)] public int moneyCost;

    [Header("Stat Requirements")]
    [Tooltip("Total INT the assigned team should meet.")]
    [Min(0)] public int requiredINT;
    [Tooltip("Total STR the assigned team should meet.")]
    [Min(0)] public int requiredSTR;
    [Tooltip("Total AGI the assigned team should meet.")]
    [Min(0)] public int requiredAGI;

    [Header("Infrastructure Requirements")]
    [Tooltip("Domain that must be unlocked to launch this mission. None = no requirement.")]
    public InfrastructureDomain requiredDomain = InfrastructureDomain.None;
    [Tooltip("Specific building that must be Built to launch this mission. Leave empty for no requirement.")]
    public BuildingData requiredBuilding;

    [Header("Duration & Behavior")]
    [Tooltip("Mission duration in seconds. 0 = resolves instantly.")]
    [Min(0f)] public float duration = 30f;
    public MissionBehavior behavior = MissionBehavior.Instant;

    [Header("Effects on Success")]
    public float chaosOnSuccess  = 10f;
    public float cureOnSuccess   = 0f;
    public int   moneyOnSuccess  = 50;

    [Header("Effects on Failure")]
    public float chaosOnFailure  = 2f;
    public float cureOnFailure   = 15f;
    [Tooltip("Agents randomly lost from the team on failure.")]
    public int   agentsLostOnFailure = 1;
}