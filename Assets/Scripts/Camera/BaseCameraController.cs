using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Perspective camera controller for the 3D base view.
/// Dolly-zoom via scroll, middle-mouse pan, spring-back to center.
///
/// Attach to Main Camera. Starts DISABLED — ViewManager enables it
/// when transitioning into the base.
/// </summary>
public class BaseCameraController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────
    [Header("Input Actions")]
    [SerializeField] private InputAction scrollAction = new InputAction(
        "Scroll", InputActionType.Value, "<Mouse>/scroll/y"
    );

    [SerializeField] private InputAction panButtonAction = new InputAction(
        "PanButton", InputActionType.Button, "<Mouse>/middleButton"
    );

    [SerializeField] private InputAction panDeltaAction = new InputAction(
        "PanDelta", InputActionType.Value, "<Pointer>/delta"
    );

    [Header("Tuning")]
    [Tooltip("How far each scroll tick moves the camera (dolly).")]
    [SerializeField] private float zoomSpeed = 2f;

    [Tooltip("Pan speed multiplier.")]
    [SerializeField] private float panSpeed = 0.3f;

    [Tooltip("How fast position and rotation lerp. Higher = snappier.")]
    [Range(1f, 20f)]
    [SerializeField] private float smoothSpeed = 8f;

    [Tooltip("How fast pan springs back to center after releasing.")]
    [Range(1f, 20f)]
    [SerializeField] private float springBackSpeed = 4f;

    [Tooltip("Max pan distance from center.")]
    [SerializeField] private float maxPanDistance = 10f;

    // ── Runtime (set by Initialize) ───────────────────────────────────
    private Vector3 lookAtPoint;
    private Vector3 offsetDirection;
    private float targetDistance;
    private float minDistance;
    private float maxDistance;
    private Vector3 panOffset;
    private bool inputLocked;
    private bool wantsExit;

    /// <summary>
    /// True when the player scrolls out past max distance.
    /// ViewManager reads this to trigger the transition back to map.
    /// </summary>
    public bool WantsToExitBase => wantsExit;

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>
    /// Called by ViewManager when entering base view. Sets up the camera
    /// orbit center, direction, and distance limits.
    /// </summary>
    public void Initialize(Vector3 lookAt, Vector3 cameraOffset, float minDist, float maxDist)
    {
        lookAtPoint = lookAt;
        offsetDirection = cameraOffset.normalized;
        targetDistance = cameraOffset.magnitude;
        minDistance = minDist;
        maxDistance = maxDist;
        panOffset = Vector3.zero;
        wantsExit = false;
    }

    public void LockInput(bool locked)
    {
        inputLocked = locked;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────

    private void OnEnable()
    {
        scrollAction.Enable();
        panButtonAction.Enable();
        panDeltaAction.Enable();
        wantsExit = false;
    }

    private void OnDisable()
    {
        scrollAction.Disable();
        panButtonAction.Disable();
        panDeltaAction.Disable();
    }

    private void Update()
    {
        if (inputLocked) return;

        HandleZoom();
        HandlePan();
        ApplySpringBack();
        ApplyMovement();
    }

    // ── Input ─────────────────────────────────────────────────────────

    private void HandleZoom()
    {
        float scroll = scrollAction.ReadValue<float>() / 120f;
        if (Mathf.Approximately(scroll, 0f)) return;

        targetDistance -= scroll * zoomSpeed;

        // Trying to zoom out past max → signal exit.
        if (targetDistance > maxDistance)
        {
            wantsExit = true;
            targetDistance = maxDistance;
        }

        targetDistance = Mathf.Max(targetDistance, minDistance);
    }

    private void HandlePan()
    {
        if (!panButtonAction.IsPressed()) return;

        Vector2 delta = panDeltaAction.ReadValue<Vector2>();

        // Pan in camera's local screen plane.
        Vector3 right = transform.right;
        Vector3 up = transform.up;
        Vector3 panDelta = (-delta.x * right + -delta.y * up)
                           * panSpeed * Time.deltaTime;

        panOffset += panDelta;
        panOffset = Vector3.ClampMagnitude(panOffset, maxPanDistance);
    }

    private void ApplySpringBack()
    {
        if (!panButtonAction.IsPressed() && panOffset.sqrMagnitude > 0.001f)
        {
            panOffset = Vector3.Lerp(
                panOffset, Vector3.zero, springBackSpeed * Time.deltaTime
            );
        }
    }

    // ── Movement ──────────────────────────────────────────────────────

    private void ApplyMovement()
    {
        Vector3 center = lookAtPoint + panOffset;
        Vector3 desiredPos = center + offsetDirection * targetDistance;

        transform.position = Vector3.Lerp(
            transform.position, desiredPos, smoothSpeed * Time.deltaTime
        );

        // Always look at the (panned) center of the base.
        Quaternion desiredRot = Quaternion.LookRotation(center - transform.position);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, desiredRot, smoothSpeed * Time.deltaTime
        );
    }
}