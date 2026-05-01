using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Owns normalized zoom intent and swaps between map, top-down base, and base orbit views.
/// </summary>
public class ViewManager : MonoBehaviour
{
    public enum ViewState
    {
        Map,
        Cloud,
        Top,
        Base,
        Transitioning
    }

    private enum CameraMode
    {
        Map,
        Base
    }

    [Header("Optional Transition Material")]
    [SerializeField] private Material zoomBlurMaterial;
    [SerializeField] private WorldCloudTransition worldCloudTransition;

    [Header("Controllers")]
    [SerializeField] private CameraController cameraController;
    [SerializeField] private BaseCameraController baseCameraController;

    [Header("Layers")]
    [SerializeField] private LayerMask mapLayer;
    [SerializeField] private LayerMask baseLayer;

    [Header("Input Actions")]
    [SerializeField] private InputAction scrollAction = new InputAction(
        "Unified Zoom Scroll",
        InputActionType.Value,
        "<Mouse>/scroll/y"
    );

    [Header("Game State")]
    [SerializeField] private GameState gameState;

    private const float BoundaryEpsilon = 0.0001f;
    private const float HandoffBoundarySlack = 0.01f;
    private const float CloudApproachMapRangeFraction = 0.35f;
    private const float CloudApproachMaxProgress = 0.22f;
    private static readonly int TransitionStrengthID = Shader.PropertyToID("_ZoomBlurStrength");
    private static readonly int TransitionProgressID = Shader.PropertyToID("_TransitionProgress");
    private static readonly int TransitionDirectionID = Shader.PropertyToID("_TransitionDirection");

    private Camera cam;
    private ViewState state = ViewState.Map;
    private CameraMode activeMode = CameraMode.Map;
    private float savedCamZ;

    private bool hasZoomState;
    private float zoomCurrentNormalized;
    private float zoomTargetNormalized;
    private float zoomVelocity;

    private bool handoffActive;
    private bool handoffToBase;
    private bool handoffSwapped;
    private float handoffElapsed;

    public ViewState CurrentState => state;
    public bool HasBaseZoomTargets => BuildZoomDomain().HasBase;
    public bool IsMapInteractionView => state == ViewState.Map;
    public bool IsBaseInteractionView => state == ViewState.Top || state == ViewState.Base;
    public bool IsGameplayInteractionBlocked => state == ViewState.Transitioning;

    private CameraZoomConfig ActiveConfig => cameraController != null ? cameraController.Config : null;

    private struct ZoomDomain
    {
        public bool HasBase;
        public float BaseMinDistance;
        public float BaseMaxDistance;
        public float BaseWaypointDistance;
        public float TopViewDistance;
        public float MinMapZoom;
        public float MapSegmentStartZoom;
        public float MapWaypointZoom;
        public float MapMaxZoom;
        public float CloudRange;
        public float BaseRange;
        public float MapRange;
        public float TotalRange;

        public bool IsValid => TotalRange > 0.0001f;
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();

        if (cameraController == null || baseCameraController == null)
        {
            Debug.LogError("ViewManager: Assign cameraController and baseCameraController.", this);
            enabled = false;
            return;
        }

        if (ActiveConfig == null)
        {
            Debug.LogError("ViewManager: CameraController is missing CameraZoomConfig.", this);
            enabled = false;
            return;
        }

        savedCamZ = cam.transform.position.z;
        EnsureWorldCloudTransition();
        SetBaseViewReadyForBirds(false);
        SetTransitionEffect(0f, 0f, 1f);

        ZoomDomain domain = BuildZoomDomain();
        float mapNormalized = GetNormalizedForMapZoom(domain, domain.MapWaypointZoom);
        zoomCurrentNormalized = mapNormalized;
        zoomTargetNormalized = mapNormalized;
        hasZoomState = true;

        EnterMapMode(ActiveConfig, domain, immediate: true);
    }

    private void OnEnable()
    {
        scrollAction.Enable();
    }

