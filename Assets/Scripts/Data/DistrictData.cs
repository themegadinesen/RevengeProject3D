// Assets/Scripts/Data/DistrictData.cs
using UnityEngine;

[CreateAssetMenu(fileName = "NewDistrict", menuName = "Gameplay/District")]
public class DistrictData : ScriptableObject
{
    [Header("Identity")]
    public string districtName = "New District";

    [TextArea(2, 4)]
    public string loreText = "";

    [Tooltip("E.g. Chip Manufacturing, Agriculture, Finance.")]
    public string industryFocus = "General";

    [Header("Unlock")]
    [Tooltip("Global People Affected must reach this count to unlock the district.")]
    [Min(0)] public int peopleAffectedUnlockThreshold;

    [Header("Simulation")]
    [Tooltip("Weight for global aggregation. Higher = more impact on global Chaos/Cure.")]
    [Min(0.1f)] public float populationWeight = 1f;

    [Tooltip("Local chaos this district starts with.")]
    [Min(0f)] public float startingChaos;

    [Tooltip("Local cure progress this district starts with.")]
    [Min(0f)] public float startingCure;

    [Tooltip("Local heat this district starts with.")]
    [Min(0f)] public float startingHeat;

    [Header("Missions")]
    [Tooltip("Missions available in this district.")]
    public MissionData[] missions;
}