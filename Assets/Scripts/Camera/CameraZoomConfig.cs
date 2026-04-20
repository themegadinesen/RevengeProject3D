using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "NewCameraZoomConfig", menuName = "Camera/Zoom Config")]
public class CameraZoomConfig : ScriptableObject
{
    [Header("Map View Zoom")]
    [FormerlySerializedAs("mapZoom")]
    [Min(0.01f)]
    public float defaultMapZoom = 15f;

    [FormerlySerializedAs("baseZoom")]
    [Tooltip("Orthographic size used when the map is fully focused on the active base anchor.")]
    [Min(0.01f)]
    public float defaultFocusedMapZoom = 5f;

    [FormerlySerializedAs("minScrollZoom")]
    [Min(0.01f)]
    public float minMapZoom = 3f;

    [FormerlySerializedAs("maxScrollZoom")]
    [Min(0.01f)]
    public float maxMapZoom = 20f;

    [FormerlySerializedAs("scrollZoomStep")]
    [Tooltip("Orthographic size change per mouse-wheel tick in map view.")]
    [Min(0.01f)]
    public float mapZoomStep = 1f;

    [FormerlySerializedAs("zoomLerpSpeed")]
    [Range(1f, 20f)]
    public float mapZoomLerpSpeed = 6f;

    [Header("Map View Thresholds")]
    [Tooltip("When map orthographic size reaches this value or lower, transition from map view to base view.")]
    [Min(0.01f)]
    public float mapToBaseTransitionZoom = 6f;

    [Header("Map View Movement")]
    [FormerlySerializedAs("panSpeed")]
    [Min(0f)]
    public float mapPanSpeed = 10f;

    [FormerlySerializedAs("springBackSpeed")]
    [Range(1f, 20f)]
    public float mapPanSpringBackSpeed = 4f;

    [FormerlySerializedAs("maxPanDistance")]
    [Min(0f)]
    public float mapMaxPanDistance = 8f;

    [FormerlySerializedAs("positionLerpSpeed")]
    [Range(1f, 20f)]
    public float mapPositionLerpSpeed = 5f;

    [Header("Base View Zoom")]
    [Tooltip("Distance change per mouse-wheel tick in base view.")]
    [Min(0.01f)]
    public float baseZoomStep = 2f;

    [Header("Base View Pan")]
    public bool enableBasePan = true;

    [Tooltip("If enabled, base-view pan returns toward center when the player releases pan input.")]
    public bool enableBasePanSpringBack = true;

    [Tooltip("If enabled, base-view pan is clamped to the bounds below.")]
    public bool limitBasePanToBounds = true;

    [Tooltip("How far the player can pan right/left and up/down in base view, measured in local screen-plane units from the base center.")]
    public Vector2 basePanBounds = new Vector2(10f, 10f);

    [Min(0f)]
    public float basePanSpeed = 0.3f;

    [Range(1f, 20f)]
    public float basePanSpringBackSpeed = 4f;

    [Range(1f, 20f)]
    public float baseMoveLerpSpeed = 8f;

    [Header("Base View Defaults")]
    [Tooltip("Default perspective field of view when entering base view.")]
    [Range(1f, 179f)]
    public float defaultBaseFieldOfView = 60f;

    [Tooltip("Perspective field of view used when the base camera is fully top-down.")]
    [Range(1f, 179f)]
    public float defaultTopDownBaseFieldOfView = 68f;

    [Tooltip("Default starting distance from the base target when entering base view.")]
    [Min(0.1f)]
    public float defaultBaseDistance = 14f;

    [Tooltip("Additional world-space height added when the base camera reaches a fully top-down state.")]
    [Min(0f)]
    public float defaultTopDownHeightBoost = 6f;

    [Header("Transition FX")]
    [Range(0.05f, 1f)]
    public float peakBlurStrength = 0.4f;

    [Range(0.1f, 1f)]
    public float transitionDuration = 0.3f;

    [Range(0.1f, 0.9f)]
    public float swapPoint = 0.5f;

    public AnimationCurve blurCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 4f),
        new Keyframe(0.5f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, -4f, 0f)
    );

    private void OnValidate()
    {
        minMapZoom = Mathf.Max(0.01f, minMapZoom);
        maxMapZoom = Mathf.Max(minMapZoom, maxMapZoom);

        defaultMapZoom = Mathf.Clamp(defaultMapZoom, minMapZoom, maxMapZoom);
        defaultFocusedMapZoom = Mathf.Clamp(defaultFocusedMapZoom, minMapZoom, maxMapZoom);
        mapToBaseTransitionZoom = Mathf.Clamp(mapToBaseTransitionZoom, minMapZoom, maxMapZoom);

        mapZoomStep = Mathf.Max(0.01f, mapZoomStep);
        baseZoomStep = Mathf.Max(0.01f, baseZoomStep);

        mapPanSpeed = Mathf.Max(0f, mapPanSpeed);
        mapMaxPanDistance = Mathf.Max(0f, mapMaxPanDistance);

        basePanSpeed = Mathf.Max(0f, basePanSpeed);
        basePanBounds.x = Mathf.Max(0f, basePanBounds.x);
        basePanBounds.y = Mathf.Max(0f, basePanBounds.y);

        defaultBaseFieldOfView = Mathf.Clamp(defaultBaseFieldOfView, 1f, 179f);
        defaultTopDownBaseFieldOfView = Mathf.Clamp(defaultTopDownBaseFieldOfView, 1f, 179f);
        defaultBaseDistance = Mathf.Max(0.1f, defaultBaseDistance);
        defaultTopDownHeightBoost = Mathf.Max(0f, defaultTopDownHeightBoost);
    }
}