    private void OnDisable()
    {
        scrollAction.Disable();
        SetTransitionEffect(0f, 0f, 1f);

        if (worldCloudTransition != null)
            worldCloudTransition.SetMapCloudsVisible(false);
    }

    private void Update()
    {
        CameraZoomConfig config = ActiveConfig;
        if (config == null)
            return;

        ZoomDomain domain = BuildZoomDomain();
        if (!domain.IsValid)
            return;

        EnsureZoomState(domain);
        HandleScrollInput(config);

        zoomCurrentNormalized = Mathf.SmoothDamp(
            zoomCurrentNormalized,
            zoomTargetNormalized,
            ref zoomVelocity,
            config.buttonTravelDuration,
            Mathf.Infinity,
            Time.deltaTime
        );
        zoomCurrentNormalized = Mathf.Clamp01(zoomCurrentNormalized);

        UpdateHandoff(domain, config);
        ApplyZoomToActiveCamera(domain, config);
        UpdateCloudBandEffect(domain, config);
        UpdateStateFromCurrent(domain);

        if (gameState != null && gameState.IsRunEnded)
            LockControllers(true);
    }

    public float GetZoomNormalized()
    {
        ZoomDomain domain = BuildZoomDomain();
        if (!domain.IsValid)
            return 1f;

        EnsureZoomState(domain);
        return zoomTargetNormalized;
    }

    public float GetBaseWaypointNormalized()
    {
        ZoomDomain domain = BuildZoomDomain();
        if (!domain.HasBase || !domain.IsValid)
            return GetZoomNormalized();

        float baseUnits = domain.BaseWaypointDistance - domain.BaseMinDistance;
        return UnitsToNormalized(domain, baseUnits);
    }

    public float GetTopViewWaypointNormalized()
    {
        ZoomDomain domain = BuildZoomDomain();
        if (!domain.HasBase || !domain.IsValid)
            return GetZoomNormalized();

        float baseUnits = domain.TopViewDistance - domain.BaseMinDistance;
        return UnitsToNormalized(domain, baseUnits);
    }

    public float GetMapWaypointNormalized()
    {
        ZoomDomain domain = BuildZoomDomain();
        if (!domain.IsValid)
            return 1f;

        float mapUnits = domain.MapWaypointZoom - domain.MapSegmentStartZoom;
        return UnitsToNormalized(domain, domain.BaseRange + mapUnits);
    }

    public void SetZoomNormalized(float normalized)
    {
        if (gameState != null && gameState.IsRunEnded)
            return;

        ZoomDomain domain = BuildZoomDomain();
        if (!domain.IsValid)
            return;

        EnsureZoomState(domain);
        zoomTargetNormalized = Mathf.Clamp01(normalized);
    }

    public void SnapToMapView()
    {
        CameraZoomConfig config = ActiveConfig;
        if (config == null)
            return;

        ZoomDomain domain = BuildZoomDomain();
        if (!domain.IsValid)
            return;

        float mapNormalized = GetMapWaypointNormalized();
        zoomTargetNormalized = mapNormalized;
        zoomCurrentNormalized = mapNormalized;
        zoomVelocity = 0f;
        hasZoomState = true;

        handoffActive = false;
        handoffSwapped = false;
        SetBaseViewReadyForBirds(false);
        SetTransitionEffect(0f, 0f, 1f);
        EnterMapMode(config, domain, immediate: true);
        UpdateStateFromCurrent(domain);
    }

    private void HandleScrollInput(CameraZoomConfig config)
    {
        if (gameState != null && gameState.IsRunEnded)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        float rawScroll = scrollAction.ReadValue<float>();
        float scroll = NormalizeScrollDelta(rawScroll);
        if (Mathf.Approximately(scroll, 0f))
            return;

        SetZoomNormalized(zoomTargetNormalized - scroll * config.normalizedScrollSpeed);
    }

    private static float NormalizeScrollDelta(float rawScroll)
    {
        if (Mathf.Approximately(rawScroll, 0f))
            return 0f;

        // Some platforms report wheel ticks as +/-120, while others report +/-1.
        return Mathf.Abs(rawScroll) > 10f ? rawScroll / 120f : rawScroll;
    }

