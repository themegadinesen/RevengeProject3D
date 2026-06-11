using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Evo.UI;
using EvoButton = Evo.UI.Button;

public class ActiveMissionOverflowUI : MonoBehaviour
{
    [Header("Required")]
    [SerializeField] private EvoButton toggleButton;
    [SerializeField] private TextMeshProUGUI badgeText;
    [SerializeField] private Popover popover;
    [SerializeField] private Transform listContentRoot;
    [SerializeField] private ActiveMissionHudItemUI overflowItemPrefab;

    [Header("Transition")]
    [SerializeField] private CanvasGroup toggleButtonCanvasGroup;
    [SerializeField] private float toggleShowDuration = 0.2f;
    [SerializeField] private float toggleHiddenScale = 0.82f;
    [SerializeField] private Sprite expandedToggleIcon;
    [SerializeField] private float expandedToggleIconSize = 22f;
    [SerializeField] private float toggleMorphDuration = 0.16f;

    private readonly Dictionary<ActiveMission, ActiveMissionHudItemUI> spawnedItems = new();
    private readonly List<ActiveMission> missionsToRemove = new();
    private Coroutine toggleTransitionCoroutine;
    private Coroutine toggleMorphCoroutine;
    private Vector3 toggleShownScale = Vector3.one;
    private bool hasCachedToggleShownScale;
    private bool closedToggleEnableIcon;
    private bool closedToggleEnableText;
    private Sprite closedToggleIcon;
    private string closedToggleText;
    private float closedToggleIconSize;

    private void Awake()
    {
        CacheToggleButtonState();
        CacheClosedTogglePresentation();
        SetToggleButtonVisible(false, false);

        if (popover != null)
            popover.SetStateImmediate(false);
    }

    private void OnEnable()
    {
        if (toggleButton != null)
            toggleButton.onClick.AddListener(OnToggleButtonClicked);

        if (popover != null)
        {
            popover.onShow.AddListener(HandlePopoverShown);
            popover.onHide.AddListener(HandlePopoverHidden);
        }
    }

    private void OnDisable()
    {
        if (toggleButton != null)
            toggleButton.onClick.RemoveListener(OnToggleButtonClicked);

        if (popover != null)
        {
            popover.onShow.RemoveListener(HandlePopoverShown);
            popover.onHide.RemoveListener(HandlePopoverHidden);
        }

        StopToggleTransition();
        StopToggleMorph();
    }

    public void Rebuild(IReadOnlyList<ActiveMission> missions, GameObject tooltipPreset)
    {
        int missionCount = missions != null ? missions.Count : 0;

        if (missionCount <= 0)
        {
            Hide();
            return;
        }

        bool animateToggleIn = toggleButton != null && !toggleButton.gameObject.activeSelf;
        SetToggleButtonVisible(true, animateToggleIn);

        if (badgeText != null)
            badgeText.text = missionCount.ToString();

        if (popover == null || !popover.isOpen)
            ApplyTogglePresentation(false);

        SyncItems(missions, tooltipPreset);
    }

    public void RefreshView()
    {
        foreach (KeyValuePair<ActiveMission, ActiveMissionHudItemUI> pair in spawnedItems)
        {
            if (pair.Value != null)
                pair.Value.RefreshView();
        }
    }

    public void Hide()
    {
        ClearItems();

        if (popover != null)
            popover.SetStateImmediate(false);

        ApplyTogglePresentation(false);
        SetToggleButtonVisible(false, false);
    }

    private void OnToggleButtonClicked()
    {
        if (popover != null)
            popover.Toggle();
    }

    private void HandlePopoverShown()
    {
        SetTogglePresentation(true, true);
    }

    private void HandlePopoverHidden()
    {
        SetTogglePresentation(false, true);
    }

