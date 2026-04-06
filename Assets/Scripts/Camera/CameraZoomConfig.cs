using UnityEngine;

/// <summary>
/// Inspector-tunable data for the camera system.
/// Create one per "act" or game phase via:
///   Right-click in Project → Create → Camera → Zoom Config
/// </summary>
[CreateAssetMenu(fileName = "NewCameraZoomConfig", menuName = "Camera/Zoom Config")]
public class CameraZoomConfig : ScriptableObject
{
    [Header("Zoom Levels (Orthographic Size)")]
    [Tooltip("Camera size when fully zoomed out (country map view).")]
    public float mapZoom = 15f;

    [Tooltip("Camera size when fully zoomed in (base view).")]
    public float baseZoom = 5f;

    [Tooltip("How fast the camera lerps between zoom levels. Higher = faster.")]
    [Range(1f, 20f)]
    public float zoomLerpSpeed = 6f;

    [Header("Scroll Zoom")]
    [Tooltip("How much each scroll tick changes the target zoom.")]
    public float scrollZoomStep = 1f;

    [Tooltip("Minimum orthographic size the player can scroll to.")]
    public float minScrollZoom = 3f;

    [Tooltip("Maximum orthographic size the player can scroll to.")]
    public float maxScrollZoom = 20f;

    [Header("Pan Settings")]
    [Tooltip("Pan speed in world units per second.")]
    public float panSpeed = 10f;

    [Tooltip("How fast the camera springs back to center after releasing pan.")]
    [Range(1f, 20f)]
    public float springBackSpeed = 4f;

    [Tooltip("Max distance the player can pan away from the current anchor.")]
    public float maxPanDistance = 8f;

    [Header("Transition")]
    [Tooltip("How fast the camera moves to the anchor position during a zoom transition.")]
    [Range(1f, 20f)]
    public float positionLerpSpeed = 5f;
}
