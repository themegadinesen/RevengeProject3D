using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

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

    [Header("Edge Reposition")]
    [Tooltip("Seconds used when the tooltip flips to the other side of the cursor near a screen edge.")]
    [Min(0f)]
    [SerializeField] private float edgeRepositionSmoothTime = 0.08f;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private RectTransform canvasRect;
    private RuntimeDistrict currentDistrict;
    private float targetAlpha;
    private Vector2 positionVelocity;
    private bool hasPosition;
    private bool isRepositioning;
    private bool hasPlacement;
    private bool placedLeftOfCursor;
    private bool placedAboveCursor;
    private bool initialized;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
        canvasGroup.blocksRaycasts = false;
    }

    private void Initialize()
    {
        if (initialized)
            return;

        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();

        Canvas rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (rootCanvas != null)
            canvasRect = rootCanvas.GetComponent<RectTransform>();

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        targetAlpha = 0f;
        initialized = true;
    }

    private void Update()
    {
        Initialize();

        canvasGroup.alpha = Mathf.MoveTowards(
            canvasGroup.alpha,
            targetAlpha,
            fadeSpeed * Time.unscaledDeltaTime);

        canvasGroup.blocksRaycasts = false;

        if (currentDistrict != null && canvasGroup.alpha > 0.01f)
        {
            RefreshStats();
            FollowMouse();
        }

        if (targetAlpha <= 0f && canvasGroup.alpha <= 0.001f)
        {
            currentDistrict = null;
            hasPosition = false;
            hasPlacement = false;
            isRepositioning = false;
            gameObject.SetActive(false);
        }
    }

    public void Show(RuntimeDistrict district, Vector2 screenPos)
    {
        bool wasInactive = !gameObject.activeSelf;
        if (wasInactive)
            gameObject.SetActive(true);

        Initialize();

        currentDistrict = district;
        targetAlpha = 1f;

        RefreshStats();
        FollowMouse(wasInactive);
    }

    public void Hide()
    {
        if (!initialized && !gameObject.activeSelf)
            return;

        Initialize();
        targetAlpha = 0f;
    }

    private void RefreshStats()
    {
        RuntimeDistrict d = currentDistrict;
        if (d == null)
            return;

        txtDistrictName.text = d.Data.districtName;

        if (txtLockStatus != null)
        {
            if (!d.IsUnlocked)
            {
                txtLockStatus.gameObject.SetActive(true);
                txtLockStatus.text =
                    $"LOCKED - People Affected must reach {d.Data.peopleAffectedUnlockThreshold:#,0}";
            }
            else
            {
                txtLockStatus.gameObject.SetActive(false);
            }
        }

        if (d.IsUnlocked)
        {
            txtChaos.text = $"Chaos: {d.LocalChaos:F1} / {d.MaxChaos}";
            txtCure.text = $"Cure: {d.LocalCure:F1} / {d.MaxCure}";

            if (txtHeat != null)
                txtHeat.text = $"Heat: {d.LocalHeat:F1} / {d.MaxHeat}";

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
                int count = missionManager != null
                    ? missionManager.GetActiveMissionCountForDistrict(d)
                    : 0;

                float pressure = districtManager != null
                    ? districtManager.GetInvestigationPressure(d)
                    : 0f;

                txtMissionPressure.text =
                    $"Pressure: +{pressure:F1} Cure/s  |  Active Missions: {count}";
            }
        }
        else
        {
            txtChaos.text = "Chaos: ???";
            txtCure.text = "Cure: ???";

            if (txtHeat != null)
                txtHeat.text = "Heat: ???";

            if (txtResponseState != null)
                txtResponseState.text = "Response: ???";

            txtPopulation.text = "People: ???";

            if (txtMissionPressure != null)
                txtMissionPressure.text = "";
        }
    }

    private void FollowMouse(bool forceInstant = false)
    {
        if (Mouse.current == null || canvasRect == null)
            return;

        Vector2 screenPos = Mouse.current.position.ReadValue();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPos, null, out Vector2 localPoint);

        Vector2 tooltipSize = rectTransform.rect.size;
        Vector2 canvasSize = canvasRect.rect.size;

        bool nextPlacedLeft = false;
        bool nextPlacedAbove = false;
        Vector2 targetPos = localPoint + offset;

        float minX = -canvasSize.x * 0.5f;
        float maxX = canvasSize.x * 0.5f - tooltipSize.x;
        float minY = -canvasSize.y * 0.5f + tooltipSize.y;
        float maxY = canvasSize.y * 0.5f;

        if (targetPos.x + tooltipSize.x > canvasSize.x * 0.5f)
        {
            targetPos.x = localPoint.x - offset.x - tooltipSize.x;
            nextPlacedLeft = true;
        }

        if (targetPos.y - tooltipSize.y < -canvasSize.y * 0.5f)
        {
            targetPos.y = localPoint.y - offset.y + tooltipSize.y;
            nextPlacedAbove = true;
        }

        targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
        targetPos.y = Mathf.Clamp(targetPos.y, minY, maxY);

        bool placementChanged = hasPlacement &&
                                (placedLeftOfCursor != nextPlacedLeft ||
                                 placedAboveCursor != nextPlacedAbove);

        placedLeftOfCursor = nextPlacedLeft;
        placedAboveCursor = nextPlacedAbove;
        hasPlacement = true;

        if (forceInstant || !hasPosition || edgeRepositionSmoothTime <= 0f)
        {
            rectTransform.localPosition = targetPos;
            positionVelocity = Vector2.zero;
            isRepositioning = false;
            hasPosition = true;
            return;
        }

        if (placementChanged)
        {
            isRepositioning = true;
            positionVelocity = Vector2.zero;
        }

        if (!isRepositioning)
        {
            rectTransform.localPosition = targetPos;
            return;
        }

        Vector2 currentPos = rectTransform.localPosition;
        Vector2 smoothedPos = Vector2.SmoothDamp(
            currentPos,
            targetPos,
            ref positionVelocity,
            edgeRepositionSmoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);

        rectTransform.localPosition = smoothedPos;

        if ((smoothedPos - targetPos).sqrMagnitude <= 1f &&
            positionVelocity.sqrMagnitude <= 1f)
        {
            rectTransform.localPosition = targetPos;
            isRepositioning = false;
            positionVelocity = Vector2.zero;
        }
    }
}