    private void EnsureZoomState(ZoomDomain domain)
    {
        if (hasZoomState)
        {
            zoomCurrentNormalized = Mathf.Clamp01(zoomCurrentNormalized);
            zoomTargetNormalized = Mathf.Clamp01(zoomTargetNormalized);
            return;
        }

        zoomCurrentNormalized = GetLiveZoomNormalized(domain);
        zoomTargetNormalized = zoomCurrentNormalized;
        zoomVelocity = 0f;
        hasZoomState = true;
    }

    private float GetLiveZoomNormalized(ZoomDomain domain)
    {
        if (activeMode == CameraMode.Base && domain.HasBase && baseCameraController.enabled)
            return GetNormalizedForBaseDistance(domain, baseCameraController.TargetDistance);

        float mapZoom = cameraController != null ? cameraController.TargetZoom : domain.MapWaypointZoom;
        return GetNormalizedForMapZoom(domain, mapZoom);
    }

    private void ApplyZoomToActiveCamera(ZoomDomain domain, CameraZoomConfig config)
    {
        bool wantsBaseSegment = WantsBaseSegment(domain, zoomCurrentNormalized);
        bool targetWantsBaseSegment = WantsBaseSegment(domain, zoomTargetNormalized);
        bool readyForBaseHandoff = wantsBaseSegment ||
            (targetWantsBaseSegment && IsNearBaseBoundary(domain, zoomCurrentNormalized));

        if (!handoffActive)
        {
            if (readyForBaseHandoff && targetWantsBaseSegment && activeMode == CameraMode.Map)
                BeginHandoff(toBase: true, domain, config);
            else if (!wantsBaseSegment && !targetWantsBaseSegment && activeMode == CameraMode.Base)
                BeginHandoff(toBase: false, domain, config);
        }

        if (activeMode == CameraMode.Map)
        {
            float mapZoom = GetMapZoomForNormalized(domain, zoomCurrentNormalized);
            cameraController.SetZoomTarget(mapZoom);
            return;
        }

        if (!domain.HasBase)
        {
            EnterMapMode(config, domain, immediate: true);
            return;
        }

        float baseDistance = GetBaseDistanceForNormalized(domain, zoomCurrentNormalized);
        baseCameraController.SetDistanceTarget(baseDistance);
    }

    private void BeginHandoff(bool toBase, ZoomDomain domain, CameraZoomConfig config)
    {
        if (toBase && !domain.HasBase)
            return;

        handoffActive = true;
        handoffToBase = toBase;
        handoffSwapped = false;
        handoffElapsed = 0f;
        state = ViewState.Transitioning;
        LockControllers(true);
        SetBaseViewReadyForBirds(false);

        if (toBase)
        {
            cameraController.SetZoomTarget(domain.MinMapZoom);
        }
        else
        {
            baseCameraController.SetDistanceTarget(domain.BaseMaxDistance);
        }
    }

    private void UpdateHandoff(ZoomDomain domain, CameraZoomConfig config)
    {
        if (!handoffActive)
            return;

        bool targetWantsBase = WantsBaseSegment(domain, zoomTargetNormalized);
        bool targetReversed = handoffToBase ? !targetWantsBase : targetWantsBase;

        if (targetReversed && !handoffSwapped)
        {
            FinishHandoff(domain);
            return;
        }

        if (targetReversed && handoffSwapped)
        {
            FinishHandoff(domain);
            BeginHandoff(!handoffToBase, domain, config);
            return;
        }

        handoffElapsed += Time.deltaTime;
        float duration = Mathf.Max(0.01f, config.mapToTopHandoffDuration);
        float t = Mathf.Clamp01(handoffElapsed / duration);
        float direction = handoffToBase ? 1f : -1f;
        float progress = handoffToBase
            ? Mathf.Lerp(0.82f, 1f, t)
            : Mathf.Lerp(1f, 0.82f, t);
        float holdUntilSwap = Mathf.Clamp01(config.swapPoint);
        float strength = handoffToBase
            ? Mathf.Lerp(config.transitionPulseStrength, 0f, SmoothRange(holdUntilSwap, 1f, t))
            : Mathf.Lerp(0f, config.transitionPulseStrength, SmoothRange(0f, holdUntilSwap, t));
        SetTransitionEffect(progress, strength, direction);

        if (!handoffSwapped && t >= config.swapPoint)
        {
            handoffSwapped = true;

            if (handoffToBase)
            {
                if (!EnterBaseMode(config, domain))
                {
                    zoomTargetNormalized = GetMapWaypointNormalized();
                    FinishHandoff(domain);
                    EnterMapMode(config, domain, immediate: true);
                    return;
                }
            }
            else
            {
                EnterMapMode(config, domain, immediate: true);
            }
        }

        if (t >= 1f)
            FinishHandoff(domain);
    }

