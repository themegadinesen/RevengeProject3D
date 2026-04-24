using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HQPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameState gameState;
    [SerializeField] private MissionPanelUI missionPanel;

    [Header("Tabs")]
    [SerializeField] private GameObject agentsTabRoot;
    [SerializeField] private TextMeshProUGUI txtAgentsPlaceholder;
    [SerializeField] private Button btnMissionsTab;
    [SerializeField] private Button btnAgentsTab;
    [SerializeField] private Button btnClose;

    [Header("Copy")]
    [TextArea(2, 3)]
    [SerializeField] private string agentsPlaceholderText =
        "Agent roster management coming soon.";

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

        gameObject.SetActive(true);
        ShowMissionsTab();
    }

    public void Close()
    {
        if (missionPanel != null)
            missionPanel.Close();

        if (agentsTabRoot != null)
            agentsTabRoot.SetActive(false);

        gameObject.SetActive(false);
    }

    private void ShowMissionsTab()
    {
        if (agentsTabRoot != null)
            agentsTabRoot.SetActive(false);

        if (missionPanel != null)
            missionPanel.Open();
    }

    private void ShowAgentsTab()
    {
        if (missionPanel != null)
            missionPanel.Close();

        if (agentsTabRoot != null)
            agentsTabRoot.SetActive(true);

        if (txtAgentsPlaceholder != null)
            txtAgentsPlaceholder.text = agentsPlaceholderText;
    }
}