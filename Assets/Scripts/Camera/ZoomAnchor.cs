using UnityEngine;

/// <summary>
/// Place this on any GameObject to mark it as the camera's zoom-in target.
/// The CameraController finds or receives this reference.
/// </summary>
public class ZoomAnchor : MonoBehaviour
{
    [Header("2D Map View")]
    [Tooltip("Optional: override the base zoom level for this specific anchor. " +
             "Leave at 0 to use the config default.")]
    public float overrideBaseZoom;

    [Header("3D Base View")]
    [Tooltip("Camera offset from this anchor in 3D. " +
             "E.g. (0, 12, -10) = above and behind, looking down ~50°.")]
    public Vector3 baseCameraOffset = new Vector3(0f, 12f, -10f);

    [Tooltip("Perspective field of view. 0 = use default 60.")]
    public float baseFOV;

    [Tooltip("Closest the camera can dolly toward the base.")]
    public float baseMinDistance = 5f;

    [Tooltip("Farthest the camera can zoom out. " +
             "Scrolling out past this triggers transition back to the map.")]
    public float baseMaxDistance = 25f;

    public float GetBaseZoom(float configDefault)
    {
        return overrideBaseZoom > 0f ? overrideBaseZoom : configDefault;
    }

    public float GetBaseFOV()
    {
        return baseFOV > 0f ? baseFOV : 60f;
    }

    public Vector3 GetBaseCameraPosition()
    {
        return transform.position + baseCameraOffset;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawLine(transform.position + Vector3.left, transform.position + Vector3.right);
        Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.down);

        // Draw the 3D camera position in the scene view.
        Gizmos.color = Color.yellow;
        Vector3 camPos = GetBaseCameraPosition();
        Gizmos.DrawWireSphere(camPos, 0.3f);
        Gizmos.DrawLine(camPos, transform.position);
    }
}