// Assets/Scripts/UI/HUDController.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameState      gameState;
    [SerializeField] private MissionManager missionManager;
    [SerializeField] private AgentRoster    agentRoster;

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
    [Tooltip("Optional. Shows how many buildings are built.")]
    [SerializeField] private BaseProgressionManager baseProgression;

    private void OnEnable()
    {
        if (gameState)   gameState.OnStateChanged     += Refresh;
        if (agentRoster) agentRoster.OnRosterChanged  += Refresh;
        if (btnOpenBase) btnOpenBase.onClick.AddListener(OnBaseButtonClicked);
    }

    private void OnDisable()
    {
        if (gameState)   gameState.OnStateChanged     -= Refresh;
        if (agentRoster) agentRoster.OnRosterChanged  -= Refresh;
        if (btnOpenBase) btnOpenBase.onClick.RemoveListener(OnBaseButtonClicked);
    }

    private void Start()
    {
        // Hide base button until progression is active.
        if (btnOpenBase != null && baseProgression != null)
            btnOpenBase.gameObject.SetActive(baseProgression.IsProgressionActive);
    }

    private void Update()
    {
        // Show/hide base button based on progression state.
        if (btnOpenBase != null && baseProgression != null)
            btnOpenBase.gameObject.SetActive(baseProgression.IsProgressionActive);
    }

    private void Refresh()
    {
        txtChaos.text          = $"Chaos: {gameState.Chaos:F1}";
        txtCure.text           = $"Cure: {gameState.Cure:F1} / {gameState.MaxCure}";
        txtPeopleAffected.text = $"People Affected: {gameState.PeopleAffected:#,0}";
        txtMoney.text          = $"Money: ${gameState.Money}";

        if (agentRoster != null)
            txtAgents.text = $"Agents: {agentRoster.AvailableCount} / {agentRoster.TotalCount}";

        if (cureBar)  cureBar.value  = gameState.CureNormalized;
        if (chaosBar) chaosBar.value = gameState.ChaosNormalized;

        if (txtActiveMissions && missionManager)
            txtActiveMissions.text = $"Active Missions: {missionManager.ActiveMissionCount}";
    }

    private void OnBaseButtonClicked()
    {
        if (baseBuildPanel != null)
            baseBuildPanel.Open();
    }
}