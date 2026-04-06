using System.Collections;
using UnityEngine;

/// <summary>
/// Drives the map ↔ base transition using a radial zoom-blur shader.
/// At peak blur: swaps camera projection, culling mask, and active controller.
///
/// Attach to Main Camera alongside CameraController and BaseCameraController.
/// BaseCameraController must start DISABLED in the Inspector.
/// </summary>
public class ViewManager : MonoBehaviour
{
    public enum ViewState { Map, Base, Transitioning }

    // ── Inspector ─────────────────────────────────────────────────────
    [Header("Config")]
    [SerializeField] private ViewTransitionConfig config;

    [Header("Zoom Blur Material")]
    [Tooltip("Material using Custom/ZoomBlur — same one on the Full Screen Pass Feature.")]
    [SerializeField] private Material zoomBlurMaterial;

    [Header("Controllers")]
    [Tooltip("The 2D orthographic controller (map view). Should start enabled.")]
    [SerializeField] private CameraController cameraController;

    [Tooltip("The 3D perspective controller (base view). Must start DISABLED.")]
    [SerializeField] private BaseCameraController baseCameraController;

    [Header("Layers")]
    [SerializeField] private LayerMask mapLayer;
    [SerializeField] private LayerMask baseLayer;

    [Header("Game State")]                                               // ◄ NEW
    [SerializeField] private GameState gameState;                        // ◄ NEW

    // ── Runtime ───────────────────────────────────────────────────────
    private Camera cam;
    private ViewState state = ViewState.Map;
    private bool hasSwapped;
    private float savedCamZ;

    private static readonly int BlurStrengthID = Shader.PropertyToID("_ZoomBlurStrength");

    public ViewState CurrentState => state;

    // ── Lifecycle ─────────────────────────────────────────────────────

    private void Awake()
    {
        cam = GetComponent<Camera>();

        if (config == null || zoomBlurMaterial == null)
        {
            Debug.LogError("ViewManager: Assign config and blur material!", this);
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

        // Block new transitions once the run has ended.                 // ◄ NEW
        if (gameState != null && gameState.IsRunEnded) return;           // ◄ NEW

        if (state == ViewState.Map)
        {
            if (cam.orthographicSize <= config.enterBaseThreshold)
                StartCoroutine(RunTransition(toBase: true));
        }
        else if (state == ViewState.Base)
        {
            if (baseCameraController.WantsToExitBase)
                StartCoroutine(RunTransition(toBase: false));
        }
    }

    // ── Transition ────────────────────────────────────────────────────

    private IEnumerator RunTransition(bool toBase)
    {
        state = ViewState.Transitioning;
        hasSwapped = false;

        // Lock whichever controller is currently active.
        if (toBase)
        {
            cameraController.LockInput(true);
            cameraController.GoToBaseView(); // Lerps toward anchor during blur-up.
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

            float blur = config.blurCurve.Evaluate(t) * config.peakBlurStrength;
            SetBlur(blur);

            if (!hasSwapped && t >= config.swapPoint)
            {
                hasSwapped = true;

                if (toBase) SwapToBase();
                else SwapToMap();
            }

            yield return null;
        }

        // Clean exit.
        SetBlur(0f);

        // Don't unlock controllers if the run ended mid-transition.     // ◄ NEW
        bool runEnded = gameState != null && gameState.IsRunEnded;       // ◄ NEW

        if (toBase)
        {
            if (!runEnded)                                               // ◄ CHANGED
                baseCameraController.LockInput(false);
            state = ViewState.Base;
        }
        else
        {
            if (!runEnded)                                               // ◄ CHANGED
                cameraController.LockInput(false);
            state = ViewState.Map;
        }
    }

    // ── Swap Logic ────────────────────────────────────────────────────

    private void SwapToBase()
    {
        ShowBaseOnly();

        ZoomAnchor anchor = cameraController.CurrentAnchor;

        // Disable 2D controller.
        cameraController.enabled = false;

        // Switch to perspective.
        cam.orthographic = false;
        cam.fieldOfView = anchor.GetBaseFOV();

        // Snap camera to 3D position looking at the base.
        cam.transform.position = anchor.GetBaseCameraPosition();
        cam.transform.LookAt(anchor.transform.position);

        // Enable 3D controller — it picks up from here.
        baseCameraController.Initialize(
            anchor.transform.position,
            anchor.baseCameraOffset,
            anchor.baseMinDistance,
            anchor.baseMaxDistance
        );
        baseCameraController.enabled = true;
        baseCameraController.LockInput(true); // Stay locked until transition ends.
    }

    private void SwapToMap()
    {
        ShowMapOnly();

        ZoomAnchor anchor = cameraController.CurrentAnchor;

        // Disable 3D controller.
        baseCameraController.enabled = false;

        // Switch to orthographic.
        cam.orthographic = true;
        cam.transform.rotation = Quaternion.identity;

        // Place camera at anchor's 2D position — CameraController will lerp to map center.
        Vector3 returnPos = anchor.transform.position;
        returnPos.z = savedCamZ;
        cam.transform.position = returnPos;

        // Set ortho size near the threshold so the zoom-out lerp is visible.
        cam.orthographicSize = config.enterBaseThreshold;

        // Re-enable 2D controller.
        cameraController.GoToMapView();
        cameraController.enabled = true;
        cameraController.LockInput(true); // Stay locked until transition ends.
    }

    // ── Helpers ───────────────────────────────────────────────────────

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