using UnityEngine;

public enum ScheduledWorldEventType
{
    Flavor,
    Impact
}

[CreateAssetMenu(
    fileName = "NewScheduledWorldEvent",
    menuName = "Gameplay/World Events/Scheduled Event")]
public class ScheduledWorldEventDefinition : ScriptableObject
{
    [Header("Time")]
    [Min(1)] public int day = 1;
    [Range(0, 23)] public int hour;
    [Range(0, 59)] public int minute;

    [Header("Event")]
    public ScheduledWorldEventType eventType = ScheduledWorldEventType.Flavor;
    public FlavorEventDefinition flavorEvent;
    public ImpactEventDefinition impactEvent;

    [Header("Target Override")]
    [Tooltip("Optional. When assigned, this scheduled event uses this district as its target.")]
    public DistrictData targetDistrict;

    [Header("Rules")]
    [Tooltip("When true, this scheduled event ignores the referenced event asset's cooldown, but still starts that cooldown after firing.")]
    public bool ignoreDefinitionCooldown = true;

    public bool IsDue(GameCalendar calendar)
    {
        if (calendar == null)
            return false;

        int scheduledMinutes = GetTotalMinutes(day, hour, minute);
        int currentMinutes = GetTotalMinutes(
            calendar.CurrentDay,
            calendar.CurrentHour,
            calendar.CurrentMinute);

        return currentMinutes >= scheduledMinutes;
    }

    private static int GetTotalMinutes(int eventDay, int eventHour, int eventMinute)
    {
        int safeDay = Mathf.Max(1, eventDay);
        int safeHour = Mathf.Clamp(eventHour, 0, 23);
        int safeMinute = Mathf.Clamp(eventMinute, 0, 59);

        return ((safeDay - 1) * 24 * 60) + (safeHour * 60) + safeMinute;
    }

    private void OnValidate()
    {
        day = Mathf.Max(1, day);
        hour = Mathf.Clamp(hour, 0, 23);
        minute = Mathf.Clamp(minute, 0, 59);
    }
}
