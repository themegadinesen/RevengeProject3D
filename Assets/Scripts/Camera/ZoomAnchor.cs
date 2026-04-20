using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Marks a location the map camera can focus on and the base camera can orbit around.
/// </summary>
public class ZoomAnchor : MonoBehaviour
{
    [Header("Map Focus")]
    [FormerlySerializedAs("overrideBaseZoom")]
    [Tooltip("Optional override for the orthographic size used when the map is fully focused on this anchor. 0 uses the shared config value.")]
    [Min(0f)]
    public float overrideFocusedMapZoom;

    [Header("Base View Placement")]
    [FormerlySerializedAs("baseCameraOffset")]
    [Tooltip("Direction from this anchor that the base camera uses when entering base view. Magnitude does not control the start zoom; use Override Base Distance instead.")]
    public Vector3 baseCameraDirection = new Vector3(0f, 12f, -10f);

    [Header("Base View Framing Overrides")]
    [FormerlySerializedAs("baseFOV")]
    [Tooltip("Optional per-anchor override for the normal base-view field of view. 0 uses the shared config default.")]
    [Range(0f, 179f)]
    public float overrideBaseFieldOfView;

    [Tooltip("Optional per-anchor override for the fully top-down field of view. 0 uses the shared config default.")]
    [Range(0f, 179f)]
    public float overrideTopDownBaseFieldOfView;

    [Tooltip("Optional override for the starting base-view distance. 0 uses the shared config default.")]
    [Min(0f)]
    public float overrideBaseDistance = 0f;

    [Tooltip("Optional per-anchor override for extra height added at full top-down. 0 uses the shared config default.")]
    [Min(0f)]
    public float overrideTopDownHeightBoost = 0f;

    [Header("Base View Distance")]
    [Tooltip("Closest distance allowed in base view.")]
    [Min(0.1f)]
    public float baseMinDistance = 5f;

    [Tooltip("Farthest distance the camera can visibly reach while still in base view.")]
    [Min(0.1f)]
    public float baseMaxDistance = 25f;

    [Tooltip("Zooming out past this requested distance triggers the transition back to map view.")]
    [Min(0f)]
    public float baseReturnToMapDistance = 0f;

    [Header("Base View Top Down Transition")]
    [Tooltip("Base-view distance where the camera starts tilting upward toward a top-down view.")]
    [Min(0.1f)]
    public float baseTopDownTransitionStartDistance = 30f;

    [Tooltip("Base-view distance where the camera becomes fully top-down.")]
    [Min(0.1f)]
    public float baseTopDownTransitionCompleteDistance = 45f;

    public float GetFocusedMapZoom(float defaultFocusedMapZoom)
    {
        return overrideFocusedMapZoom > 0f ? overrideFocusedMapZoom : defaultFocusedMapZoom;
    }

    public float GetBaseFieldOfView(float defaultFOV)
    {
        return overrideBaseFieldOfView > 0f ? overrideBaseFieldOfView : defaultFOV;
    }

    public float GetTopDownBaseFieldOfView(float defaultFOV)
    {
        return overrideTopDownBaseFieldOfView > 0f ? overrideTopDownBaseFieldOfView : defaultFOV;
    }

    public float GetBaseStartDistance(float defaultBaseDistance)
    {
        return overrideBaseDistance > 0f ? overrideBaseDistance : defaultBaseDistance;
    }

    public float GetTopDownHeightBoost(float defaultHeightBoost)
    {
        return overrideTopDownHeightBoost > 0f ? overrideTopDownHeightBoost : defaultHeightBoost;
    }

    public float GetBaseWaypointDistance(float defaultBaseDistance)
    {
        float preferredBaseDistance = GetBaseStartDistance(defaultBaseDistance);
        float lastNonTopDownDistance = Mathf.Max(
            baseMinDistance,
            baseTopDownTransitionStartDistance - 0.01f
        );

        return Mathf.Clamp(preferredBaseDistance, baseMinDistance, lastNonTopDownDistance);
    }

    public float GetReturnToMapDistance()
    {
        if (baseReturnToMapDistance <= 0f)
            return baseMaxDistance;

        return Mathf.Max(baseReturnToMapDistance, baseMaxDistance);
    }

    public Vector3 GetBaseCameraDirection()
    {
        if (baseCameraDirection.sqrMagnitude <= 0.0001f)
            return Vector3.back;

        return baseCameraDirection.normalized;
    }

    public Vector3 GetBaseCameraPosition(float defaultBaseDistance)
    {
        return transform.position + GetBaseCameraDirection() * GetBaseStartDistance(defaultBaseDistance);
    }

    private void OnValidate()
    {
        baseMinDistance = Mathf.Max(0.1f, baseMinDistance);
        baseMaxDistance = Mathf.Max(baseMinDistance, baseMaxDistance);

        if (overrideBaseDistance > 0f)
            overrideBaseDistance = Mathf.Max(0.1f, overrideBaseDistance);

        if (overrideTopDownHeightBoost > 0f)
            overrideTopDownHeightBoost = Mathf.Max(0f, overrideTopDownHeightBoost);

        baseTopDownTransitionStartDistance = Mathf.Max(baseMinDistance, baseTopDownTransitionStartDistance);
        baseTopDownTransitionCompleteDistance = Mathf.Max(
            baseTopDownTransitionStartDistance,
            baseTopDownTransitionCompleteDistance
        );

        baseMaxDistance = Mathf.Max(baseMaxDistance, baseTopDownTransitionCompleteDistance);

        if (baseReturnToMapDistance > 0f)
            baseReturnToMapDistance = Mathf.Max(baseReturnToMapDistance, baseMaxDistance);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawLine(transform.position + Vector3.left, transform.position + Vector3.right);
        Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.down);

        Gizmos.color = Color.yellow;
        float previewDistance = overrideBaseDistance > 0f
            ? overrideBaseDistance
            : Mathf.Clamp(baseCameraDirection.magnitude, baseMinDistance, baseMaxDistance);

        Vector3 cameraPosition = transform.position + GetBaseCameraDirection() * previewDistance;
        Gizmos.DrawWireSphere(cameraPosition, 0.3f);
        Gizmos.DrawLine(cameraPosition, transform.position);
    }
}