    private void FinishHandoff(ZoomDomain domain)
    {
        handoffActive = false;
        handoffSwapped = false;
        SetTransitionEffect(0f, 0f, handoffToBase ? 1f : -1f);

        bool runEnded = gameState != null && gameState.IsRunEnded;
        LockControllers(runEnded);
        UpdateStateFromCurrent(domain);
    }

    private bool EnterBaseMode(CameraZoomConfig config, ZoomDomain domain)
    {
        ZoomAnchor anchor = cameraController.CurrentAnchor;
        if (anchor == null)
        {
            Debug.LogWarning("ViewManager: Cannot enter Top/Base view without an active ZoomAnchor.", this);
            return false;
        }

        float baseDistance = GetBaseDistanceForNormalized(domain, zoomCurrentNormalized);
        float normalBaseFieldOfView = anchor.GetBaseFieldOfView(config.defaultBaseFieldOfView);
        float topDownBaseFieldOfView = anchor.GetTopDownBaseFieldOfView(config.defaultTopDownBaseFieldOfView);
        float topDownHeightBoost = anchor.GetTopDownHeightBoost(config.defaultTopDownHeightBoost);

        cameraController.enabled = false;
        baseCameraController.enabled = false;

        cam.orthographic = false;
        ShowBaseOnly();

        baseCameraController.Initialize(
            config,
            anchor.transform.position,
            anchor.GetBaseCameraDirection(),
            baseDistance,
            anchor.baseMinDistance,
            anchor.baseMaxDistance,
            anchor.baseTopDownTransitionStartDistance,
            anchor.baseTopDownTransitionCompleteDistance,
            anchor.GetReturnToMapDistance(),
            normalBaseFieldOfView,
            topDownBaseFieldOfView,
            topDownHeightBoost
        );

        baseCameraController.enabled = true;
        baseCameraController.SnapToCurrentState(cam.transform);
        activeMode = CameraMode.Base;
        SetBaseViewReadyForBirds(true);
        return true;
    }

    private void EnterMapMode(CameraZoomConfig config, ZoomDomain domain, bool immediate)
    {
        baseCameraController.enabled = false;
        activeMode = CameraMode.Map;
        SetBaseViewReadyForBirds(false);

        cam.orthographic = true;
        cam.transform.rotation = Quaternion.identity;
        ShowMapOnly();

        Vector3 returnPosition = GetMapReturnPosition();
        cam.transform.position = returnPosition;

        float mapZoom = GetMapZoomForNormalized(domain, zoomCurrentNormalized);
        if (immediate)
            cameraController.SetZoomImmediate(mapZoom);
        else
            cameraController.SetZoomTarget(mapZoom);

        cameraController.enabled = true;
        LockControllers(gameState != null && gameState.IsRunEnded);
    }

    private Vector3 GetMapReturnPosition()
    {
        ZoomAnchor anchor = cameraController.CurrentAnchor;
        Vector3 returnPosition = anchor != null ? anchor.transform.position : cam.transform.position;
        returnPosition.z = savedCamZ;
        return returnPosition;
    }

