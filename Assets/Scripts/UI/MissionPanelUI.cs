// Assets/Scripts/UI/MissionPanelUI.cs
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionPanelUI : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private MissionManager missionManager;
    [SerializeField] private DistrictManager districtManager;
    [SerializeField] private GameState gameState;
    [SerializeField] private AgentRoster agentRoster;

    [Header("Optional Systems")]
    [Tooltip("Leave empty if base progression is not yet set up.")]
    [SerializeField] private BaseProgressionManager baseProgression;

    [Header("Panel Roots")]
    [Tooltip("Optional root for the always-visible three-column mission layout.")]
    [SerializeField] private GameObject missionContentRoot;
    [Tooltip("Optional root for the district column. This is no longer a separate page.")]
    [SerializeField] private GameObject districtSelectionRoot;

    [Header("District Column")]
    [SerializeField] private TextMeshProUGUI txtDistrictPageTitle;
    [SerializeField] private Transform districtListContent;
    [SerializeField] private DistrictSlotUI districtSlotPrefab;

    [Header("Mission Column")]
    [SerializeField] private TextMeshProUGUI txtMissionListMessage;
    [SerializeField] private Transform missionListContent;
    [SerializeField] private MissionSlotUI missionSlotPrefab;

    [Header("Mission Details")]
    [Tooltip("Shows which district's missions are displayed.")]
    [SerializeField] private TextMeshProUGUI txtDistrictName;
    [SerializeField] private TextMeshProUGUI txtMissionName;
    [SerializeField] private TextMeshProUGUI txtMissionDesc;
    [SerializeField] private TextMeshProUGUI txtCost;
    [SerializeField] private TextMeshProUGUI txtRequirements;
    [SerializeField] private TextMeshProUGUI txtDuration;
    [SerializeField] private TextMeshProUGUI txtRewards;
    [SerializeField] private TextMeshProUGUI txtFailure;
    [SerializeField] private TextMeshProUGUI txtDistrictRisk;
    [Tooltip("Optional text that shows domain/building lock reasons. Hidden when no requirements.")]
    [SerializeField] private TextMeshProUGUI txtInfraStatus;
    [SerializeField] private TextMeshProUGUI txtActiveStatus;

    [Header("Agent Selection Overlay")]
    [SerializeField] private GameObject agentSelectionRoot;
    [SerializeField] private Transform agentListContent;
    [SerializeField] private AgentSlotUI agentSlotPrefab;
    [SerializeField] private TextMeshProUGUI txtTeamTotals;
    [SerializeField] private TextMeshProUGUI txtScorePreview;

    [Header("Buttons")]
    [SerializeField] private Button btnSelectAgents;
    [Tooltip("Start Mission button inside the agent selection overlay.")]
    [SerializeField] private Button btnLaunch;
    [SerializeField] private Button btnCancelAgentSelection;

    [Header("Result Overlay")]
    [SerializeField] private GameObject resultOverlay;
    [SerializeField] private TextMeshProUGUI txtResult;
    [SerializeField] private Button btnDismiss;

    // ── Runtime ───────────────────────────────────────────────────────
    private MissionData[] currentMissions;
    private MissionData selectedMission;
    private RuntimeDistrict activeDistrict;

    private readonly List<RuntimeAgent> selectedAgents = new();
    private readonly List<AgentSlotUI> spawnedAgentSlots = new();
    private readonly List<DistrictSlotUI> spawnedDistrictSlots = new();
    private readonly List<MissionSlotUI> spawnedMissionSlots = new();

    // ── Enable / Disable ──────────────────────────────────────────────
    private void OnEnable()
    {
        if (btnSelectAgents != null) btnSelectAgents.onClick.AddListener(OnSelectAgentsClicked);
        if (btnLaunch != null) btnLaunch.onClick.AddListener(OnLaunchClicked);
        if (btnCancelAgentSelection != null) btnCancelAgentSelection.onClick.AddListener(OnCancelAgentSelectionClicked);
        if (btnDismiss != null) btnDismiss.onClick.AddListener(DismissResult);

        if (missionManager != null)
            missionManager.OnMissionResolved += ShowResult;
    }

    private void OnDisable()
    {
        if (btnSelectAgents != null) btnSelectAgents.onClick.RemoveListener(OnSelectAgentsClicked);
        if (btnLaunch != null) btnLaunch.onClick.RemoveListener(OnLaunchClicked);
        if (btnCancelAgentSelection != null) btnCancelAgentSelection.onClick.RemoveListener(OnCancelAgentSelectionClicked);
        if (btnDismiss != null) btnDismiss.onClick.RemoveListener(DismissResult);

        if (missionManager != null)
            missionManager.OnMissionResolved -= ShowResult;
    }

    // ── Live Update ───────────────────────────────────────────────────
    private void Update()
    {
        if (!gameObject.activeSelf) return;
        if (selectedMission == null) return;

        ActiveMission active = missionManager.GetActiveMission(selectedMission, activeDistrict);

        if (active != null && txtActiveStatus != null)
        {
            txtActiveStatus.gameObject.SetActive(true);
            txtActiveStatus.text =
                $"IN PROGRESS — {active.TimeRemaining:F0}s " +
                $"({active.Progress * 100f:F0}%)";
        }
        else if (txtActiveStatus != null)
        {
            txtActiveStatus.gameObject.SetActive(false);
        }

        RefreshDistrictRisk();
        if (agentSelectionRoot == null || agentSelectionRoot.activeSelf)
            RefreshTeamPreview();

        RefreshActionButtons();
    }

    // ── Open / Close ──────────────────────────────────────────────────

    /// <summary>
    /// Opens the panel on the three-column mission layout.
    /// </summary>
    public void Open()
    {
        if (gameState != null && gameState.IsRunEnded) return;

        gameObject.SetActive(true);
        ShowPanel();
    }

    public void Close()
    {
        gameObject.SetActive(false);
        currentMissions = null;
        selectedMission = null;
        activeDistrict = null;
        selectedAgents.Clear();
        ClearAgentList();
    }

    // ── Panel Setup ───────────────────────────────────────────────────

    private void ShowPanel()
    {
        if (districtSelectionRoot != null) districtSelectionRoot.SetActive(true);
        if (missionContentRoot != null) missionContentRoot.SetActive(true);
        if (resultOverlay != null) resultOverlay.SetActive(false);
        CloseAgentSelection();

        NormalizeListContent(districtListContent);
        NormalizeListContent(missionListContent);
        NormalizeListContent(agentListContent);

        currentMissions = null;
        selectedMission = null;
        activeDistrict = null;
        selectedAgents.Clear();

        if (txtDistrictPageTitle != null)
            txtDistrictPageTitle.text = "Districts";

        RebuildDistrictList();
        RebuildMissionList();
        RefreshDisplay();
    }

    private void RebuildDistrictList()
    {
        // Destroy old slots.
        foreach (var slot in spawnedDistrictSlots)
            if (slot != null) Destroy(slot.gameObject);
        spawnedDistrictSlots.Clear();

        RuntimeDistrict[] all = districtManager.GetAllRuntimeDistricts();
        if (all == null) return;

        for (int i = 0; i < all.Length; i++)
        {
            RuntimeDistrict rd = all[i];
            if (rd == null || !rd.IsUnlocked) continue;

            DistrictSlotUI slot = Instantiate(districtSlotPrefab, districtListContent);
            NormalizeSlot(slot.transform, 60f);
            slot.Setup(rd, OnDistrictSlotClicked);
            slot.SetSelected(rd == activeDistrict);
            spawnedDistrictSlots.Add(slot);
        }
    }

    private void OnDistrictSlotClicked(RuntimeDistrict rd)
    {
        if (rd == null || !rd.IsUnlocked) return;
        activeDistrict = rd;
        currentMissions = rd.Data.missions;
        selectedMission = null;
        selectedAgents.Clear();

        districtManager.SelectDistrict(rd);
        CloseAgentSelection();
        RebuildDistrictList();
        RebuildMissionList();
        RefreshDisplay();
    }

    // ── Display ───────────────────────────────────────────────────────
    private void RefreshDisplay()
    {
        MissionData m = selectedMission;

        if (txtDistrictName != null)
        {
            txtDistrictName.text = activeDistrict != null
                ? activeDistrict.Data.districtName
                : "Select District";
        }

        if (m == null)
        {
            if (txtMissionName != null) txtMissionName.text = "Select Mission";
            if (txtMissionDesc != null)
            {
                txtMissionDesc.text = activeDistrict == null
                    ? "Select a district first."
                    : "Select a mission from the mission list.";
            }
            if (txtCost != null) txtCost.text = "";
            if (txtRequirements != null) txtRequirements.text = "";
            if (txtDuration != null) txtDuration.text = "";
            if (txtRewards != null) txtRewards.text = "";
            if (txtFailure != null) txtFailure.text = "";
            if (txtInfraStatus != null) txtInfraStatus.gameObject.SetActive(false);
            if (txtActiveStatus != null) txtActiveStatus.gameObject.SetActive(false);

            RefreshDistrictRisk();
            RefreshActionButtons();
            return;
        }

        if (txtMissionName != null) txtMissionName.text = m.missionName;
        if (txtMissionDesc != null) txtMissionDesc.text = m.description;
        if (txtCost != null) txtCost.text = $"Cost: ${m.moneyCost}";

        if (txtRequirements != null)
        {
            txtRequirements.text =
                $"Required - INT: {m.requiredINT}  " +
                $"STR: {m.requiredSTR}  AGI: {m.requiredAGI}";
        }

        if (txtDuration != null)
        {
            txtDuration.text = m.duration <= 0f
                ? "Duration: Instant"
                : $"Duration: ~{m.duration:F0}s ({m.behavior})";
        }

        if (txtRewards != null)
        {
            string special = m.specialReward == MissionSpecialReward.CandidateArrival
                ? " | Candidate arrives"
                : "";
            txtRewards.text =
                $"Success - Chaos +{m.chaosOnSuccess:F0} | Heat +{m.heatOnSuccess:F0} | " +
                $"Cure +{m.cureOnSuccess:F0} | Money +${m.moneyOnSuccess}{special}";
        }

        if (txtFailure != null)
        {
            txtFailure.text =
                $"Failure - Chaos +{m.chaosOnFailure:F0} | Heat +{m.heatOnFailure:F0} | " +
                $"Cure +{m.cureOnFailure:F0} | Agents Lost {m.agentsLostOnFailure}";
        }

        RefreshInfraStatus(m);
        RefreshDistrictRisk();
        RefreshActionButtons();
    }

    private void RefreshDistrictRisk()
    {
        if (txtDistrictRisk == null) return;

        if (activeDistrict == null)
        {
            txtDistrictRisk.gameObject.SetActive(false);
            return;
        }

        DistrictResponseState responseState = missionManager.GetDistrictResponseState(activeDistrict);
        float successPenalty = missionManager.GetDistrictSuccessChancePenalty(activeDistrict);
        float cureMultiplier = missionManager.GetDistrictCureMultiplier(activeDistrict);
        float extraLossChance = missionManager.GetDistrictBonusAgentLossChance(activeDistrict);
        float pressure = missionManager.GetDistrictInvestigationPressure(activeDistrict);

        txtDistrictRisk.gameObject.SetActive(true);
        string sabotageLine = string.Empty;
        float sabotagePenalty = missionManager.GetRecruitmentSabotagePenaltyPreview();
        int sabotageMissions = missionManager.GetRemainingImplantPenaltyMissions();
        if (sabotagePenalty > 0f && sabotageMissions > 0)
        {
            string missionLabel = sabotageMissions == 1 ? "mission" : "missions";
            sabotageLine =
                $"\nRecruitment Risk: -{sabotagePenalty * 100f:F0}% success for next {sabotageMissions} {missionLabel}";
        }

        txtDistrictRisk.text =
            $"District Risk — Heat {activeDistrict.LocalHeat:F1}/{activeDistrict.MaxHeat:F0}  |  " +
            $"State: {responseState}  |  " +
            $"Success Penalty: -{successPenalty * 100f:F0}%  |  " +
            $"Cure x{cureMultiplier:F2}  |  " +
            $"Extra Loss: {extraLossChance * 100f:F0}%  |  " +
            $"Pressure: +{pressure:F1}/s" +
            sabotageLine;
    }

    private void RefreshInfraStatus(MissionData m)
    {
        if (txtInfraStatus == null) return;

        bool needsDomain   = m.requiredDomain != InfrastructureDomain.None;
        bool needsBuilding = m.requiredBuilding != null;

        if (!needsDomain && !needsBuilding)
        {
            txtInfraStatus.gameObject.SetActive(false);
            return;
        }

        txtInfraStatus.gameObject.SetActive(true);

        bool domainOk   = baseProgression == null || baseProgression.IsDomainUnlocked(m.requiredDomain);
        bool buildingOk = baseProgression == null || baseProgression.IsBuildingBuilt(m.requiredBuilding);

        string status = "";

        if (needsDomain)
        {
            string color = domainOk ? "green" : "red";
            string label = domainOk ? "UNLOCKED" : "LOCKED";
            status += $"Domain: <color={color}>{m.requiredDomain} — {label}</color>";
        }

        if (needsBuilding)
        {
            if (status.Length > 0) status += "\n";
            string color = buildingOk ? "green" : "red";
            string label = buildingOk ? "BUILT" : "NOT BUILT";
            status += $"Building: <color={color}>{m.requiredBuilding.buildingName} — {label}</color>";
        }

        txtInfraStatus.text = status;
    }

    // ── Agent Selection ───────────────────────────────────────────────
    private void RebuildAgentList()
    {
        ClearAgentList();

        if (agentRoster == null || agentSlotPrefab == null || agentListContent == null)
            return;

        List<RuntimeAgent> available = agentRoster.GetAvailableAgents();
        foreach (var agent in available)
        {
            AgentSlotUI slot = Instantiate(agentSlotPrefab, agentListContent);
            NormalizeAgentSlot(slot.transform);
            bool isSelected  = selectedAgents.Contains(agent);
            slot.Setup(
                agent,
                isSelected,
                OnAgentClicked,
                missionManager != null ? missionManager.GetRookieMissionStatPenalty() : 0);
            spawnedAgentSlots.Add(slot);
        }
    }

    private void OnAgentClicked(RuntimeAgent agent)
    {
        if (selectedAgents.Contains(agent))
            selectedAgents.Remove(agent);
        else
            selectedAgents.Add(agent);

        List<RuntimeAgent> available = agentRoster.GetAvailableAgents();
        for (int i = 0; i < spawnedAgentSlots.Count && i < available.Count; i++)
            spawnedAgentSlots[i].SetSelected(selectedAgents.Contains(available[i]));

        RefreshTeamPreview();
        RefreshActionButtons();
    }

    private void RefreshTeamPreview()
    {
        if (agentSelectionRoot != null && !agentSelectionRoot.activeSelf)
            return;

        MissionData m = selectedMission;

        if (m == null)
        {
            if (txtTeamTotals != null)
                txtTeamTotals.text = "Team - select a mission";

            if (txtScorePreview != null)
                txtScorePreview.gameObject.SetActive(false);

            return;
        }

        missionManager.GetMissionTeamTotals(selectedAgents, out int tINT, out int tSTR, out int tAGI);

        if (txtTeamTotals != null)
        {
            string C(int have, int need) =>
                need <= 0 || have >= need ? "green" : "red";

            txtTeamTotals.text =
                $"Team - " +
                $"<color={C(tINT, m.requiredINT)}>INT {tINT}/{m.requiredINT}</color>  " +
                $"<color={C(tSTR, m.requiredSTR)}>STR {tSTR}/{m.requiredSTR}</color>  " +
                $"<color={C(tAGI, m.requiredAGI)}>AGI {tAGI}/{m.requiredAGI}</color>";
        }

        if (txtScorePreview == null)
            return;

        if (selectedAgents.Count <= 0)
        {
            txtScorePreview.gameObject.SetActive(false);
            return;
        }

        float score = MissionManager.CalculateScoreFromTotals(tINT, tSTR, tAGI, m);
        float baseChance = missionManager.GetBaseSuccessChance(score);
        float traitBonus = missionManager.GetTeamTraitSuccessChanceBonus(selectedAgents);
        float heatPenalty = missionManager.GetDistrictSuccessChancePenalty(activeDistrict);
        float sabotagePenalty = missionManager.GetRecruitmentSabotagePenaltyPreview();
        float chance = missionManager.GetTeamSuccessChance(score, selectedAgents, activeDistrict);
        float durMul = missionManager.GetDurationMultiplier(score);
        float rewMul = missionManager.GetRewardMultiplier(score);
        DistrictResponseState responseState = missionManager.GetDistrictResponseState(activeDistrict);

        string successBreakdown = $"Base {baseChance * 100f:F0}%";

        if (traitBonus > 0f)
            successBreakdown += $"  + Trait {traitBonus * 100f:F0}%";

        if (heatPenalty > 0f)
            successBreakdown += $"  - Risk {heatPenalty * 100f:F0}%";

        if (sabotagePenalty > 0f)
            successBreakdown += $"  - Sabotage {sabotagePenalty * 100f:F0}%";

        string durText = m.duration > 0f
            ? $"~{m.duration * durMul:F0}s"
            : "Instant";

        txtScorePreview.gameObject.SetActive(true);
        txtScorePreview.text =
            $"Score: {score * 100f:F0}%  |  " +
            $"Success: {chance * 100f:F0}% ({successBreakdown})  |  " +
            $"State: {responseState}  |  " +
            $"Duration: {durText}  |  " +
            $"Reward: x{rewMul:F1}";
    }

    private void RefreshActionButtons()
    {
        RefreshSelectAgentsButton();
        RefreshLaunchButton();
    }

    private void RefreshSelectAgentsButton()
    {
        if (btnSelectAgents == null)
            return;

        MissionData m = selectedMission;

        if (m == null)
        {
            btnSelectAgents.interactable = false;
            return;
        }

        bool isActive = missionManager.IsMissionActive(m, activeDistrict);
        bool infraOk = baseProgression == null
                    || baseProgression.AreMissionRequirementsMet(m);
        bool hasAgents = agentRoster != null && agentRoster.AvailableCount > 0;
        bool runActive = gameState == null || !gameState.IsRunEnded;
        bool resultHidden = resultOverlay == null || !resultOverlay.activeSelf;

        btnSelectAgents.interactable = hasAgents
                                     && gameState.Money >= m.moneyCost
                                     && runActive
                                     && !isActive
                                     && resultHidden
                                     && infraOk;
    }

    private void RefreshLaunchButton()
    {
        if (btnLaunch == null)
            return;

        MissionData m = selectedMission;

        if (m == null)
        {
            btnLaunch.interactable = false;
            return;
        }

        bool isActive = missionManager.IsMissionActive(m, activeDistrict);

        bool infraOk = baseProgression == null
                    || baseProgression.AreMissionRequirementsMet(m);

        bool runActive = gameState == null || !gameState.IsRunEnded;
        bool resultHidden = resultOverlay == null || !resultOverlay.activeSelf;

        bool canLaunch = selectedAgents.Count > 0
                        && gameState.Money >= m.moneyCost
                        && runActive
                        && !isActive
                        && resultHidden
                        && infraOk;

        btnLaunch.interactable = canLaunch;
    }


    private void OnMissionSlotClicked(MissionData mission)
    {
        selectedMission = mission;
        selectedAgents.Clear();
        RebuildMissionList();
        CloseAgentSelection();
        RefreshDisplay();
    }
    private void RebuildMissionList()
    {
        foreach (var slot in spawnedMissionSlots)
            if (slot != null) Destroy(slot.gameObject);
        spawnedMissionSlots.Clear();

        if (activeDistrict == null)
        {
            SetMissionListMessage("Select a district first.");
            return;
        }

        if (currentMissions == null || currentMissions.Length == 0)
        {
            SetMissionListMessage("No missions available.");
            return;
        }

        SetMissionListMessage("");

        foreach (MissionData mission in currentMissions)
        {
            MissionSlotUI slot = Instantiate(missionSlotPrefab, missionListContent);
            NormalizeSlot(slot.transform, 60f);
            bool selected = mission == selectedMission;
            bool active = missionManager.IsMissionActive(mission, activeDistrict);
            slot.Setup(mission, selected, active, OnMissionSlotClicked);
            spawnedMissionSlots.Add(slot);
        }
    }

    private void SetMissionListMessage(string message)
    {
        if (txtMissionListMessage == null)
            return;

        bool hasMessage = !string.IsNullOrWhiteSpace(message);
        txtMissionListMessage.gameObject.SetActive(hasMessage);
        txtMissionListMessage.text = message;
    }

    private void OnSelectAgentsClicked()
    {
        if (selectedMission == null) return;

        if (agentSelectionRoot != null)
            agentSelectionRoot.SetActive(true);

        selectedAgents.Clear();
        RebuildAgentList();
        RefreshTeamPreview();
        RefreshActionButtons();
    }

    private void OnCancelAgentSelectionClicked()
    {
        selectedAgents.Clear();
        CloseAgentSelection();
        RefreshActionButtons();
    }

    private void CloseAgentSelection()
    {
        if (agentSelectionRoot != null)
            agentSelectionRoot.SetActive(false);
    }

    private void ClearAgentList()
    {
        foreach (var slot in spawnedAgentSlots)
            if (slot != null) Destroy(slot.gameObject);

        spawnedAgentSlots.Clear();
    }

    private static void NormalizeListContent(Transform content)
    {
        if (content == null) return;

        RectTransform rect = content as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, rect.sizeDelta.y);
        }

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = content.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scrollRect = content.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            scrollRect.content = rect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
        }
    }

    private static void NormalizeSlot(Transform slotTransform, float preferredHeight)
    {
        if (slotTransform == null) return;

        RectTransform rect = slotTransform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, preferredHeight);
        }

        LayoutElement layout = slotTransform.GetComponent<LayoutElement>();
        if (layout == null)
            layout = slotTransform.gameObject.AddComponent<LayoutElement>();

        layout.minHeight = preferredHeight;
        layout.preferredHeight = preferredHeight;
        layout.flexibleWidth = 1f;
        layout.flexibleHeight = 0f;
    }

    private static void NormalizeAgentSlot(Transform slotTransform)
    {
        const float PreferredHeight = 64f;

        NormalizeSlot(slotTransform, PreferredHeight);

        HorizontalLayoutGroup horizontalLayout = slotTransform.GetComponent<HorizontalLayoutGroup>();
        if (horizontalLayout == null)
            horizontalLayout = slotTransform.gameObject.AddComponent<HorizontalLayoutGroup>();

        horizontalLayout.padding = new RectOffset(8, 8, 6, 6);
        horizontalLayout.spacing = 8f;
        horizontalLayout.childAlignment = TextAnchor.MiddleLeft;
        horizontalLayout.childControlWidth = true;
        horizontalLayout.childControlHeight = true;
        horizontalLayout.childForceExpandWidth = false;
        horizontalLayout.childForceExpandHeight = true;

        for (int i = 0; i < slotTransform.childCount; i++)
        {
            Transform child = slotTransform.GetChild(i);
            RectTransform childRect = child as RectTransform;
            if (childRect != null)
            {
                childRect.anchorMin = new Vector2(0f, 0f);
                childRect.anchorMax = new Vector2(0f, 1f);
                childRect.pivot = new Vector2(0f, 0.5f);
                childRect.anchoredPosition = Vector2.zero;
            }

            TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
            if (text != null)
            {
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Ellipsis;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.margin = Vector4.zero;
            }

            LayoutElement childLayout = child.GetComponent<LayoutElement>();
            if (childLayout == null)
                childLayout = child.gameObject.AddComponent<LayoutElement>();

            if (child.name.Contains("Name"))
            {
                childLayout.minWidth = 90f;
                childLayout.preferredWidth = 130f;
                childLayout.flexibleWidth = 0f;
            }
            else
            {
                childLayout.minWidth = 120f;
                childLayout.preferredWidth = 220f;
                childLayout.flexibleWidth = 1f;
            }

            childLayout.minHeight = PreferredHeight - horizontalLayout.padding.vertical;
            childLayout.preferredHeight = PreferredHeight - horizontalLayout.padding.vertical;
            childLayout.flexibleHeight = 1f;
        }
    }
    
    // ── Launch ────────────────────────────────────────────────────────
    private void OnLaunchClicked()
    {
        if (gameState != null && gameState.IsRunEnded) return;
        if (selectedMission == null) return;

        bool launched = missionManager.TryLaunchMission(
            selectedMission,
            new List<RuntimeAgent>(selectedAgents),
            activeDistrict);

        if (!launched) return;

        selectedAgents.Clear();
        CloseAgentSelection();
        RebuildMissionList();
        RefreshDisplay();
    }

    // ── Result ────────────────────────────────────────────────────────
    private void ShowResult(MissionResult result)
    {
        if (!gameObject.activeSelf) return;

        if (resultOverlay != null)
            resultOverlay.SetActive(true);

        if (btnLaunch != null)
            btnLaunch.interactable = false;

        if (btnSelectAgents != null)
            btnSelectAgents.interactable = false;

        RebuildMissionList();

        MissionData m = result.Data;
        string districtLabel = result.District != null
            ? $"[{result.District.Data.districtName}] "
            : "";

        if (result.Success)
        {
            string chaosLine = m.behavior == MissionBehavior.SlowBurn
                ? "(Chaos applied during op)"
                : $"+{m.chaosOnSuccess} Chaos";

            string modifierLine = BuildResultModifierLine(result);

            string heatLine = result.District != null
                ? $"\nHeat: {result.DistrictHeatBeforeOutcome:F1} -> {result.DistrictHeatAfterOutcome:F1} ({result.ResponseState})"
                : "";

            txtResult.text =
                $"<b>{districtLabel}{m.missionName}</b>\n" +
                $"<color=green>SUCCESS!</color>  " +
                $"(Score: {result.Score * 100f:F0}%  |  Chance Used: {result.SuccessChance * 100f:F0}%)\n" +
                modifierLine +
                $"{chaosLine}\n" +
                $"+${result.ActualMoneyReward} Money" +
                heatLine;
        }
        else
        {
            string lostLine = "No agents lost.";
            if (result.LostAgents != null && result.LostAgents.Count > 0)
            {
                var names = new List<string>();
                foreach (var a in result.LostAgents) names.Add(a.Name);
                lostLine = $"Lost: {string.Join(", ", names)}";
            }

            string modifierLine = BuildResultModifierLine(result);

            string heatLine = result.District != null
                ? $"\nHeat: {result.DistrictHeatBeforeOutcome:F1} -> {result.DistrictHeatAfterOutcome:F1} ({result.ResponseState})"
                : "";

            txtResult.text =
                $"<b>{districtLabel}{m.missionName}</b>\n" +
                $"<color=red>FAILED</color>  " +
                $"(Score: {result.Score * 100f:F0}%  |  Chance Used: {result.SuccessChance * 100f:F0}%)\n" +
                modifierLine +
                $"{lostLine}\n" +
                $"+{m.chaosOnFailure} Chaos  +{m.cureOnFailure} Cure" +
                heatLine;
        }
    }

    private void DismissResult()
    {
        if (resultOverlay != null)
            resultOverlay.SetActive(false);

        RefreshDisplay();
    }

    private static string BuildResultModifierLine(MissionResult result)
    {
        List<string> parts = new();

        if (result.TraitSuccessChanceBonus > 0f)
            parts.Add($"Trait +{result.TraitSuccessChanceBonus * 100f:F0}%");

        if (result.ImplantSuccessPenaltyApplied > 0f)
        {
            string missionLabel = result.RemainingImplantPenaltyMissions == 1 ? "mission" : "missions";
            parts.Add(
                $"Sabotage -{result.ImplantSuccessPenaltyApplied * 100f:F0}% ({result.RemainingImplantPenaltyMissions} {missionLabel} remain)");
        }

        return parts.Count > 0
            ? $"{string.Join("  |  ", parts)}\n"
            : string.Empty;
    }
}