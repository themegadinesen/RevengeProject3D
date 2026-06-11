using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Evo.UI;
using EvoTimer = Evo.UI.Timer;
using EvoButton = Evo.UI.Button;

public class ActiveMissionHudItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Required")]
    [SerializeField] private EvoTimer timer;
    [SerializeField] private Tooltip tooltip;
    [SerializeField] private EvoButton button;
    [SerializeField] private Graphic raycastTarget;

    [Header("Animation")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeInDuration = 0.18f;

    [Header("Optional Visuals")]
    [SerializeField] private Graphic hoverVisual;
    [SerializeField] private Image centerIcon;
    [SerializeField] private TextMeshProUGUI centerText;

    private ActiveMission mission;
    private string cachedTooltipTitle;
    private string cachedTooltipDescription;
    private Coroutine fadeCoroutine;
    private bool pendingFadeIn;

    public ActiveMission Mission => mission;

    private void Awake()
    {
        if (canvasGroup == null)
            TryGetComponent(out canvasGroup);
    }

    private void OnEnable()
    {
        TryStartFadeIn();
    }

    public void Bind(ActiveMission activeMission, GameObject tooltipPreset)
    {
        mission = activeMission;
        cachedTooltipTitle = null;
        cachedTooltipDescription = null;

        if (tooltip != null && tooltipPreset != null)
            tooltip.tooltipPreset = tooltipPreset;

        if (button != null && raycastTarget != null && button.targetGraphic == null)
            button.targetGraphic = raycastTarget;

        if (raycastTarget != null)
            raycastTarget.raycastTarget = true;

        ConfigureTimerForMissionDisplay();

        if (centerIcon != null)
            centerIcon.enabled = centerIcon.sprite != null;

        SetHoverVisual(false);
        RefreshView();
        QueueFadeIn();
    }

    public void RefreshView()
    {
        if (mission == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (timer != null)
        {
            timer.duration = Mathf.Max(0f, mission.Duration);
            timer.countDown = true;
            timer.SetCurrentTimeWithoutNotify(Mathf.Max(0f, mission.TimeRemaining));
        }

        if (centerText != null)
            centerText.text = mission.AssignedCount.ToString();

        string tooltipTitle = GetMissionTitle(mission);
        string tooltipDescription = BuildTooltipDescription(mission);

        if (tooltip != null &&
            (tooltipTitle != cachedTooltipTitle || tooltipDescription != cachedTooltipDescription))
        {
            tooltip.SetContent(tooltipTitle, tooltipDescription);
            cachedTooltipTitle = tooltipTitle;
            cachedTooltipDescription = tooltipDescription;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHoverVisual(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHoverVisual(false);
    }

    private void OnDisable()
    {
        SetHoverVisual(false);

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
    }

    private void ConfigureTimerForMissionDisplay()
    {
        if (timer == null)
            return;

        timer.countDown = true;
        timer.loop = false;
        timer.timeMultiplier = 1f;
        timer.enableSmoothing = false;
        timer.updateBarOnSecondsOnly = false;
        timer.textFormat = "{0}";
        timer.displayFormat = EvoTimer.DisplayFormat.Time_MM_SS;
        timer.StopTimer();
    }

    private void QueueFadeIn()
    {
        if (canvasGroup == null)
            return;

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        pendingFadeIn = true;
        canvasGroup.alpha = fadeInDuration <= 0f ? 1f : 0f;
        TryStartFadeIn();
    }

    private void TryStartFadeIn()
    {
        if (!pendingFadeIn || canvasGroup == null || !gameObject.activeInHierarchy)
            return;

        pendingFadeIn = false;

        if (fadeInDuration <= 0f)
        {
            canvasGroup.alpha = 1f;
            return;
        }

        fadeCoroutine = StartCoroutine(FadeInCoroutine());
    }

    private IEnumerator FadeInCoroutine()
    {
        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        fadeCoroutine = null;
    }

    private void SetHoverVisual(bool isVisible)
    {
        if (hoverVisual != null)
            hoverVisual.enabled = isVisible;
    }

    private static string GetMissionTitle(ActiveMission activeMission)
    {
        if (activeMission == null || activeMission.Data == null)
            return "Unknown Mission";

        if (string.IsNullOrWhiteSpace(activeMission.Data.missionName))
            return "Unknown Mission";

        return activeMission.Data.missionName;
    }

    private static string BuildTooltipDescription(ActiveMission activeMission)
    {
        return
            $"Time Remaining: {FormatRemainingTime(activeMission != null ? activeMission.TimeRemaining : 0f)}\n" +
            $"Agents Assigned: {FormatAssignedAgents(activeMission != null ? activeMission.AssignedAgents : null)}";
    }

    public static string FormatRemainingTime(float secondsRemaining)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(secondsRemaining));
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;

        if (hours > 0)
            return $"{hours}:{minutes:00}:{seconds:00}";

        return $"{minutes:00}:{seconds:00}";
    }

    public static string FormatAssignedAgents(IReadOnlyList<RuntimeAgent> agents)
    {
        if (agents == null || agents.Count == 0)
            return "None";

        List<string> names = new();

        for (int i = 0; i < agents.Count; i++)
        {
            RuntimeAgent agent = agents[i];
            if (agent == null)
                continue;

            names.Add(string.IsNullOrWhiteSpace(agent.Name) ? "Unnamed Agent" : agent.Name);
        }

        return names.Count > 0 ? string.Join(", ", names) : "None";
    }
}