    private void UpdateStateFromCurrent(ZoomDomain domain)
    {
        if (handoffActive)
        {
            state = ViewState.Transitioning;
            return;
        }

        if (activeMode == CameraMode.Map || !domain.HasBase)
        {
            state = IsInCloudBand(domain, zoomCurrentNormalized)
                ? ViewState.Cloud
                : ViewState.Map;
            return;
        }

        float baseDistance = GetBaseDistanceForNormalized(domain, zoomCurrentNormalized);
        state = baseDistance >= domain.TopViewDistance - 0.01f
            ? ViewState.Top
            : ViewState.Base;
    }

    private ZoomDomain BuildZoomDomain()
    {
        CameraZoomConfig config = ActiveConfig;
        if (config == null)
            return default;

        bool hasBase = cameraController != null && cameraController.CurrentAnchor != null;
        ZoomAnchor anchor = hasBase ? cameraController.CurrentAnchor : null;
        float mapSegmentStart = hasBase
            ? GetMapHandoffZoom(config, anchor)
            : config.minMapZoom;

        ZoomDomain domain = new ZoomDomain
        {
            HasBase = hasBase,
            MinMapZoom = config.minMapZoom,
            MapSegmentStartZoom = mapSegmentStart,
            MapWaypointZoom = Mathf.Clamp(config.defaultMapZoom, mapSegmentStart, config.maxMapZoom),
            MapMaxZoom = config.maxMapZoom
        };

        if (!hasBase)
        {
            domain.CloudRange = 0f;
            domain.BaseRange = 0f;
            domain.MapRange = Mathf.Max(0f, domain.MapMaxZoom - domain.MapSegmentStartZoom);
            domain.TotalRange = domain.MapRange;
            return domain;
        }

        domain.BaseMinDistance = Mathf.Max(0.1f, anchor.baseMinDistance);
        domain.BaseMaxDistance = Mathf.Max(domain.BaseMinDistance, anchor.baseMaxDistance);
        domain.BaseWaypointDistance = anchor.GetBaseWaypointDistance(config.defaultBaseDistance);
        domain.TopViewDistance = Mathf.Clamp(
            anchor.baseTopDownTransitionCompleteDistance,
            domain.BaseMinDistance,
            domain.BaseMaxDistance
        );

        domain.BaseRange = Mathf.Max(0f, domain.BaseMaxDistance - domain.BaseMinDistance);
        domain.CloudRange = Mathf.Max(0f, config.mapToTopCloudTravelRange);
        domain.MapRange = Mathf.Max(0f, domain.MapMaxZoom - domain.MapSegmentStartZoom);
        domain.TotalRange = domain.BaseRange + domain.CloudRange + domain.MapRange;

        return domain;
    }

    private static float GetMapHandoffZoom(CameraZoomConfig config, ZoomAnchor anchor)
    {
        float focusedZoom = anchor.GetFocusedMapZoom(config.defaultFocusedMapZoom);
        float handoffZoom = Mathf.Min(config.mapToBaseTransitionZoom, focusedZoom);
        return Mathf.Clamp(handoffZoom, config.minMapZoom, config.maxMapZoom);
    }

    private static bool WantsBaseSegment(ZoomDomain domain, float normalized)
    {
        if (!domain.HasBase)
            return false;

        float units = Mathf.Clamp01(normalized) * domain.TotalRange;
        return units <= domain.BaseRange + BoundaryEpsilon;
    }

    private static bool IsNearBaseBoundary(ZoomDomain domain, float normalized)
    {
        if (!domain.HasBase || !domain.IsValid)
            return false;

        float baseBoundaryNormalized = GetTopBoundaryNormalized(domain);
        return Mathf.Abs(Mathf.Clamp01(normalized) - baseBoundaryNormalized) <= HandoffBoundarySlack;
    }

    private static float GetTopBoundaryNormalized(ZoomDomain domain)
    {
        if (!domain.IsValid)
            return 0f;

        return Mathf.Clamp01(domain.BaseRange / domain.TotalRange);
    }

