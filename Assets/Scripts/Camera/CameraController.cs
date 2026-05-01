using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Handles map-view camera zoom and pan in orthographic mode.
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraZoomConfig config;
    [SerializeField] private ZoomAnchor currentAnchor;

    [Header("Input Actions")]
    [SerializeField] private InputAction panButtonAction = new InputAction(
        "PanButton", InputActionType.Button, "<Mouse>/middleButton"
    );

    [SerializeField] private InputAction panDeltaAction = new InputAction(
        "PanDelta", InputActionType.Value, "<Pointer>/delta"
    );

    [Header("Starting State")]
    [SerializeField] private bool startInMapView = true;

    private Camera cam;
    private Vector3 mapCenter;
    private Vector3 homePosition;
    private float targetZoom;
    private Vector3 panOffset;
    private bool isPanning;
    private bool isInBaseView;
    private bool inputLocked;

    public ZoomAnchor CurrentAnchor => currentAnchor;
    public CameraZoomConfig Config => config;
    public float TargetZoom => targetZoom;
    public float CurrentZoom => cam != null ? cam.orthographicSize : targetZoom;
    public float MinZoom => config != null ? config.minMapZoom : 0.01f;
    public float MaxZoom => config != null ? config.maxMapZoom : 0.01f;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        if (config == null)
        {
            Debug.LogError("CameraController: No CameraZoomConfig assigned.", this);
            enabled = false;
            return;
        }

        mapCenter = transform.position;

        if (startInMapView)
        {
            targetZoom = config.defaultMapZoom;
            cam.orthographicSize = targetZoom;
            homePosition = mapCenter;
            isInBaseView = false;
        }
        else
        {
            GoToBaseView();
            cam.orthographicSize = targetZoom;
            transform.position = new Vector3(homePosition.x, homePosition.y, transform.position.z);
        }
    }

    private void OnEnable()
    {
        panButtonAction.Enable();
        panDeltaAction.Enable();
    }

    private void OnDisable()
    {
        panButtonAction.Disable();
        panDeltaAction.Disable();
    }

    private void Update()
    {
        HandlePanInput();
        UpdateHomePosition();
        ApplySpringBack();
        ApplySmoothMovement();
    }

    public void GoToBaseView()
    {
        if (currentAnchor == null)
        {
            Debug.LogWarning("CameraController: No ZoomAnchor assigned.", this);
            return;
        }

        isInBaseView = true;
        targetZoom = GetAnchorFocusZoom();
        panOffset = Vector3.zero;
    }

    public void GoToMapView()
    {
        isInBaseView = false;
        targetZoom = config.defaultMapZoom;
        panOffset = Vector3.zero;
    }

    public void SetAnchor(ZoomAnchor newAnchor)
    {
        currentAnchor = newAnchor;

        if (isInBaseView && currentAnchor != null)
            GoToBaseView();
    }

    public void SetConfig(CameraZoomConfig newConfig)
    {
        if (newConfig == null)
        {
            Debug.LogWarning("CameraController: Ignored null CameraZoomConfig.", this);
            return;
        }

        config = newConfig;

        if (isInBaseView && currentAnchor != null)
        {
            targetZoom = GetAnchorFocusZoom();
        }
        else
        {
            targetZoom = ClampZoom(targetZoom);

            if (cam != null && cam.orthographic)
                cam.orthographicSize = ClampZoom(cam.orthographicSize);
        }
    }

    public void LockInput(bool locked)
    {
        inputLocked = locked;
    }

    public void SetZoomTarget(float zoom)
    {
        if (config == null) return;
        targetZoom = ClampZoom(zoom);
    }

    public void SetZoomImmediate(float zoom)
    {
        if (config == null) return;

        targetZoom = ClampZoom(zoom);

        if (cam != null)
            cam.orthographicSize = targetZoom;

        UpdateHomePosition();
        SnapToTargetPosition();
    }

    private void HandlePanInput()
    {
        if (ShouldBlockRawInput())
        {
            isPanning = false;
            return;
        }

        isPanning = panButtonAction.IsPressed();
        if (!isPanning) return;

        Vector2 delta = panDeltaAction.ReadValue<Vector2>();

        Vector3 panDelta = new Vector3(-delta.x, -delta.y, 0f)
            * config.mapPanSpeed * Time.deltaTime;

        panOffset += panDelta;
        panOffset = Vector3.ClampMagnitude(panOffset, config.mapMaxPanDistance);
    }

    private void UpdateHomePosition()
    {
        if (currentAnchor == null)
        {
            homePosition = mapCenter;
            return;
        }

        float focusedMapZoom = GetAnchorFocusZoom();
        float blend = Mathf.InverseLerp(config.defaultMapZoom, focusedMapZoom, targetZoom);

        Vector3 anchorPosition = currentAnchor.transform.position;
        anchorPosition.z = mapCenter.z;

        homePosition = Vector3.Lerp(mapCenter, anchorPosition, blend);
    }

    private void ApplySpringBack()
    {
        if (!isPanning && panOffset.sqrMagnitude > 0.001f)
        {
            panOffset = Vector3.Lerp(
                panOffset,
                Vector3.zero,
                config.mapPanSpringBackSpeed * Time.deltaTime
            );
        }
    }

    private void ApplySmoothMovement()
    {
        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize,
            targetZoom,
            config.mapZoomLerpSpeed * Time.deltaTime
        );

        Vector3 desiredPosition = homePosition + panOffset;
        desiredPosition.z = transform.position.z;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            config.mapPositionLerpSpeed * Time.deltaTime
        );
    }

    private void SnapToTargetPosition()
    {
        Vector3 desiredPosition = homePosition + panOffset;
        desiredPosition.z = transform.position.z;
        transform.position = desiredPosition;
    }

    private float ClampZoom(float zoom)
    {
        return Mathf.Clamp(zoom, config.minMapZoom, config.maxMapZoom);
    }

    private float GetAnchorFocusZoom()
    {
        if (currentAnchor == null || config == null)
            return targetZoom;

        float focusedMapZoom = currentAnchor.GetFocusedMapZoom(config.defaultFocusedMapZoom);
        float handoffMapZoom = Mathf.Min(focusedMapZoom, config.mapToBaseTransitionZoom);
        return ClampZoom(handoffMapZoom);
    }

    private bool ShouldBlockRawInput()
    {
        return inputLocked || IsPointerOverUI();
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}