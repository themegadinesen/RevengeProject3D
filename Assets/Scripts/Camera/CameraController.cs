using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles all camera behaviour: zoom in/out, pan with spring-back, and
/// smooth transitions between map view and base view.
///
/// Attach to the Main Camera. Assign a CameraZoomConfig and a ZoomAnchor
/// in the Inspector.
///
/// Uses the new Input System with embedded InputAction fields — bindings
/// are visible and rebindable directly in the Inspector on this component.
/// </summary>
public class CameraController : MonoBehaviour
{
    // ── Inspector Fields ──────────────────────────────────────────────
    [Header("References")]
    [Tooltip("Drag your CameraZoomConfig ScriptableObject here.")]
    [SerializeField] private CameraZoomConfig config;

    [Tooltip("Current zoom-in anchor. Swap at runtime for Act 2.")]
    [SerializeField] private ZoomAnchor currentAnchor;

    [Header("Input Actions")]
    [Tooltip("Scroll input for zooming. Default: mouse scroll wheel.")]
    [SerializeField] private InputAction scrollAction = new InputAction(
        "Scroll", InputActionType.Value, "<Mouse>/scroll/y"
    );

    [Tooltip("Button held to pan the camera. Default: middle mouse button.")]
    [SerializeField] private InputAction panButtonAction = new InputAction(
        "PanButton", InputActionType.Button, "<Mouse>/middleButton"
    );

    [Tooltip("Pointer movement delta used for panning. Default: mouse delta.")]
    [SerializeField] private InputAction panDeltaAction = new InputAction(
        "PanDelta", InputActionType.Value, "<Pointer>/delta"
    );

    [Header("Starting State")]
    [Tooltip("Check this to start the game in map (zoomed-out) view.")]
    [SerializeField] private bool startInMapView = true;

    // ── Runtime State (private) ───────────────────────────────────────
    private Camera cam;

    // Where the camera sits when fully zoomed out.
    private Vector3 mapCenter;

    // The position the camera considers "home" — blended between map center and anchor.
    private Vector3 homePosition;

    // The zoom level the camera is lerping toward.
    private float targetZoom;

    // Offset from homePosition caused by panning.
    private Vector3 panOffset;

    // True when the player is actively holding the pan button.
    private bool isPanning;

    // True when we're zoomed in to the base view.
    private bool isInBaseView;

    private bool inputLocked;

    // ── Unity Lifecycle ───────────────────────────────────────────────

    private void Awake()
    {
        cam = GetComponent<Camera>();

        if (config == null)
        {
            Debug.LogError("CameraController: No CameraZoomConfig assigned!", this);
            enabled = false;
            return;
        }

        // Remember the starting position as the map center.
        mapCenter = transform.position;

        // Initialise to the correct view.
        if (startInMapView)
        {
            targetZoom = config.mapZoom;
            cam.orthographicSize = config.mapZoom;
            homePosition = mapCenter;
            isInBaseView = false;
        }
        else
        {
            GoToBaseView();
            // Snap immediately instead of lerping on the first frame.
            cam.orthographicSize = targetZoom;
            transform.position = new Vector3(homePosition.x, homePosition.y, transform.position.z);
        }
    }

    /// <summary>
    /// Enable all input actions when this component is enabled.
    /// Required by the new Input System — actions do nothing until enabled.
    /// </summary>
    private void OnEnable()
    {
        scrollAction.Enable();
        panButtonAction.Enable();
        panDeltaAction.Enable();
    }

    /// <summary>
    /// Disable all input actions when this component is disabled or destroyed.
    /// Prevents input from firing when the camera isn't active.
    /// </summary>
    private void OnDisable()
    {
        scrollAction.Disable();
        panButtonAction.Disable();
        panDeltaAction.Disable();
    }

    private void Update()
    {
        HandleScrollZoom();
        HandlePanInput();
        UpdateHomePosition();
        ApplySpringBack();
        ApplySmoothMovement();
    }

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>
    /// Smoothly transitions to the base (zoomed-in) view centered on the anchor.
    /// Call this from a UI button, hotkey, or game event.
    /// </summary>
    public void GoToBaseView()
    {
        if (currentAnchor == null)
        {
            Debug.LogWarning("CameraController: No ZoomAnchor assigned.");
            return;
        }

        isInBaseView = true;
        targetZoom = currentAnchor.GetBaseZoom(config.baseZoom);
        panOffset = Vector3.zero;
    }