    private static bool IsInCloudBand(ZoomDomain domain, float normalized)
    {
        if (!domain.HasBase || domain.CloudRange <= 0.0001f || !domain.IsValid)
            return false;

        float units = Mathf.Clamp01(normalized) * domain.TotalRange;
        return units > domain.BaseRange + BoundaryEpsilon &&
               units < domain.BaseRange + domain.CloudRange - BoundaryEpsilon;
    }

    private static float GetCloudBandProgress(ZoomDomain domain, float normalized)
    {
        if (!domain.HasBase || domain.CloudRange <= 0.0001f || !domain.IsValid)
            return 0f;

        float units = Mathf.Clamp01(normalized) * domain.TotalRange;
        float cloudStartUnits = domain.BaseRange + domain.CloudRange;
        return Mathf.Clamp01(Mathf.InverseLerp(cloudStartUnits, domain.BaseRange, units));
    }

    private static bool IsInCloudApproach(ZoomDomain domain, float normalized)
    {
        if (!domain.HasBase || domain.CloudRange <= 0.0001f || domain.MapRange <= 0.0001f || !domain.IsValid)
            return false;

        float units = Mathf.Clamp01(normalized) * domain.TotalRange;
        float cloudStartUnits = domain.BaseRange + domain.CloudRange;
        float approachRange = GetCloudApproachRange(domain);
        return units >= cloudStartUnits &&
               units <= cloudStartUnits + approachRange + BoundaryEpsilon;
    }

    private static float GetCloudApproachProgress(ZoomDomain domain, float normalized)
    {
        if (!IsInCloudApproach(domain, normalized))
            return 0f;

        float units = Mathf.Clamp01(normalized) * domain.TotalRange;
        float cloudStartUnits = domain.BaseRange + domain.CloudRange;
        float approachRange = GetCloudApproachRange(domain);
        float progress = Mathf.InverseLerp(cloudStartUnits + approachRange, cloudStartUnits, units);
        return Mathf.Lerp(0.02f, CloudApproachMaxProgress, Mathf.SmoothStep(0f, 1f, progress));
    }

    private static float GetCloudApproachRange(ZoomDomain domain)
    {
        return Mathf.Min(
            domain.MapRange,
            Mathf.Max(0.001f, domain.CloudRange * CloudApproachMapRangeFraction)
        );
    }

    private static float GetBaseDistanceForNormalized(ZoomDomain domain, float normalized)
    {
        float units = Mathf.Clamp01(normalized) * domain.TotalRange;
        return Mathf.Clamp(domain.BaseMinDistance + units, domain.BaseMinDistance, domain.BaseMaxDistance);
    }

    private static float GetMapZoomForNormalized(ZoomDomain domain, float normalized)
    {
        float units = Mathf.Clamp01(normalized) * domain.TotalRange;
        if (domain.HasBase && units <= domain.BaseRange + domain.CloudRange)
        {
            if (domain.CloudRange <= 0.0001f || units <= domain.BaseRange)
                return domain.MinMapZoom;

            float cloudProgress = Mathf.InverseLerp(
                domain.BaseRange + domain.CloudRange,
                domain.BaseRange,
                units
            );
            float easedProgress = SmoothRange(0f, 1f, cloudProgress);
            return Mathf.Lerp(domain.MapSegmentStartZoom, domain.MinMapZoom, easedProgress);
        }

        float mapUnits = domain.HasBase
            ? Mathf.Max(0f, units - domain.BaseRange - domain.CloudRange)
            : units;
        return Mathf.Clamp(
            domain.MapSegmentStartZoom + mapUnits,
            domain.MapSegmentStartZoom,
            domain.MapMaxZoom
        );
    }

    private static float GetNormalizedForBaseDistance(ZoomDomain domain, float distance)
    {
        float baseUnits = Mathf.Clamp(distance, domain.BaseMinDistance, domain.BaseMaxDistance)
            - domain.BaseMinDistance;

        return UnitsToNormalized(domain, baseUnits);
    }

