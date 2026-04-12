// Assets/Scripts/UI/DistrictHoverUI.cs
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Fade-in tooltip that follows the mouse while hovering a district.
/// Must be a child of a Screen Space – Overlay Canvas.
/// Requires a CanvasGroup component on the same GameObject.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class DistrictHoverUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DistrictManager districtManager;
    [SerializeField] private MissionManager missionManager;

    [Header("Text Fields")]
    [SerializeField] private TextMeshProUGUI txtDistrictName;
    [SerializeField] private TextMeshProUGUI txtLockStatus;
    [SerializeField] private TextMeshProUGUI txtChaos;
    [SerializeField] private TextMeshProUGUI txtCure;
    [SerializeField] private TextMeshProUGUI txtHeat;
    [SerializeField] private TextMeshProUGUI txtResponseState;
    [SerializeField] private TextMeshProUGUI txtPopulation;
    [SerializeField] private TextMeshProUGUI txtMissionPressure;

    [Header("Fade")]
    [SerializeField] private float fadeSpeed = 8f;

    [Header("Offset")]
    [Tooltip("Offset in Canvas-scaled pixels from the mouse cursor.")]
    [SerializeField] private Vector2 offset = new Vector2(20f, -20f);

    // ── Runtime ───────────────────────────────────────────────────────
    private CanvasGroup     canvasGroup;
    private RectTransform   rectTransform;
    private RectTransform   canvasRect;
    private RuntimeDistrict currentDistrict;
    private float           targetAlpha;

    private void Awake()
    {
        canvasGroup   = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();

        // Walk up to the root Canvas so we can convert screen → canvas space.
        Canvas rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
        canvasRect = rootCanvas.GetComponent<RectTransform>();

        canvasGroup.alpha          = 0f;
        canvasGroup.blocksRaycasts = false;
        targetAlpha = 0f;
    }

    private void Update()
    {
        canvasGroup.alpha = Mathf.MoveTowards(
            canvasGroup.alpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);

        canvasGroup.blocksRaycasts = false;

        if (currentDistrict != null && canvasGroup.alpha > 0.01f)
        {
            RefreshStats();
            FollowMouse();
        }
    }

    // ── Public API (called by DistrictMapInput) ───────────────────────
    public void Show(RuntimeDistrict district, Vector2 screenPos)
    {
        currentDistrict = district;
        targetAlpha     = 1f;
        RefreshStats();
    }

    public void Hide()
    {
        targetAlpha     = 0f;
        currentDistrict = null;
    }

    // ── Internals ─────────────────────────────────────────────────────
    private void RefreshStats()
    {
        RuntimeDistrict d = currentDistrict;

        txtDistrictName.text = d.Data.districtName;

        if (txtLockStatus != null)
        {
            if (!d.IsUnlocked)
            {
                txtLockStatus.gameObject.SetActive(true);
                txtLockStatus.text =
                    $"LOCKED — People Affected must reach {d.Data.peopleAffectedUnlockThreshold:#,0}";
            }
            else
            {
                txtLockStatus.gameObject.SetActive(false);
            }
        }

        if (d.IsUnlocked)
        {
            txtChaos.text      = $"Chaos: {d.LocalChaos:F1} / {d.MaxChaos}";
            txtCure.text       = $"Cure: {d.LocalCure:F1} / {d.MaxCure}";
            if (txtHeat != null) txtHeat.text = $"Heat: {d.LocalHeat:F1} / {d.MaxHeat}";
            if (txtResponseState != null)
            {
                DistrictResponseState state = districtManager != null
                    ? districtManager.GetResponseState(d)
                    : DistrictResponseState.Calm;

                txtResponseState.text = $"Response: {state}";
            }
            txtPopulation.text = $"People Affected: {d.LocalPeopleAffected:#,0}";

            if (txtMissionPressure != null)
            {
                int count = missionManager.GetActiveMissionCountForDistrict(d);
                float pressure = districtManager != null
                    ? districtManager.GetInvestigationPressure(d)
                    : 0f;

                txtMissionPressure.text =
                    $"Pressure: +{pressure:F1} Cure/s  |  Active Missions: {count}";
            }
        }
        else
        {
            txtChaos.text      = "Chaos: ???";
            txtCure.text       = "Cure: ???";
            if (txtHeat != null) txtHeat.text = "Heat: ???";
            if (txtResponseState != null) txtResponseState.text = "Response: ???";
            txtPopulation.text = "People: ???";
            if (txtMissionPressure != null)
                txtMissionPressure.text = "";
        }
    }

    private void FollowMouse()
    {
        if (Mouse.current == null) return;

        Vector2 screenPos = Mouse.current.position.ReadValue();

        // Convert screen position into the Canvas's local coordinate space.
        // This correctly handles Canvas Scaler (Scale With Screen Size).
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPos, null, out Vector2 localPoint);

        Vector2 tooltipSize = rectTransform.rect.size;
        Vector2 canvasSize  = canvasRect.rect.size;

        // Anchor/pivot should be (0,1) = top-left, so the box hangs
        // below-right of the cursor. Apply offset.
        Vector2 pos = localPoint + offset;

        // Clamp so the tooltip stays fully on-screen.
        // Canvas local space: X goes from -canvasSize.x/2 to +canvasSize.x/2,
        //                     Y goes from -canvasSize.y/2 to +canvasSize.y/2.
        float minX = -canvasSize.x * 0.5f;
        float maxX =  canvasSize.x * 0.5f - tooltipSize.x;
        float minY = -canvasSize.y * 0.5f + tooltipSize.y;
        float maxY =  canvasSize.y * 0.5f;

        // If the box would go off the right edge, flip to the left of the cursor.
        if (pos.x + tooltipSize.x > canvasSize.x * 0.5f)
            pos.x = localPoint.x - offset.x - tooltipSize.x;

        // If the box would go off the bottom edge, flip above the cursor.
        if (pos.y - tooltipSize.y < -canvasSize.y * 0.5f)
            pos.y = localPoint.y - offset.y + tooltipSize.y;

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        rectTransform.localPosition = pos;
    }
}