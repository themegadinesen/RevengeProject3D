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

    [Header("Mission Info")]
    [SerializeField] private TextMeshProUGUI txtMissionName;
    [SerializeField] private TextMeshProUGUI txtMissionDesc;
    [SerializeField] private TextMeshProUGUI txtCost;
    [SerializeField] private TextMeshProUGUI txtRequirements;
    [SerializeField] private TextMeshProUGUI txtDuration;

    [Header("Active Mission Status")]
    [SerializeField] private TextMeshProUGUI txtActiveStatus;

    [Header("Agent Selection")]
    [Tooltip("Parent Transform inside the ScrollView Content.")]
    [SerializeField] private Transform    agentListContent;
    [SerializeField] private AgentSlotUI  agentSlotPrefab;
    [SerializeField] private TextMeshProUGUI txtTeamTotals;
    [SerializeField] private TextMeshProUGUI txtScorePreview;

    [Header("Buttons")]
    [SerializeField] private Button btnLaunch;
    [SerializeField] private Button btnClose;
    [SerializeField] private Button btnNextMission;
    [SerializeField] private Button btnPrevMission;

    [Header("Result Overlay")]
    [SerializeField] private GameObject      resultOverlay;
    [SerializeField] private TextMeshProUGUI txtResult;
    [SerializeField] private Button          btnDismiss;

    // ── Runtime ───────────────────────────────────────────────────────
    private MissionData[] currentMissions;
    private int selectedIndex;
    private readonly List<RuntimeAgent>  selectedAgents = new();
    private readonly List<AgentSlotUI>   spawnedSlots   = new();

    // ── Enable / Disable ──────────────────────────────────────────────
    private void OnEnable()
    {
        btnLaunch.onClick.AddListener(OnLaunchClicked);
        btnClose.onClick.AddListener(Close);
        btnDismiss.onClick.AddListener(DismissResult);
        if (btnNextMission) btnNextMission.onClick.AddListener(NextMission);
        if (btnPrevMission) btnPrevMission.onClick.AddListener(PrevMission);

        missionManager.OnMissionResolved += ShowResult;
    }

    private void OnDisable()
    {
        btnLaunch.onClick.RemoveListener(OnLaunchClicked);
        btnClose.onClick.RemoveListener(Close);
        btnDismiss.onClick.RemoveListener(DismissResult);
        if (btnNextMission) btnNextMission.onClick.RemoveListener(NextMission);
        if (btnPrevMission) btnPrevMission.onClick.RemoveListener(PrevMission);

        missionManager.OnMissionResolved -= ShowResult;
    }

    // ── Live Update ───────────────────────────────────────────────────
    private void Update()
    {
        if (currentMissions == null || currentMissions.Length == 0) return;

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
    public void Open()
    {
        if (gameState.IsRunEnded) return;

        DistrictData district = districtManager.ActiveDistrict;
        if (district == null || district.missions.Length == 0)
        {
            Debug.LogWarning("No active district or no missions.");
            return;
        }

        currentMissions = district.missions;
        selectedIndex   = 0;
        selectedAgents.Clear();
        resultOverlay.SetActive(false);
        gameObject.SetActive(true);
        RefreshDisplay();
    }

    public void Close()
    {
        gameObject.SetActive(false);
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

        RebuildAgentList();
        RefreshTeamPreview();
        RefreshLaunchButton();
    }

    // ── Agent Selection ───────────────────────────────────────────────
    private void RebuildAgentList()
    {
        // Destroy old slots.
        foreach (var slot in spawnedSlots)
            if (slot != null) Destroy(slot.gameObject);
        spawnedSlots.Clear();

        // Spawn a slot for each available agent.
        List<RuntimeAgent> available = agentRoster.GetAvailableAgents();
        foreach (var agent in available)
        {
            AgentSlotUI slot = Instantiate(agentSlotPrefab, agentListContent);
            bool isSelected  = selectedAgents.Contains(agent);
            slot.Setup(agent, isSelected, OnAgentClicked);
            spawnedSlots.Add(slot);
        }
    }

    private void OnAgentClicked(RuntimeAgent agent)
    {
        if (selectedAgents.Contains(agent))
            selectedAgents.Remove(agent);
        else
            selectedAgents.Add(agent);

        // Refresh visuals without full rebuild.
        List<RuntimeAgent> available = agentRoster.GetAvailableAgents();
        for (int i = 0; i < spawnedSlots.Count && i < available.Count; i++)
            spawnedSlots[i].SetSelected(selectedAgents.Contains(available[i]));

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

        // Team totals with color coding.
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

        // Score preview (only when at least one agent is selected).
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
        MissionData m  = currentMissions[selectedIndex];
        bool isActive  = missionManager.IsMissionActive(m);

        bool canLaunch = selectedAgents.Count > 0
                         && gameState.Money >= m.moneyCost
                         && !gameState.IsRunEnded
                         && !isActive;

        btnLaunch.interactable = canLaunch;
    }

    // ── Launch ────────────────────────────────────────────────────────
    private void OnLaunchClicked()
    {
        if (gameState.IsRunEnded) return;

        MissionData m = currentMissions[selectedIndex];
        bool launched  = missionManager.TryLaunchMission(
                             m, new List<RuntimeAgent>(selectedAgents));

        if (launched)
        {
            selectedAgents.Clear();
            RefreshDisplay();       // rebuilds agent list (busy agents gone)
        }
    }

    // ── Result ────────────────────────────────────────────────────────
    private void ShowResult(MissionResult result)
    {
        if (!gameObject.activeSelf) return;

        resultOverlay.SetActive(true);
        btnLaunch.interactable = false;

        MissionData m = result.Data;

        if (result.Success)
        {
            string chaosLine = m.behavior == MissionBehavior.SlowBurn
                ? "(Chaos applied during op)"
                : $"+{m.chaosOnSuccess} Chaos";

            txtResult.text =
                $"<b>{m.missionName}</b>\n" +
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
                $"<b>{m.missionName}</b>\n" +
                $"<color=red>FAILED!</color>  " +
                $"(Score: {result.Score * 100f:F0}%)\n" +
                $"+{m.chaosOnFailure} Chaos  |  +{m.cureOnFailure} Cure\n" +
                lostLine;
        }

        RefreshDisplay();
    }

    private void DismissResult()
    {
        resultOverlay.SetActive(false);
        RefreshDisplay();
    }
}