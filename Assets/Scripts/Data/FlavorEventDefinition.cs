using UnityEngine;

[CreateAssetMenu(
    fileName = "NewFlavorEvent",
    menuName = "Gameplay/World Events/Flavor Event")]
public class FlavorEventDefinition : ScriptableObject
{
    [Header("Trigger Rules")]
    [Min(0f)] public float cooldownSeconds = 30f;
    public WorldEventTargetMode targetMode = WorldEventTargetMode.FirstUnlockedDistrict;
    public WorldEventConditionSet conditions = new();

    [Header("Copy")]
    [TextArea(2, 4)]
    public string message = "Local reports mention unusual activity near {district}.";
}