    private void SyncItems(IReadOnlyList<ActiveMission> missions, GameObject tooltipPreset)
    {
        if (listContentRoot == null || overflowItemPrefab == null)
            return;

        missionsToRemove.Clear();

        foreach (KeyValuePair<ActiveMission, ActiveMissionHudItemUI> pair in spawnedItems)
        {
            if (!ContainsMission(missions, pair.Key))
                missionsToRemove.Add(pair.Key);
        }

        for (int i = 0; i < missionsToRemove.Count; i++)
        {
            ActiveMission mission = missionsToRemove[i];

            if (spawnedItems.TryGetValue(mission, out ActiveMissionHudItemUI item))
            {
                if (item != null)
                    Destroy(item.gameObject);

                spawnedItems.Remove(mission);
            }
        }

        for (int i = 0; i < missions.Count; i++)
        {
            ActiveMission mission = missions[i];
            if (mission == null)
                continue;

            if (!spawnedItems.TryGetValue(mission, out ActiveMissionHudItemUI item) || item == null)
            {
                item = Instantiate(overflowItemPrefab, listContentRoot);
                item.Bind(mission, tooltipPreset);
                spawnedItems[mission] = item;
            }

            item.transform.SetSiblingIndex(i);
            item.RefreshView();
        }
    }

    private void ClearItems()
    {
        foreach (KeyValuePair<ActiveMission, ActiveMissionHudItemUI> pair in spawnedItems)
        {
            if (pair.Value != null)
                Destroy(pair.Value.gameObject);
        }

        spawnedItems.Clear();
        missionsToRemove.Clear();
    }

    private void CacheClosedTogglePresentation()
    {
        if (toggleButton == null)
            return;

        closedToggleEnableIcon = toggleButton.enableIcon;
        closedToggleEnableText = toggleButton.enableText;
        closedToggleIcon = toggleButton.icon;
        closedToggleText = toggleButton.text;
        closedToggleIconSize = toggleButton.iconSize;
    }

