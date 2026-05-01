using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HQPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameState gameState;
    [SerializeField] private MissionPanelUI missionPanel;
    [SerializeField] private AgentRoster agentRoster;
    [Tooltip("Optional. Used by the agent roster UI for rookie stat context.")]
    [SerializeField] private RecruitmentManager recruitmentManager;
    [Tooltip("Optional. Used by the agent roster UI for capacity bonus refreshes.")]
    [SerializeField] private BaseProgressionManager baseProgression;

    [Header("Tabs")]
    [SerializeField] private GameObject agentsTabRoot;
    [SerializeField] private RosterPanelUI agentRosterPanel;
    [SerializeField] private TextMeshProUGUI txtAgentsPlaceholder;
    [SerializeField] private Button btnMissionsTab;
    [SerializeField] private Button btnAgentsTab;
    [SerializeField] private Button btnClose;

    [Header("Copy")]
    [TextArea(2, 3)]
    [SerializeField] private string agentsPlaceholderText =
        "Agent roster management coming soon.";

    private void Awake()
    {
        ResolveRosterPanel();
    }

    private void OnEnable()
    {
        if (btnMissionsTab != null)
            btnMissionsTab.onClick.AddListener(ShowMissionsTab);

        if (btnAgentsTab != null)
            btnAgentsTab.onClick.AddListener(ShowAgentsTab);

        if (btnClose != null)
            btnClose.onClick.AddListener(Close);
    }

    private void OnDisable()
    {
        if (btnMissionsTab != null)
            btnMissionsTab.onClick.RemoveListener(ShowMissionsTab);

        if (btnAgentsTab != null)
            btnAgentsTab.onClick.RemoveListener(ShowAgentsTab);

        if (btnClose != null)
            btnClose.onClick.RemoveListener(Close);
    }

    public void Open()
    {
        if (gameState != null && gameState.IsRunEnded)
            return;

        ResolveRosterPanel();
        gameObject.SetActive(true);
        ShowMissionsTab();
    }

    public void Close()
    {
        if (missionPanel != null)
            missionPanel.Close();

        if (agentRosterPanel != null)
            agentRosterPanel.Close();
        else if (agentsTabRoot != null)
            agentsTabRoot.SetActive(false);

        gameObject.SetActive(false);
    }

    private void ShowMissionsTab()
    {
        ResolveRosterPanel();

        if (agentRosterPanel != null)
            agentRosterPanel.Close();
        else if (agentsTabRoot != null)
            agentsTabRoot.SetActive(false);

        if (missionPanel != null)
            missionPanel.Open();
    }

    private void ShowAgentsTab()
    {
        if (missionPanel != null)
            missionPanel.Close();

        ResolveRosterPanel();

        if (agentsTabRoot != null)
            agentsTabRoot.SetActive(true);

        if (agentRosterPanel != null)
        {
            agentRosterPanel.Open();
        }
        else if (txtAgentsPlaceholder != null)
        {
            txtAgentsPlaceholder.gameObject.SetActive(true);
            txtAgentsPlaceholder.text = agentsPlaceholderText;
        }
    }

    private void ResolveRosterPanel()
    {
        if (agentRoster == null)
            agentRoster = FindFirstObjectByType<AgentRoster>();

        if (recruitmentManager == null)
            recruitmentManager = FindFirstObjectByType<RecruitmentManager>();

        if (baseProgression == null)
            baseProgression = FindFirstObjectByType<BaseProgressionManager>();

        if (agentRosterPanel == null && agentsTabRoot != null)
            agentRosterPanel = agentsTabRoot.GetComponentInChildren<RosterPanelUI>(true);

        if (agentRosterPanel == null && agentsTabRoot != null)
            agentRosterPanel = agentsTabRoot.AddComponent<RosterPanelUI>();

        if (agentRosterPanel != null)
        {
            agentRosterPanel.Configure(
                agentRoster,
                recruitmentManager,
                baseProgression,
                txtAgentsPlaceholder);
        }
    }
}