    private static float GetNormalizedForMapZoom(ZoomDomain domain, float mapZoom)
    {
        float mapUnits = Mathf.Clamp(mapZoom, domain.MapSegmentStartZoom, domain.MapMaxZoom)
            - domain.MapSegmentStartZoom;

        return UnitsToNormalized(domain, domain.BaseRange + domain.CloudRange + mapUnits);
    }

    private static float UnitsToNormalized(ZoomDomain domain, float units)
    {
        if (!domain.IsValid)
            return 1f;

        return Mathf.Clamp01(units / domain.TotalRange);
    }

    private static float SmoothRange(float min, float max, float value)
    {
        if (max <= min)
            return value >= max ? 1f : 0f;

        float t = Mathf.Clamp01((value - min) / (max - min));
        return t * t * (3f - 2f * t);
    }

    private void UpdateCloudBandEffect(ZoomDomain domain, CameraZoomConfig config)
    {
        if (handoffActive)
            return;

        if (activeMode == CameraMode.Map && IsInCloudBand(domain, zoomCurrentNormalized))
        {
            float progress = Mathf.Lerp(
                CloudApproachMaxProgress,
                1f,
                GetCloudBandProgress(domain, zoomCurrentNormalized)
            );
            float strength = Mathf.Lerp(0.35f, 1f, Mathf.SmoothStep(0f, 1f, progress))
                * config.transitionPulseStrength;
            float direction = zoomTargetNormalized <= zoomCurrentNormalized ? 1f : -1f;
            SetTransitionEffect(progress, strength, direction);
            return;
        }

        if (activeMode == CameraMode.Map && IsInCloudApproach(domain, zoomCurrentNormalized))
        {
            float progress = GetCloudApproachProgress(domain, zoomCurrentNormalized);
            float strength = Mathf.Lerp(0.12f, 0.35f, Mathf.SmoothStep(0f, 1f, progress / CloudApproachMaxProgress))
                * config.transitionPulseStrength;
            float direction = zoomTargetNormalized <= zoomCurrentNormalized ? 1f : -1f;
            SetTransitionEffect(progress, strength, direction);
            return;
        }

        SetTransitionEffect(0f, 0f, activeMode == CameraMode.Map ? 1f : -1f);
    }

    private void LockControllers(bool locked)
    {
        if (cameraController != null)
            cameraController.LockInput(locked || activeMode != CameraMode.Map || handoffActive);

        if (baseCameraController != null)
            baseCameraController.LockInput(locked || activeMode != CameraMode.Base || handoffActive);
    }

    private void SetTransitionEffect(float progress, float strength, float direction)
    {
        if (zoomBlurMaterial != null)
        {
            zoomBlurMaterial.SetFloat(TransitionProgressID, Mathf.Clamp01(progress));
            zoomBlurMaterial.SetFloat(TransitionStrengthID, Mathf.Clamp01(strength));
            zoomBlurMaterial.SetFloat(TransitionDirectionID, direction >= 0f ? 1f : -1f);
        }

        if (worldCloudTransition != null)
            worldCloudTransition.SetTransition(progress, strength, direction);
    }

    private void SetBaseViewReadyForBirds(bool ready)
    {
        if (worldCloudTransition != null)
            worldCloudTransition.SetBaseViewReadyForBirds(ready);
    }

    private void EnsureWorldCloudTransition()
    {
        if (worldCloudTransition == null)
            worldCloudTransition = GetComponent<WorldCloudTransition>();

        if (worldCloudTransition == null)
        {
            Debug.LogWarning("ViewManager: Add a WorldCloudTransition component to the camera for cloud cover and cloud dive effects.", this);
            return;
        }

        worldCloudTransition.Initialize(cam);
    }

    private void ShowMapOnly()
    {
        cam.cullingMask |= mapLayer;
        cam.cullingMask &= ~baseLayer;

        if (worldCloudTransition != null)
            worldCloudTransition.SetMapCloudsVisible(true);
    }

    private void ShowBaseOnly()
    {
        cam.cullingMask |= baseLayer;
        cam.cullingMask &= ~mapLayer;

        if (worldCloudTransition != null)
            worldCloudTransition.SetMapCloudsVisible(false);
    }
}
