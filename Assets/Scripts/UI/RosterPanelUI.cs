using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RosterPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AgentRoster agentRoster;
    [Tooltip("Optional. Used only to display rookie mission penalties in effective stats.")]
    [SerializeField] private RecruitmentManager recruitmentManager;
    [Tooltip("Optional. Capacity changes also arrive through AgentRoster when assigned there.")]
    [SerializeField] private BaseProgressionManager baseProgression;

    [Header("UI")]
    [Tooltip("Optional root to show/hide for the roster panel. If empty, a lightweight layout is built at runtime.")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI txtSummary;
    [SerializeField] private TextMeshProUGUI txtEmptyMessage;
    [SerializeField] private Transform rosterListContent;
    [SerializeField] private RosterAgentSlotUI rosterSlotPrefab;
    [Tooltip("Optional filter. Lost agents stay hidden when this is not assigned.")]
    [SerializeField] private Toggle toggleShowLost;

    [Header("Copy")]
    [SerializeField] private string emptyRosterText = "No active agents in the roster.";

    private readonly List<RosterAgentSlotUI> spawnedSlots = new();
    private TextMeshProUGUI fallbackStatusText;
    private GameObject generatedRoot;
    private bool isOpen;
    private bool subscribedToRoster;
    private bool subscribedToBaseProgression;

    private void OnEnable()
    {
        ResolveReferences();

        if (toggleShowLost != null)
            toggleShowLost.onValueChanged.AddListener(OnShowLostChanged);

        Subscribe();
    }

    private void OnDisable()
    {
        if (toggleShowLost != null)
            toggleShowLost.onValueChanged.RemoveListener(OnShowLostChanged);

        Unsubscribe();
    }

    public void Configure(
        AgentRoster roster,
        RecruitmentManager recruitment,
        BaseProgressionManager progression,
        TextMeshProUGUI statusText)
    {
        if (agentRoster == null)
            agentRoster = roster;

        if (recruitmentManager == null)
            recruitmentManager = recruitment;

        if (baseProgression == null)
            baseProgression = progression;

        if (fallbackStatusText == null)
            fallbackStatusText = statusText;

        ResolveReferences();
        Subscribe();
    }

    public void Open()
    {
        isOpen = true;
        EnsureLayout();

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (generatedRoot != null)
            generatedRoot.SetActive(true);

        if (panelRoot == null && generatedRoot == null)
            SetLooseUiActive(true);

        if (fallbackStatusText != null)
            fallbackStatusText.gameObject.SetActive(false);

        RebuildList();
    }

    public void Close()
    {
        isOpen = false;

        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (generatedRoot != null)
            generatedRoot.SetActive(false);

        if (panelRoot == null && generatedRoot == null)
            SetLooseUiActive(false);

        if (fallbackStatusText != null)
            fallbackStatusText.gameObject.SetActive(false);
    }

    private void OnRosterChanged()
    {
        if (isOpen)
            RebuildList();
    }

    private void OnBuildingStateChanged(RuntimeBuilding runtimeBuilding)
    {
        if (isOpen)
            RefreshSummary();
    }

    private void OnShowLostChanged(bool showLost)
    {
        if (isOpen)
            RebuildList();
    }

    private void RebuildList()
    {
        ClearSlots();
        RefreshSummary();

        if (agentRoster == null || rosterListContent == null)
        {
            SetEmptyMessage("Agent roster is not assigned.");
            return;
        }

        IReadOnlyList<RuntimeAgent> agents = agentRoster.AllAgents;
        bool showLost = toggleShowLost != null && toggleShowLost.isOn;
        int visibleCount = 0;

        for (int i = 0; i < agents.Count; i++)
        {
            RuntimeAgent agent = agents[i];
            if (agent == null)
                continue;

            if (agent.Status == AgentStatus.Lost && !showLost)
                continue;

            RosterAgentSlotUI slot = CreateSlot();
            slot.Setup(agent, GetRookieStatPenalty());
            spawnedSlots.Add(slot);
            visibleCount++;
        }

        SetEmptyMessage(visibleCount == 0 ? emptyRosterText : string.Empty);
        RefreshListLayout();
    }

    private RosterAgentSlotUI CreateSlot()
    {
        RosterAgentSlotUI slot = rosterSlotPrefab != null
            ? Instantiate(rosterSlotPrefab, rosterListContent)
            : CreateGeneratedSlot();

        NormalizeSlot(slot.transform);
        return slot;
    }

    private RosterAgentSlotUI CreateGeneratedSlot()
    {
        GameObject slotObject = new(
            "RosterAgentSlot",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement),
            typeof(RosterAgentSlotUI));

        slotObject.transform.SetParent(rosterListContent, false);

        VerticalLayoutGroup layout = slotObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.spacing = 2f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        return slotObject.GetComponent<RosterAgentSlotUI>();
    }

    private void RefreshSummary()
    {
        if (txtSummary == null)
            return;

        if (agentRoster == null)
        {
            txtSummary.text = "Agents";
            return;
        }

        txtSummary.text =
            $"Agents {agentRoster.ActiveAgentCount}/{agentRoster.AgentCapacity}  |  " +
            $"Available {agentRoster.AvailableCount}  |  " +
            $"Busy {agentRoster.BusyCount}  |  " +
            $"Lost {agentRoster.LostCount}";
    }

    private void SetEmptyMessage(string message)
    {
        if (txtEmptyMessage == null)
            return;

        bool hasMessage = !string.IsNullOrWhiteSpace(message);
        txtEmptyMessage.gameObject.SetActive(hasMessage);
        txtEmptyMessage.text = message;
    }

    private void SetLooseUiActive(bool active)
    {
        if (txtSummary != null)
            txtSummary.gameObject.SetActive(active);

        if (txtEmptyMessage != null && !active)
            txtEmptyMessage.gameObject.SetActive(false);

        if (rosterListContent != null)
            rosterListContent.gameObject.SetActive(active);
    }

    private int GetRookieStatPenalty()
    {
        return recruitmentManager != null ? recruitmentManager.RookieMissionStatPenalty : 0;
    }

    private void ClearSlots()
    {
        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            if (spawnedSlots[i] != null)
                Destroy(spawnedSlots[i].gameObject);
        }

        spawnedSlots.Clear();
    }

    private void ResolveReferences()
    {
        if (agentRoster == null)
            agentRoster = FindFirstObjectByType<AgentRoster>();

        if (recruitmentManager == null)
            recruitmentManager = FindFirstObjectByType<RecruitmentManager>();

        if (baseProgression == null)
            baseProgression = FindFirstObjectByType<BaseProgressionManager>();
    }

    private void Subscribe()
    {
        if (!subscribedToRoster && agentRoster != null)
        {
            agentRoster.OnRosterChanged += OnRosterChanged;
            subscribedToRoster = true;
        }

        if (!subscribedToBaseProgression && baseProgression != null)
        {
            baseProgression.OnBuildingStateChanged += OnBuildingStateChanged;
            subscribedToBaseProgression = true;
        }
    }

    private void Unsubscribe()
    {
        if (subscribedToRoster && agentRoster != null)
        {
            agentRoster.OnRosterChanged -= OnRosterChanged;
            subscribedToRoster = false;
        }

        if (subscribedToBaseProgression && baseProgression != null)
        {
            baseProgression.OnBuildingStateChanged -= OnBuildingStateChanged;
            subscribedToBaseProgression = false;
        }
    }

    private void EnsureLayout()
    {
        if (panelRoot != null || rosterListContent != null)
            return;

        CreateFallbackLayout();
    }

    private void CreateFallbackLayout()
    {
        Transform layoutParent = GetFallbackLayoutParent();
        generatedRoot = new GameObject("RosterPanelRuntime", typeof(RectTransform), typeof(VerticalLayoutGroup));
        generatedRoot.transform.SetParent(layoutParent, false);

        RectTransform rootRect = generatedRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.08f, 0.08f);
        rootRect.anchorMax = new Vector2(0.92f, 0.78f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup rootLayout = generatedRoot.GetComponent<VerticalLayoutGroup>();
        rootLayout.padding = new RectOffset(12, 12, 12, 12);
        rootLayout.spacing = 8f;
        rootLayout.childAlignment = TextAnchor.UpperLeft;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        txtSummary = CreateText(generatedRoot.transform, "TxtRosterSummary", 22, FontStyles.Bold);
        txtEmptyMessage = CreateText(generatedRoot.transform, "TxtRosterEmpty", 18, FontStyles.Italic);

        GameObject listObject = new("RosterListContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        listObject.transform.SetParent(generatedRoot.transform, false);
        rosterListContent = listObject.transform;

        VerticalLayoutGroup listLayout = listObject.GetComponent<VerticalLayoutGroup>();
        listLayout.spacing = 6f;
        listLayout.childAlignment = TextAnchor.UpperLeft;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = listObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private Transform GetFallbackLayoutParent()
    {
        if (transform.parent != null &&
            transform.parent.name.Contains("TabBar") &&
            transform.parent.parent != null)
        {
            return transform.parent.parent;
        }

        return transform;
    }

    private TextMeshProUGUI CreateText(
        Transform parent,
        string objectName,
        float fontSize,
        FontStyles style)
    {
        GameObject textObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.enableWordWrapping = true;
        text.alignment = TextAlignmentOptions.Left;

        LayoutElement layout = textObject.AddComponent<LayoutElement>();
        layout.minHeight = fontSize + 8f;
        layout.preferredHeight = fontSize + 12f;
        layout.flexibleWidth = 1f;

        return text;
    }

    private static void NormalizeSlot(Transform slotTransform)
    {
        if (slotTransform == null)
            return;

        RectTransform rect = slotTransform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 104f);
        }

        LayoutElement layout = slotTransform.GetComponent<LayoutElement>();
        if (layout == null)
            layout = slotTransform.gameObject.AddComponent<LayoutElement>();

        layout.minHeight = 104f;
        layout.preferredHeight = 104f;
        layout.flexibleWidth = 1f;
        layout.flexibleHeight = 0f;
    }

    private void RefreshListLayout()
    {
        if (rosterListContent is not RectTransform rectTransform)
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
    }
}
