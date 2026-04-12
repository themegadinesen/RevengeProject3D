using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameState gameState;
    [SerializeField] private MissionManager missionManager;
    [SerializeField] private AgentRoster agentRoster;

    [Header("Text Fields")]
    [SerializeField] private TextMeshProUGUI txtChaos;
    [SerializeField] private TextMeshProUGUI txtCure;
    [SerializeField] private TextMeshProUGUI txtPeopleAffected;
    [SerializeField] private TextMeshProUGUI txtAgents;
    [SerializeField] private TextMeshProUGUI txtMoney;

    [Header("Optional")]
    [SerializeField] private TextMeshProUGUI txtActiveMissions;

    [Header("Cure Bar")]
    [SerializeField] private Slider cureBar;

    [Header("Chaos Bar")]
    [SerializeField] private Slider chaosBar;

    [Header("Base Build Panel")]
    [Tooltip("Optional. If assigned, the Base button opens this panel.")]
    [SerializeField] private BaseBuildPanelUI baseBuildPanel;
    [Tooltip("Optional. Button to open the base build panel. Hidden when progression is inactive.")]
    [SerializeField] private Button btnOpenBase;
    [Tooltip("Optional. Shows/hides base access based on progression state.")]
    [SerializeField] private BaseProgressionManager baseProgression;

    [Header("Recruitment")]
    [Tooltip("Optional. Drives pending candidate count.")]
    [SerializeField] private RecruitmentManager recruitmentManager;
    [Tooltip("Optional. Used to show the recruitment button only in base view.")]
    [SerializeField] private ViewManager viewManager;
    [Tooltip("Optional. Button to open the recruitment panel.")]
    [SerializeField] private Button btnOpenRecruitment;
    [Tooltip("Optional. Text showing how many pending candidates are waiting.")]
    [SerializeField] private TextMeshProUGUI txtPendingCandidates;
    [Tooltip("Optional. Candidate review panel opened by the recruitment button.")]
    [SerializeField] private RecruitmentPanelUI recruitmentPanel;

    private void OnEnable()
    {
        if (gameState) gameState.OnStateChanged += Refresh;
        if (agentRoster) agentRoster.OnRosterChanged += Refresh;
        if (recruitmentManager) recruitmentManager.OnPendingCandidatesChanged += Refresh;

        if (btnOpenBase) btnOpenBase.onClick.AddListener(OnBaseButtonClicked);
        if (btnOpenRecruitment) btnOpenRecruitment.onClick.AddListener(OnRecruitmentButtonClicked);
    }

    private void OnDisable()
    {
        if (gameState) gameState.OnStateChanged -= Refresh;
        if (agentRoster) agentRoster.OnRosterChanged -= Refresh;
        if (recruitmentManager) recruitmentManager.OnPendingCandidatesChanged -= Refresh;

        if (btnOpenBase) btnOpenBase.onClick.RemoveListener(OnBaseButtonClicked);
        if (btnOpenRecruitment) btnOpenRecruitment.onClick.RemoveListener(OnRecruitmentButtonClicked);
    }

    private void Start()
    {
        Refresh();
        UpdateButtonVisibility();
    }

    private void Update()
    {
        UpdateButtonVisibility();
    }

    private void Refresh()
    {
        txtChaos.text = $"Chaos: {gameState.Chaos:F1}";
        txtCure.text = $"Cure: {gameState.Cure:F1} / {gameState.MaxCure}";
        txtPeopleAffected.text = $"People Affected: {gameState.PeopleAffected:#,0}";
        txtMoney.text = $"Money: ${gameState.Money}";

        if (agentRoster != null)
            txtAgents.text = $"Agents: {agentRoster.AvailableCount} / {agentRoster.TotalCount}";

        if (txtPendingCandidates != null && recruitmentManager != null)
            txtPendingCandidates.text = $"Candidates: {recruitmentManager.PendingCount}";

        if (cureBar) cureBar.value = gameState.CureNormalized;
        if (chaosBar) chaosBar.value = gameState.ChaosNormalized;

        if (txtActiveMissions && missionManager)
            txtActiveMissions.text = $"Active Missions: {missionManager.ActiveMissionCount}";

        UpdateButtonVisibility();
    }

    private void UpdateButtonVisibility()
    {
        if (btnOpenBase != null && baseProgression != null)
            btnOpenBase.gameObject.SetActive(baseProgression.IsProgressionActive);

        if (btnOpenRecruitment != null)
        {
            bool showRecruitmentButton = recruitmentPanel != null && IsInBaseView();
            btnOpenRecruitment.gameObject.SetActive(showRecruitmentButton);

            if (showRecruitmentButton)
            {
                bool hasPendingCandidates =
                    recruitmentManager != null && recruitmentManager.PendingCount > 0;
                bool runActive = gameState == null || !gameState.IsRunEnded;

                btnOpenRecruitment.interactable = hasPendingCandidates && runActive;
            }
        }
    }

    private bool IsInBaseView()
    {
        if (viewManager == null)
            return true;

        return viewManager.CurrentState == ViewManager.ViewState.Base;
    }

    private void OnBaseButtonClicked()
    {
        if (baseBuildPanel != null)
            baseBuildPanel.Open();
    }

    private void OnRecruitmentButtonClicked()
    {
        if (recruitmentPanel != null)
            recruitmentPanel.Open();
    }
}