// Assets/Scripts/Data/BuildingData.cs
using UnityEngine;

[CreateAssetMenu(fileName = "NewBuilding", menuName = "Gameplay/Building")]
public class BuildingData : ScriptableObject
{
    [Header("Identity")]
    public string buildingName = "New Building";
    [TextArea(2, 4)]
    public string description = "";

    [Header("Cost & Time")]
    [Min(0)] public int moneyCost;
    [Tooltip("Build time in seconds. 0 = instant.")]
    [Min(0f)] public float buildTime = 30f;


    
    [Header("Domain")]
    
    [Tooltip("Which infrastructure domain this building unlocks when built.")]
    public InfrastructureDomain unlocksDomain = InfrastructureDomain.None;

    [Header("Prerequisites")]
    [Tooltip("Buildings that must be Built before this one becomes Available. Leave empty for no prerequisites.")]
    public BuildingData[] prerequisites;

    [Header("Bonuses (applied when Built)")]
    [Tooltip("Extra agent capacity added to roster.")]
    [Min(0)] public int agentCapacityBonus;
    [Tooltip("Flat bonus added to all mission success chances (e.g. 0.05 = +5%).")]
    [Range(0f, 0.5f)] public float missionSuccessBonus;
    [Tooltip("Multiplier applied to money gains (e.g. 0.1 = +10%). Stacks additively.")]
    [Range(0f, 1f)] public float moneyGainBonus;
}