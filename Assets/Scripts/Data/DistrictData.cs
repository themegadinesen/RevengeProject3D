// Assets/Scripts/Data/DistrictData.cs
using UnityEngine;

[CreateAssetMenu(fileName = "NewDistrict", menuName = "Gameplay/District")]
public class DistrictData : ScriptableObject
{
    public string districtName = "New District";

    [Tooltip("Chaos must reach this value to unlock the district.")]
    [Min(0f)] public float chaosUnlockThreshold;

    [Tooltip("Missions available in this district.")]
    public MissionData[] missions;
}