    /// <summary>
    /// Smoothly transitions back to the full map (zoomed-out) view.
    /// </summary>
    public void GoToMapView()
    {
        isInBaseView = false;
        targetZoom = config.mapZoom;
        panOffset = Vector3.zero;
    }

    /// <summary>
    /// Swap the zoom anchor at runtime (e.g., when the player moves to Act 2).
    /// </summary>
    public void SetAnchor(ZoomAnchor newAnchor)
    {
        currentAnchor = newAnchor;

        // If we're already in base view, re-center on the new anchor.
        if (isInBaseView)
        {
            GoToBaseView();
        }
    }

    /// <summary>
    /// Swap the entire config at runtime (e.g., different zoom limits per act).
    /// </summary>
    public void SetConfig(CameraZoomConfig newConfig)
    {
        config = newConfig;
    }

    /// <summary>ViewManager reads this to know which anchor is active.</summary>
    public ZoomAnchor CurrentAnchor => currentAnchor;
    
    /// <summary>
    /// Temporarily locks or unlocks scroll and pan input.
    /// Called by ViewManager during transitions.
    /// </summary>
    public void LockInput(bool locked)
    {
        inputLocked = locked;
    }

    // ── Input Handling ────────────────────────────────────────────────

    private void HandleScrollZoom()
    {
        if (inputLocked) return;

        // ReadValue<float>() because we bound to scroll/y (a single axis).
        float scroll = scrollAction.ReadValue<float>();

        if (Mathf.Approximately(scroll, 0f))
            return;

        // Normalize: scroll/y returns ~120 per tick on most mice.
        // Dividing by 120 gives us ~1.0 per tick, matching the old behaviour.
        scroll /= 120f;

        // Scroll down = zoom out (bigger ortho size), scroll up = zoom in.
        targetZoom -= scroll * config.scrollZoomStep;
        targetZoom = Mathf.Clamp(targetZoom, config.minScrollZoom, config.maxScrollZoom);

    }

    private void HandlePanInput()
    {
        if (inputLocked) return;
        // IsPressed() returns true every frame the button is held — replaces
        // the old GetMouseButton() pattern without needing started/canceled callbacks.
        isPanning = panButtonAction.IsPressed();

        if (!isPanning)
            return;

        // Read the pointer delta (in screen pixels).
        Vector2 delta = panDeltaAction.ReadValue<Vector2>();

        // Invert so dragging right moves the camera left (moves the world right).
        // Scale by panSpeed and Time.deltaTime for frame-rate independence.
        Vector3 panDelta = new Vector3(-delta.x, -delta.y, 0f)
            * config.panSpeed * Time.deltaTime;

        panOffset += panDelta;

        // Clamp pan offset so the player can't scroll off the map.
        panOffset = Vector3.ClampMagnitude(panOffset, config.maxPanDistance);
    }

    // ── Position Blending ────────────────────────────────────────────

    /// <summary>
    /// Blends homePosition between mapCenter and the anchor based on the
    /// current target zoom. As the player zooms in, the camera drifts
    /// toward the anchor. Fully zoomed out = map center, fully zoomed in = anchor.
    /// </summary>
    private void UpdateHomePosition()
    {
        if (currentAnchor == null)
        {
            homePosition = mapCenter;
            return;
        }

        float baseZoom = currentAnchor.GetBaseZoom(config.baseZoom);

        // t = 0 when at mapZoom (fully out), t = 1 when at baseZoom (fully in).
        float t = Mathf.InverseLerp(config.mapZoom, baseZoom, targetZoom);

        Vector3 anchorPos = currentAnchor.transform.position;
        anchorPos.z = mapCenter.z; // Keep the camera's Z constant.

        homePosition = Vector3.Lerp(mapCenter, anchorPos, t);
    }

    // ── Smooth Movement ──────────────────────────────────────────────

    private void ApplySpringBack()
    {
        // When not panning, smoothly pull the offset back to zero.
        if (!isPanning && panOffset.sqrMagnitude > 0.001f)
        {
            panOffset = Vector3.Lerp(panOffset, Vector3.zero, config.springBackSpeed * Time.deltaTime);
        }
    }

    private void ApplySmoothMovement()
    {
        // Smoothly lerp the orthographic size.
        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize,
            targetZoom,
            config.zoomLerpSpeed * Time.deltaTime
        );

        // Smoothly lerp position toward homePosition + panOffset.
        Vector3 desiredPos = homePosition + panOffset;
        desiredPos.z = transform.position.z; // Keep the camera's Z constant.

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPos,
            config.positionLerpSpeed * Time.deltaTime
        );
    }
}