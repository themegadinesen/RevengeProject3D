using UnityEngine;

[CreateAssetMenu(
    fileName = "NewImpactEvent",
    menuName = "Gameplay/World Events/Impact Event")]
public class ImpactEventDefinition : ScriptableObject
{
    [Header("Trigger Rules")]
    [Min(0f)] public float cooldownSeconds = 180f;
    public WorldEventTargetMode targetMode = WorldEventTargetMode.FirstUnlockedDistrict;
    public WorldEventConditionSet conditions = new();

    [Header("Copy")]
    public string eventTitle = "World Event";
    [TextArea(3, 6)]
    public string description = "Something is happening near {district}.";

    [Header("Choices")]
    public ImpactEventChoice[] choices;
}
