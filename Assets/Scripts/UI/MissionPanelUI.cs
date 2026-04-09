// Assets/Scripts/UI/MissionPanelUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MissionPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MissionManager  missionManager;
    [SerializeField] private DistrictManager districtManager;
    [SerializeField] private GameState       gameState;
    [SerializeField] private AgentRoster     agentRoster;

    [Header("Base Progression (optional)")]
    [Tooltip("Leave empty if base progression is not yet set up.")]
    [SerializeField] private BaseProgressionManager baseProgression;

    // ── District Selection Page ───────────────────────────────────────
    [Header("District Selection")]
    [Tooltip("Root GameObject for the district list page. Toggled on/off.")]
    [SerializeField] private GameObject        districtSelectionRoot;
    [SerializeField] private Transform         districtListContent;
    [SerializeField] private DistrictSlotUI    districtSlotPrefab;
    [SerializeField] private TextMeshProUGUI   txtDistrictPageTitle;
    [SerializeField] private Button            btnCloseDistrictPage;

    // ── Mission Page ──────────────────────────────────────────────────
    [Header("Mission Page")]
    [Tooltip("Root GameObject for the mission details page. Toggled on/off.")]
    [SerializeField] private GameObject        missionContentRoot;

    [Header("District Header")]
    [Tooltip("Shows which district's missions are displayed.")]
    [SerializeField] private TextMeshProUGUI txtDistrictName;

    [Header("Mission Info")]
    [SerializeField] private TextMeshProUGUI txtMissionName;
    [SerializeField] private TextMeshProUGUI txtMissionDesc;
    [SerializeField] private TextMeshProUGUI txtCost;
    [SerializeField] private TextMeshProUGUI txtRequirements;
    [SerializeField] private TextMeshProUGUI txtDuration;

    [Header("Infrastructure Lock Status")]
    [Tooltip("Optional text that shows domain/building lock reasons. Hidden when no requirements.")]
    [SerializeField] private TextMeshProUGUI txtInfraStatus;

    [Header("Active Mission Status")]
    [SerializeField] private TextMeshProUGUI txtActiveStatus;

    [Header("Agent Selection")]
    [SerializeField] private Transform       agentListContent;
    [SerializeField] private AgentSlotUI     agentSlotPrefab;
    [SerializeField] private TextMeshProUGUI txtTeamTotals;
    [SerializeField] private TextMeshProUGUI txtScorePreview;

    [Header("Buttons")]
    [SerializeField] private Button btnLaunch;
    [SerializeField] private Button btnClose;
    [SerializeField] private Button btnNextMission;
    [SerializeField] private Button btnPrevMission;
    [SerializeField] private Button btnBackToDistricts;

    [Header("Result Overlay")]
    [SerializeField] private GameObject      resultOverlay;
    [SerializeField] private TextMeshProUGUI txtResult;
    [SerializeField] private Button          btnDismiss;

    // ── Runtime ───────────────────────────────────────────────────────
    private MissionData[]    currentMissions;
    private int              selectedIndex;
    private RuntimeDistrict  activeDistrict;
    private readonly List<RuntimeAgent>     selectedAgents     = new();
    private readonly List<AgentSlotUI>      spawnedAgentSlots  = new();
    private readonly List<DistrictSlotUI>   spawnedDistrictSlots = new();

    // ── Enable / Disable ──────────────────────────────────────────────
    private void OnEnable()
    {
        btnLaunch.onClick.AddListener(OnLaunchClicked);
        btnClose.onClick.AddListener(Close);
        btnDismiss.onClick.AddListener(DismissResult);
        if (btnNextMission)      btnNextMission.onClick.AddListener(NextMission);
        if (btnPrevMission)      btnPrevMission.onClick.AddListener(PrevMission);
        if (btnBackToDistricts)  btnBackToDistricts.onClick.AddListener(ShowDistrictSelection);
        if (btnCloseDistrictPage) btnCloseDistrictPage.onClick.AddListener(Close);

        missionManager.OnMissionResolved += ShowResult;
    }

    private void OnDisable()
    {
        btnLaunch.onClick.RemoveListener(OnLaunchClicked);
        btnClose.onClick.RemoveListener(Close);
        btnDismiss.onClick.RemoveListener(DismissResult);
        if (btnNextMission)      btnNextMission.onClick.RemoveListener(NextMission);
        if (btnPrevMission)      btnPrevMission.onClick.RemoveListener(PrevMission);
        if (btnBackToDistricts)  btnBackToDistricts.onClick.RemoveListener(ShowDistrictSelection);
        if (btnCloseDistrictPage) btnCloseDistrictPage.onClick.RemoveListener(Close);

        missionManager.OnMissionResolved -= ShowResult;
    }

    // ── Live Update ───────────────────────────────────────────────────
    private void Update()
    {
        if (currentMissions == null || currentMissions.Length == 0) return;
        if (!missionContentRoot.activeSelf) return;

        MissionData m = currentMissions[selectedIndex];
        ActiveMission active = missionManager.GetActiveMission(m);

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
    }

    // ── Open / Close ──────────────────────────────────────────────────

    /// <summary>
    /// Opens the panel. Always starts on the district selection page.
    /// </summary>
    public void Open()
    {
        if (gameState.IsRunEnded) return;

        gameObject.SetActive(true);
        ShowDistrictSelection();
    }

    public void Close()
    {
        gameObject.SetActive(false);
        currentMissions = null;
        activeDistrict  = null;
    }

    // ── District Selection Page ───────────────────────────────────────

    private void ShowDistrictSelection()
    {
        districtSelectionRoot.SetActive(true);
        missionContentRoot.SetActive(false);
        resultOverlay.SetActive(false);

        currentMissions = null;
        activeDistrict  = null;
        selectedAgents.Clear();

        if (txtDistrictPageTitle != null)
            txtDistrictPageTitle.text = "Select District";

        RebuildDistrictList();
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
            if (!rd.IsUnlocked) continue;
            if (rd.Data.missions == null || rd.Data.missions.Length == 0) continue;

            DistrictSlotUI slot = Instantiate(districtSlotPrefab, districtListContent);
            slot.Setup(rd, OnDistrictSlotClicked);
            spawnedDistrictSlots.Add(slot);
        }
    }

    private void OnDistrictSlotClicked(RuntimeDistrict rd)
    {
        activeDistrict  = rd;
        currentMissions = rd.Data.missions;
        selectedIndex   = 0;
        selectedAgents.Clear();

        districtManager.SelectDistrict(rd);
        ShowMissionPage();
    }

    // ── Mission Page ──────────────────────────────────────────────────

    private void ShowMissionPage()
    {
        districtSelectionRoot.SetActive(false);
        missionContentRoot.SetActive(true);
        resultOverlay.SetActive(false);

        RefreshDisplay();
    }

    // ── Navigation ────────────────────────────────────────────────────
    private void NextMission()
    {
        if (currentMissions == null || currentMissions.Length == 0) return;
        selectedIndex = (selectedIndex + 1) % currentMissions.Length;
        selectedAgents.Clear();
        resultOverlay.SetActive(false);
        RefreshDisplay();
    }

    private void PrevMission()
    {
        if (currentMissions == null || currentMissions.Length == 0) return;
        selectedIndex = (selectedIndex - 1 + currentMissions.Length)
                        % currentMissions.Length;
        selectedAgents.Clear();
        resultOverlay.SetActive(false);
        RefreshDisplay();
    }

    // ── Display ───────────────────────────────────────────────────────
    private void RefreshDisplay()
    {
        MissionData m = currentMissions[selectedIndex];

        // District header.
        if (txtDistrictName != null && activeDistrict != null)
            txtDistrictName.text = activeDistrict.Data.districtName;

        txtMissionName.text = m.missionName;
        txtMissionDesc.text = m.description;
        txtCost.text        = $"Cost: ${m.moneyCost}";

        if (txtRequirements != null)
        {
            txtRequirements.text =
                $"Required — INT: {m.requiredINT}  " +
                $"STR: {m.requiredSTR}  AGI: {m.requiredAGI}";
        }

        if (txtDuration != null)
        {
            txtDuration.text = m.duration <= 0f
                ? "Duration: Instant"
                : $"Duration: ~{m.duration:F0}s ({m.behavior})";
        }

        // Infrastructure lock status.
        RefreshInfraStatus(m);

        RebuildAgentList();
        RefreshTeamPreview();
        RefreshLaunchButton();
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
        foreach (var slot in spawnedAgentSlots)
            if (slot != null) Destroy(slot.gameObject);
        spawnedAgentSlots.Clear();

        List<RuntimeAgent> available = agentRoster.GetAvailableAgents();
        foreach (var agent in available)
        {
            AgentSlotUI slot = Instantiate(agentSlotPrefab, agentListContent);
            bool isSelected  = selectedAgents.Contains(agent);
            slot.Setup(agent, isSelected, OnAgentClicked);
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
        RefreshLaunchButton();
    }

    private void RefreshTeamPreview()
    {
        MissionData m = currentMissions[selectedIndex];

        int tINT = 0, tSTR = 0, tAGI = 0;
        foreach (var a in selectedAgents)
        {
            tINT += a.INT;
            tSTR += a.STR;
            tAGI += a.AGI;
        }

        if (txtTeamTotals != null)
        {
            string C(int have, int need) =>
                need <= 0 || have >= need ? "green" : "red";

            txtTeamTotals.text =
                $"Team — " +
                $"<color={C(tINT, m.requiredINT)}>INT {tINT}/{m.requiredINT}</color>  " +
                $"<color={C(tSTR, m.requiredSTR)}>STR {tSTR}/{m.requiredSTR}</color>  " +
                $"<color={C(tAGI, m.requiredAGI)}>AGI {tAGI}/{m.requiredAGI}</color>";
        }

        if (txtScorePreview != null)
        {
            if (selectedAgents.Count > 0)
            {
                float score  = MissionManager.CalculateScoreFromTotals(
                                   tINT, tSTR, tAGI, m);
                float chance = missionManager.GetSuccessChance(score);
                float durMul = missionManager.GetDurationMultiplier(score);
                float rewMul = missionManager.GetRewardMultiplier(score);

                string durText = m.duration > 0f
                    ? $"~{m.duration * durMul:F0}s"
                    : "Instant";

                txtScorePreview.gameObject.SetActive(true);
                txtScorePreview.text =
                    $"Score: {score * 100f:F0}%  |  " +
                    $"Success: {chance * 100f:F0}%  |  " +
                    $"Duration: {durText}  |  " +
                    $"Reward: x{rewMul:F1}";
            }
            else
            {
                txtScorePreview.gameObject.SetActive(false);
            }
        }
    }

    private void RefreshLaunchButton()
    {
        MissionData m = currentMissions[selectedIndex];
        bool isActive  = missionManager.IsMissionActive(m);

        // Infrastructure gate.
        bool infraOk = baseProgression == null
                       || baseProgression.AreMissionRequirementsMet(m);

        bool canLaunch = selectedAgents.Count > 0
                         && gameState.Money >= m.moneyCost
                         && !gameState.IsRunEnded
                         && !isActive
                         && infraOk;

        btnLaunch.interactable = canLaunch;
    }

    // ── Launch ────────────────────────────────────────────────────────
    private void OnLaunchClicked()
    {
        if (gameState.IsRunEnded) return;

        MissionData m = currentMissions[selectedIndex];
        bool launched  = missionManager.TryLaunchMission(
                             m, new List<RuntimeAgent>(selectedAgents), activeDistrict);

        if (launched)
        {
            selectedAgents.Clear();
            RefreshDisplay();
        }
    }

    // ── Result ────────────────────────────────────────────────────────
    private void ShowResult(MissionResult result)
    {
        if (!gameObject.activeSelf) return;
        if (!missionContentRoot.activeSelf) return;

        resultOverlay.SetActive(true);
        btnLaunch.interactable = false;

        MissionData m = result.Data;
        string districtLabel = result.District != null
            ? $"[{result.District.Data.districtName}] "
            : "";

        if (result.Success)
        {
            string chaosLine = m.behavior == MissionBehavior.SlowBurn
                ? "(Chaos applied during op)"
                : $"+{m.chaosOnSuccess} Chaos";

            txtResult.text =
                $"<b>{districtLabel}{m.missionName}</b>\n" +
                $"<color=green>SUCCESS!</color>  " +
                $"(Score: {result.Score * 100f:F0}%)\n" +
                $"{chaosLine}\n" +
                $"+${result.ActualMoneyReward} Money";
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

            txtResult.text =
                $"<b>{districtLabel}{m.missionName}</b>\n" +
                $"<color=red>FAILED</color>  " +
                $"(Score: {result.Score * 100f:F0}%)\n" +
                $"{lostLine}\n" +
                $"+{m.chaosOnFailure} Chaos  +{m.cureOnFailure} Cure";
        }
    }

    private void DismissResult()
    {
        resultOverlay.SetActive(false);
        RefreshDisplay();
    }
}