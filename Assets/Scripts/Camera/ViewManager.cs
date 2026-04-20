using System.Collections;
using UnityEngine;

/// <summary>
/// Swaps between orthographic map view and perspective base view.
/// Also exposes a unified zoom API for HUD controls.
/// </summary>
public class ViewManager : MonoBehaviour
{
    public enum ViewState
    {
        Map,
        Base,
        Transitioning
    }

    [Header("Zoom Blur Material")]
    [SerializeField] private Material zoomBlurMaterial;

    [Header("Controllers")]
    [SerializeField] private CameraController cameraController;
    [SerializeField] private BaseCameraController baseCameraController;

    [Header("Layers")]
    [SerializeField] private LayerMask mapLayer;
    [SerializeField] private LayerMask baseLayer;

    [Header("Game State")]
    [SerializeField] private GameState gameState;

    private Camera cam;
    private ViewState state = ViewState.Map;
    private bool hasSwapped;
    private float savedCamZ;
    private bool hasPendingZoomTarget;
    private float pendingZoomNormalized;
    private float transitionStartNormalized;
    private float transitionEndNormalized;
    private float transitionProgress;

    private static readonly int BlurStrengthID = Shader.PropertyToID("_ZoomBlurStrength");

    public ViewState CurrentState => state;
    public bool HasBaseZoomTargets => BuildZoomDomain().HasBase;

    private CameraZoomConfig ActiveConfig => cameraController != null ? cameraController.Config : null;

    private struct ZoomDomain
    {
        public bool HasBase;
        public float BaseMinDistance;
        public float BaseMaxDistance;
        public float BaseWaypointDistance;
        public float TopViewDistance;
        public float MapSegmentStartZoom;
        public float MapWaypointZoom;
        public float MapMaxZoom;
        public float BaseRange;
        public float MapRange;
        public float TotalRange;
        

        public bool IsValid => TotalRange > 0.0001f;
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();

        if (cameraController == null || baseCameraController == null || zoomBlurMaterial == null)
        {
            Debug.LogError("ViewManager: Assign cameraController, baseCameraController, and zoomBlurMaterial.", this);
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
        SetBlur(0f);
        ShowMapOnly();
    }

    private void LateUpdate()
    {
        if (state == ViewState.Transitioning) return;
        if (gameState != null && gameState.IsRunEnded) return;

        CameraZoomConfig config = ActiveConfig;
        if (config == null) return;

        if (state == ViewState.Map)
        {
            if (cameraController.CurrentAnchor != null &&
                cam.orthographicSize <= config.mapToBaseTransitionZoom)
            {
                StartCoroutine(RunTransition(toBase: true, config));
            }
        }
        else if (state == ViewState.Base)
        {
            if (baseCameraController.WantsToExitBase)
            {
                StartCoroutine(RunTransition(toBase: false, config));
            }
        }
    }

        private float GetLiveZoomNormalized()
        {
            ZoomDomain domain = BuildZoomDomain();
            if (!domain.IsValid)
                return 1f;

            if (state == ViewState.Base && domain.HasBase && baseCameraController.enabled)
                return GetNormalizedForBaseDistance(domain, baseCameraController.TargetDistance);

            float mapZoom = cameraController != null ? cameraController.TargetZoom : domain.MapWaypointZoom;
            return GetNormalizedForMapZoom(domain, mapZoom);
        }

    private float GetTransitionEndNormalized(bool toBase, CameraZoomConfig config)
    {
        if (hasPendingZoomTarget)
            return Mathf.Clamp01(pendingZoomNormalized);

        ZoomDomain domain = BuildZoomDomain();
        if (!domain.IsValid)
            return 1f;

        if (toBase)
            return GetNormalizedForBaseDistance(domain, GetRequestedBaseEntryDistance(config));

        return GetNormalizedForMapZoom(domain, config.defaultMapZoom);
    }