    private void CacheToggleButtonState()
    {
        if (toggleButton == null)
            return;

        if (!hasCachedToggleShownScale)
        {
            toggleShownScale = toggleButton.transform.localScale;
            hasCachedToggleShownScale = true;
        }

        if (toggleButtonCanvasGroup == null &&
            !toggleButton.TryGetComponent(out toggleButtonCanvasGroup))
        {
            toggleButtonCanvasGroup = toggleButton.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void SetToggleButtonVisible(bool isVisible, bool animate)
    {
        if (toggleButton == null)
            return;

        StopToggleTransition();
        StopToggleMorph();
        CacheToggleButtonState();

        if (!isVisible)
        {
            if (toggleButtonCanvasGroup != null)
                toggleButtonCanvasGroup.alpha = 0f;

            toggleButton.transform.localScale = toggleShownScale;
            toggleButton.gameObject.SetActive(false);
            return;
        }

        toggleButton.gameObject.SetActive(true);

        if (!animate || toggleShowDuration <= 0f || !isActiveAndEnabled)
        {
            if (toggleButtonCanvasGroup != null)
                toggleButtonCanvasGroup.alpha = 1f;

            toggleButton.transform.localScale = toggleShownScale;
            return;
        }

        if (toggleButtonCanvasGroup != null)
            toggleButtonCanvasGroup.alpha = 0f;

        toggleButton.transform.localScale = toggleShownScale * Mathf.Max(0f, toggleHiddenScale);
        toggleTransitionCoroutine = StartCoroutine(AnimateToggleIn());
    }

    private IEnumerator AnimateToggleIn()
    {
        float elapsed = 0f;
        Vector3 hiddenScale = toggleShownScale * Mathf.Max(0f, toggleHiddenScale);

        while (elapsed < toggleShowDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / toggleShowDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            if (toggleButtonCanvasGroup != null)
                toggleButtonCanvasGroup.alpha = eased;

            if (toggleButton != null)
                toggleButton.transform.localScale = Vector3.LerpUnclamped(hiddenScale, toggleShownScale, eased);

            yield return null;
        }

        if (toggleButtonCanvasGroup != null)
            toggleButtonCanvasGroup.alpha = 1f;

        if (toggleButton != null)
            toggleButton.transform.localScale = toggleShownScale;

        toggleTransitionCoroutine = null;
    }

    private void SetTogglePresentation(bool isExpanded, bool animate)
    {
        if (toggleButton == null)
            return;

        StopToggleMorph();

        if (!animate || toggleMorphDuration <= 0f || !isActiveAndEnabled || !toggleButton.gameObject.activeInHierarchy)
        {
            ApplyTogglePresentation(isExpanded);
            return;
        }

        toggleMorphCoroutine = StartCoroutine(AnimateTogglePresentationChange(isExpanded));
    }

    private IEnumerator AnimateTogglePresentationChange(bool isExpanded)
    {
        float halfDuration = toggleMorphDuration * 0.5f;
        Vector3 compressedScale = new Vector3(
            toggleShownScale.x * 0.88f,
            toggleShownScale.y * 0.92f,
            toggleShownScale.z);

        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = halfDuration > 0f ? Mathf.Clamp01(elapsed / halfDuration) : 1f;

            if (toggleButtonCanvasGroup != null)
                toggleButtonCanvasGroup.alpha = Mathf.Lerp(1f, 0.45f, t);

            if (toggleButton != null)
                toggleButton.transform.localScale = Vector3.Lerp(toggleShownScale, compressedScale, t);

            yield return null;
        }

        ApplyTogglePresentation(isExpanded);

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = halfDuration > 0f ? Mathf.Clamp01(elapsed / halfDuration) : 1f;
            float eased = 1f - Mathf.Pow(1f - t, 2f);

            if (toggleButtonCanvasGroup != null)
                toggleButtonCanvasGroup.alpha = Mathf.Lerp(0.45f, 1f, eased);

            if (toggleButton != null)
                toggleButton.transform.localScale = Vector3.Lerp(compressedScale, toggleShownScale, eased);

            yield return null;
        }

        if (toggleButtonCanvasGroup != null)
            toggleButtonCanvasGroup.alpha = 1f;

        if (toggleButton != null)
            toggleButton.transform.localScale = toggleShownScale;

        toggleMorphCoroutine = null;
    }

    private void ApplyTogglePresentation(bool isExpanded)
    {
        if (toggleButton == null)
            return;

        if (isExpanded)
        {
            toggleButton.enableIcon = expandedToggleIcon != null;
            toggleButton.icon = expandedToggleIcon;
            toggleButton.iconSize = expandedToggleIconSize;
            toggleButton.enableText = false;
            toggleButton.SetText(string.Empty);
            toggleButton.SetIcon(expandedToggleIcon);
            toggleButton.UpdateLayout();
        }
        else
        {
            toggleButton.enableIcon = closedToggleEnableIcon;
            toggleButton.icon = closedToggleIcon;
            toggleButton.iconSize = closedToggleIconSize;
            toggleButton.enableText = closedToggleEnableText;
            toggleButton.SetText(closedToggleText);
            toggleButton.SetIcon(closedToggleIcon);
            toggleButton.UpdateLayout();
        }

        if (badgeText != null)
            badgeText.gameObject.SetActive(!isExpanded);
    }

    private void StopToggleTransition()
    {
        if (toggleTransitionCoroutine == null)
            return;

        StopCoroutine(toggleTransitionCoroutine);
        toggleTransitionCoroutine = null;
    }

    private void StopToggleMorph()
    {
        if (toggleMorphCoroutine == null)
            return;

        StopCoroutine(toggleMorphCoroutine);
        toggleMorphCoroutine = null;
    }

    private static bool ContainsMission(IReadOnlyList<ActiveMission> missions, ActiveMission mission)
    {
        if (missions == null || mission == null)
            return false;

        for (int i = 0; i < missions.Count; i++)
        {
            if (ReferenceEquals(missions[i], mission))
                return true;
        }

        return false;
    }
}
