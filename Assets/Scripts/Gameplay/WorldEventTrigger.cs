using UnityEngine;

public class WorldEventTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorldEventDirector worldEventDirector;

    [Header("Event")]
    [SerializeField] private ScheduledWorldEventType eventType = ScheduledWorldEventType.Flavor;
    [SerializeField] private FlavorEventDefinition flavorEvent;
    [SerializeField] private ImpactEventDefinition impactEvent;

    [Header("Target Override")]
    [Tooltip("Optional. When assigned, this trigger uses this district as the event target.")]
    [SerializeField] private DistrictData targetDistrict;

    [Header("Rules")]
    [SerializeField] private bool ignoreDefinitionCooldown;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    public void Trigger()
    {
        switch (eventType)
        {
            case ScheduledWorldEventType.Flavor:
                TriggerFlavor();
                break;

            case ScheduledWorldEventType.Impact:
                TriggerImpact();
                break;
        }
    }

    public bool TriggerFlavor()
    {
        ResolveReferences();

        return worldEventDirector != null &&
               worldEventDirector.TryTriggerFlavorEvent(
                   flavorEvent,
                   targetDistrict,
                   ignoreDefinitionCooldown);
    }

    public bool TriggerImpact()
    {
        ResolveReferences();

        return worldEventDirector != null &&
               worldEventDirector.TryTriggerImpactEvent(
                   impactEvent,
                   targetDistrict,
                   ignoreDefinitionCooldown);
    }

    private void ResolveReferences()
    {
        if (worldEventDirector == null)
            worldEventDirector = FindFirstObjectByType<WorldEventDirector>();
    }
}