    private float GetRequestedBaseEntryDistance(CameraZoomConfig config)
    {
        ZoomDomain domain = BuildZoomDomain();

        if (domain.HasBase && hasPendingZoomTarget)
        {
            float units = Mathf.Clamp01(pendingZoomNormalized) * domain.TotalRange;
            if (units < domain.BaseRange)
                return Mathf.Clamp(
                    domain.BaseMinDistance + units,
                    domain.BaseMinDistance,
                    domain.BaseMaxDistance
                );
        }

        ZoomAnchor anchor = cameraController.CurrentAnchor;
        if (anchor == null)
            return config.defaultBaseDistance;

        return Mathf.Clamp(
            anchor.GetBaseStartDistance(config.defaultBaseDistance),
            domain.BaseMinDistance,
            domain.BaseMaxDistance
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

        return UnitsToNormalized(domain, domain.BaseRange + mapUnits);
    }

    public float GetZoomNormalized()
    {
        if (state == ViewState.Transitioning)
            return Mathf.Lerp(transitionStartNormalized, transitionEndNormalized, transitionProgress);

        return GetLiveZoomNormalized();
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

        hasPendingZoomTarget = true;
        pendingZoomNormalized = Mathf.Clamp01(normalized);

        if (state == ViewState.Transitioning)
            return;

        TryApplyPendingZoomTarget();
    }

    private IEnumerator RunTransition(bool toBase, CameraZoomConfig config)
    {
        transitionStartNormalized = GetLiveZoomNormalized();
        transitionEndNormalized = GetTransitionEndNormalized(toBase, config);
        transitionProgress = 0f;

        state = ViewState.Transitioning;
        hasSwapped = false;
        bool transitionSucceeded = true;

        if (toBase)
        {
            cameraController.LockInput(true);
            cameraController.GoToBaseView();
        }
        else
        {
            baseCameraController.LockInput(true);
        }

        float elapsed = 0f;

        while (elapsed < config.transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / config.transitionDuration);
            transitionProgress = t;
            float blur = config.blurCurve.Evaluate(t) * config.peakBlurStrength;
            SetBlur(blur);

            if (!hasSwapped && t >= config.swapPoint)
            {
                hasSwapped = true;

                if (toBase)
                    transitionSucceeded = SwapToBase(config);
                else
                    SwapToMap(config);

                if (!transitionSucceeded)
                    break;
            }

            yield return null;
        }
        transitionProgress = 1f;
        SetBlur(0f);

        if (!transitionSucceeded)
        {
            hasPendingZoomTarget = false;
            ShowMapOnly();
            cameraController.enabled = true;
            cameraController.LockInput(false);
            baseCameraController.enabled = false;
            state = ViewState.Map;
            yield break;
        }

        bool runEnded = gameState != null && gameState.IsRunEnded;

        if (toBase)
        {
            state = ViewState.Base;
            TryApplyPendingZoomTarget();

            if (!runEnded)
                baseCameraController.LockInput(false);
        }
        else
        {
            state = ViewState.Map;
            TryApplyPendingZoomTarget();

            if (!runEnded)
                cameraController.LockInput(false);
        }
    }

