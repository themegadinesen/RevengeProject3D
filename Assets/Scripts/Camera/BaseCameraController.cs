using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Handles perspective base-view camera zoom, pan, framing, and top-down blending.
/// </summary>
public class BaseCameraController : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputAction panButtonAction = new InputAction(
        "PanButton", InputActionType.Button, "<Mouse>/middleButton"
    );

    [SerializeField] private InputAction panDeltaAction = new InputAction(
        "PanDelta", InputActionType.Value, "<Pointer>/delta"
    );

    private static readonly Vector3 TopDownScreenUpFallback = Vector3.forward;

    private Camera cam;
    private CameraZoomConfig config;
    private Vector3 lookAtPoint;
    private Vector3 orbitDirection;
    private float requestedDistance;
    private float targetDistance;
    private float minDistance;
    private float maxDistance;
    private float returnToMapDistance;
    private float topDownTransitionStartDistance;
    private float topDownTransitionCompleteDistance;
    private float baseFieldOfView;
    private float topDownBaseFieldOfView;
    private float topDownHeightBoost;
    private Vector3 panOffset;
    private bool inputLocked;
    private bool wantsExit;

    public bool WantsToExitBase => wantsExit;
    public float TargetDistance => targetDistance;
    public float MinDistance => minDistance;
    public float MaxDistance => maxDistance;
    public float TopDownTransitionCompleteDistance => topDownTransitionCompleteDistance;
    public float ReturnToMapDistance => returnToMapDistance;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    public void Initialize(
        CameraZoomConfig activeConfig,
        Vector3 lookAt,
        Vector3 cameraDirection,
        float startDistance,
        float minDist,
        float maxDist,
        float topDownStartDist,
        float topDownCompleteDist,
        float exitDist,
        float normalBaseFieldOfView,
        float fullyTopDownFieldOfView,
        float fullyTopDownHeightBoost)
    {
        config = activeConfig;
        lookAtPoint = lookAt;
        orbitDirection = cameraDirection.sqrMagnitude > 0.0001f
            ? cameraDirection.normalized
            : Vector3.back;

        minDistance = Mathf.Max(0.1f, minDist);
        topDownTransitionStartDistance = Mathf.Max(minDistance, topDownStartDist);
        topDownTransitionCompleteDistance = Mathf.Max(
            topDownTransitionStartDistance,
            topDownCompleteDist
        );

        maxDistance = Mathf.Max(minDistance, maxDist, topDownTransitionCompleteDistance);
        returnToMapDistance = Mathf.Max(exitDist, maxDistance);

        requestedDistance = Mathf.Clamp(startDistance, minDistance, maxDistance);
        targetDistance = requestedDistance;

        baseFieldOfView = Mathf.Clamp(normalBaseFieldOfView, 1f, 179f);
        topDownBaseFieldOfView = Mathf.Clamp(fullyTopDownFieldOfView, 1f, 179f);
        topDownHeightBoost = Mathf.Max(0f, fullyTopDownHeightBoost);

        panOffset = Vector3.zero;
        wantsExit = false;
    }

    public void LockInput(bool locked)
    {
        inputLocked = locked;
    }

    public void SnapToCurrentState(Transform cameraTransform)
    {
        Vector3 center = GetCurrentCenter();
        Vector3 desiredPosition = GetDesiredCameraPosition(center);

        cameraTransform.position = desiredPosition;
        cameraTransform.rotation = GetDesiredCameraRotation(center, desiredPosition);

        if (cam != null)
            cam.fieldOfView = GetDesiredFieldOfView();
    }

    public void SetDistanceTarget(float distance)
    {
        if (config == null) return;

        requestedDistance = Mathf.Max(0f, distance);
        wantsExit = false;

        if (requestedDistance < minDistance)
            requestedDistance = minDistance;

        targetDistance = Mathf.Clamp(requestedDistance, minDistance, maxDistance);
    }

    private void OnEnable()
    {
        panButtonAction.Enable();
        panDeltaAction.Enable();
        wantsExit = false;
    }

    private void OnDisable()
    {
        panButtonAction.Disable();
        panDeltaAction.Disable();
    }

    private void Update()
    {
        if (config == null) return;

        HandlePan();
        ApplySpringBack();
        ApplyMovement();
    }

    private void HandlePan()
    {
        if (!config.enableBasePan) return;
        if (!IsPanPressed()) return;

        Vector2 delta = panDeltaAction.ReadValue<Vector2>();

        Quaternion panRotation = GetPanReferenceRotation();
        Vector3 panRight = panRotation * Vector3.right;
        Vector3 panUp = panRotation * Vector3.up;

        Vector3 panDelta = (-delta.x * panRight + -delta.y * panUp)
            * config.basePanSpeed * Time.deltaTime;

        panOffset += panDelta;

        if (config.limitBasePanToBounds)
            panOffset = ClampPanOffsetToBounds(panOffset, panRotation);
    }

    private void ApplySpringBack()
    {
        if (!config.enableBasePan) return;
        if (!config.enableBasePanSpringBack) return;

        if (!IsPanPressed() && panOffset.sqrMagnitude > 0.001f)
        {
            panOffset = Vector3.Lerp(
                panOffset,
                Vector3.zero,
                config.basePanSpringBackSpeed * Time.deltaTime
            );
        }
    }

    private Vector3 ClampPanOffsetToBounds(Vector3 offset, Quaternion referenceRotation)
    {
        Vector3 right = referenceRotation * Vector3.right;
        Vector3 up = referenceRotation * Vector3.up;

        float horizontal = Vector3.Dot(offset, right);
        float vertical = Vector3.Dot(offset, up);

        horizontal = Mathf.Clamp(horizontal, -config.basePanBounds.x, config.basePanBounds.x);
        vertical = Mathf.Clamp(vertical, -config.basePanBounds.y, config.basePanBounds.y);

        return right * horizontal + up * vertical;
    }

    private float GetTopDownBlend()
    {
        if (targetDistance <= topDownTransitionStartDistance)
            return 0f;

        if (topDownTransitionCompleteDistance <= topDownTransitionStartDistance)
            return 1f;

        float rawBlend = Mathf.InverseLerp(
            topDownTransitionStartDistance,
            topDownTransitionCompleteDistance,
            targetDistance
        );

        return config != null && config.topToBaseBlendCurve != null
            ? Mathf.Clamp01(config.topToBaseBlendCurve.Evaluate(rawBlend))
            : rawBlend;
    }

    private Vector3 GetCurrentCenter()
    {
        return lookAtPoint + panOffset;
    }

    private Vector3 GetCurrentOrbitDirection()
    {
        float blend = GetTopDownBlend();
        return Vector3.Slerp(orbitDirection, Vector3.up, blend).normalized;
    }

    private float GetDesiredFieldOfView()
    {
        float blend = GetTopDownBlend();
        return Mathf.Lerp(baseFieldOfView, topDownBaseFieldOfView, blend);
    }

    private Vector3 GetDesiredCameraPosition(Vector3 center)
    {
        float blend = GetTopDownBlend();
        Vector3 currentDirection = GetCurrentOrbitDirection();
        Vector3 heightOffset = Vector3.up * (topDownHeightBoost * blend);
        return center + currentDirection * targetDistance + heightOffset;
    }

    private Quaternion GetPanReferenceRotation()
    {
        Vector3 center = GetCurrentCenter();
        Vector3 desiredPosition = GetDesiredCameraPosition(center);
        return GetDesiredCameraRotation(center, desiredPosition);
    }

    private Quaternion GetDesiredCameraRotation(Vector3 center, Vector3 cameraPosition)
    {
        Vector3 forward = center - cameraPosition;
        if (forward.sqrMagnitude <= 0.0001f)
            return transform.rotation;

        forward.Normalize();

        Vector3 upReference = GetRotationUpReference(forward);
        return Quaternion.LookRotation(forward, upReference);
    }

    private Vector3 GetRotationUpReference(Vector3 forward)
    {
        Vector3 worldUpProjected = Vector3.ProjectOnPlane(Vector3.up, forward);
        Vector3 topDownUpProjected = Vector3.ProjectOnPlane(TopDownScreenUpFallback, forward);

        bool hasWorldUp = worldUpProjected.sqrMagnitude > 0.0001f;
        bool hasTopDownUp = topDownUpProjected.sqrMagnitude > 0.0001f;

        if (hasWorldUp && hasTopDownUp)
        {
            float blend = GetTopDownBlend();
            return Vector3.Slerp(
                worldUpProjected.normalized,
                topDownUpProjected.normalized,
                blend
            ).normalized;
        }

        if (hasWorldUp)
            return worldUpProjected.normalized;

        if (hasTopDownUp)
            return topDownUpProjected.normalized;

        Vector3 rightProjected = Vector3.ProjectOnPlane(Vector3.right, forward);
        if (rightProjected.sqrMagnitude > 0.0001f)
            return rightProjected.normalized;

        return Vector3.forward;
    }

    private void ApplyMovement()
    {
        Vector3 center = GetCurrentCenter();
        Vector3 desiredPosition = GetDesiredCameraPosition(center);

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            config.baseMoveLerpSpeed * Time.deltaTime
        );

        Quaternion desiredRotation = GetDesiredCameraRotation(center, desiredPosition);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            config.baseMoveLerpSpeed * Time.deltaTime
        );

        if (cam != null)
        {
            cam.fieldOfView = Mathf.Lerp(
                cam.fieldOfView,
                GetDesiredFieldOfView(),
                config.baseMoveLerpSpeed * Time.deltaTime
            );
        }
    }

    private bool ShouldBlockRawInput()
    {
        return inputLocked || IsPointerOverUI();
    }

    private bool IsPanPressed()
    {
        return !ShouldBlockRawInput() && panButtonAction.IsPressed();
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}