    private bool SwapToBase(CameraZoomConfig config)
    {
        ZoomAnchor anchor = cameraController.CurrentAnchor;
        if (anchor == null)
        {
            Debug.LogWarning("ViewManager: Cannot enter base view without an active ZoomAnchor.", this);
            ShowMapOnly();
            return false;
        }

        ShowBaseOnly();

        cameraController.enabled = false;
        cam.orthographic = false;

        float normalBaseFieldOfView = anchor.GetBaseFieldOfView(config.defaultBaseFieldOfView);
        float topDownBaseFieldOfView = anchor.GetTopDownBaseFieldOfView(config.defaultTopDownBaseFieldOfView);
        float topDownHeightBoost = anchor.GetTopDownHeightBoost(config.defaultTopDownHeightBoost);

        baseCameraController.Initialize(
            config,
            anchor.transform.position,
            anchor.GetBaseCameraDirection(),
            GetRequestedBaseEntryDistance(config),
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
        baseCameraController.LockInput(true);
        return true;
    }

    private void SwapToMap(CameraZoomConfig config)
    {
        ShowMapOnly();

        ZoomAnchor anchor = cameraController.CurrentAnchor;

        baseCameraController.enabled = false;

        cam.orthographic = true;
        cam.transform.rotation = Quaternion.identity;

        Vector3 returnPosition = anchor != null ? anchor.transform.position : cam.transform.position;
        returnPosition.z = savedCamZ;
        cam.transform.position = returnPosition;

        cam.orthographicSize = config.mapToBaseTransitionZoom;

        cameraController.GoToMapView();
        cameraController.enabled = true;
        cameraController.LockInput(true);
    }

    private void TryApplyPendingZoomTarget()
    {
        if (!hasPendingZoomTarget || state == ViewState.Transitioning)
            return;

        ZoomDomain domain = BuildZoomDomain();
        if (!domain.IsValid)
        {
            hasPendingZoomTarget = false;
            return;
        }

        float units = Mathf.Clamp01(pendingZoomNormalized) * domain.TotalRange;
        bool wantsBase = domain.HasBase && units < domain.BaseRange;

        if (wantsBase)
        {
            if (state != ViewState.Base)
            {
                CameraZoomConfig config = ActiveConfig;
                if (config != null)
                    StartCoroutine(RunTransition(toBase: true, config));

                return;
            }

            float targetDistance = domain.BaseMinDistance + units;
            baseCameraController.SetDistanceTarget(targetDistance);
            hasPendingZoomTarget = false;
            return;
        }

        float mapUnits = domain.HasBase ? Mathf.Max(0f, units - domain.BaseRange) : units;
        float targetMapZoom = domain.MapSegmentStartZoom + mapUnits;
        targetMapZoom = Mathf.Clamp(targetMapZoom, domain.MapSegmentStartZoom, domain.MapMaxZoom);

        if (state != ViewState.Map)
        {
            CameraZoomConfig config = ActiveConfig;
            if (config != null)
                StartCoroutine(RunTransition(toBase: false, config));

            return;
        }

        cameraController.SetZoomTarget(targetMapZoom);
        hasPendingZoomTarget = false;
    }

    private ZoomDomain BuildZoomDomain()
    {
        CameraZoomConfig config = ActiveConfig;
        if (config == null)
            return default;

        bool hasBase = cameraController != null && cameraController.CurrentAnchor != null;
        float mapSegmentStart = hasBase
            ? Mathf.Clamp(config.mapToBaseTransitionZoom, config.minMapZoom, config.maxMapZoom)
            : config.minMapZoom;

        ZoomDomain domain = new ZoomDomain
        {
            HasBase = hasBase,
            MapSegmentStartZoom = mapSegmentStart,
            MapWaypointZoom = Mathf.Clamp(config.defaultMapZoom, mapSegmentStart, config.maxMapZoom),
            MapMaxZoom = config.maxMapZoom
        };

        if (!hasBase)
        {
            domain.BaseRange = 0f;
            domain.MapRange = Mathf.Max(0f, domain.MapMaxZoom - domain.MapSegmentStartZoom);
            domain.TotalRange = domain.MapRange;
            return domain;
        }

        ZoomAnchor anchor = cameraController.CurrentAnchor;

        domain.BaseMinDistance = Mathf.Max(0.1f, anchor.baseMinDistance);
        domain.BaseMaxDistance = Mathf.Max(domain.BaseMinDistance, anchor.baseMaxDistance);
        domain.BaseWaypointDistance = anchor.GetBaseWaypointDistance(config.defaultBaseDistance);
        domain.TopViewDistance = Mathf.Clamp(
            anchor.baseTopDownTransitionCompleteDistance,
            domain.BaseMinDistance,
            domain.BaseMaxDistance
        );

        domain.BaseRange = Mathf.Max(0f, domain.BaseMaxDistance - domain.BaseMinDistance);
        domain.MapRange = Mathf.Max(0f, domain.MapMaxZoom - domain.MapSegmentStartZoom);
        domain.TotalRange = domain.BaseRange + domain.MapRange;

        return domain;
    }

    private static float UnitsToNormalized(ZoomDomain domain, float units)
    {
        if (!domain.IsValid)
            return 1f;

        return Mathf.Clamp01(units / domain.TotalRange);
    }

    private void SetBlur(float strength)
    {
        zoomBlurMaterial.SetFloat(BlurStrengthID, strength);
    }

    private void ShowMapOnly()
    {
        cam.cullingMask |= mapLayer;
        cam.cullingMask &= ~baseLayer;
    }

    private void ShowBaseOnly()
    {
        cam.cullingMask |= baseLayer;
        cam.cullingMask &= ~mapLayer;
    }

    private void OnDisable()
    {
        if (zoomBlurMaterial != null)
            SetBlur(0f);
    }
}