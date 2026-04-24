using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BaseBuildingDetailsPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BaseProgressionManager baseProgression;
    [SerializeField] private GameState gameState;

    [Header("Copy")]
    [SerializeField] private TextMeshProUGUI txtBuildingName;
    [SerializeField] private TextMeshProUGUI txtDescription;
    [SerializeField] private TextMeshProUGUI txtStatus;
    [SerializeField] private TextMeshProUGUI txtRequirements;
    [SerializeField] private TextMeshProUGUI txtCost;
    [SerializeField] private TextMeshProUGUI txtBuildTime;

    [Header("Buttons")]
    [SerializeField] private Button btnRenovate;
    [SerializeField] private TextMeshProUGUI txtRenovateButtonLabel;
    [SerializeField] private Button btnClose;

    private BaseBuildingNode currentNode;

    private void OnEnable()
    {
        if (btnRenovate != null)
            btnRenovate.onClick.AddListener(OnRenovateClicked);

        if (btnClose != null)
            btnClose.onClick.AddListener(Close);

        if (baseProgression != null)
            baseProgression.OnBuildingStateChanged += OnBuildingStateChanged;

        if (gameState != null)
            gameState.OnStateChanged += Refresh;
    }

    private void OnDisable()
    {
        if (btnRenovate != null)
            btnRenovate.onClick.RemoveListener(OnRenovateClicked);

        if (btnClose != null)
            btnClose.onClick.RemoveListener(Close);

        if (baseProgression != null)
            baseProgression.OnBuildingStateChanged -= OnBuildingStateChanged;

        if (gameState != null)
            gameState.OnStateChanged -= Refresh;
    }

    private void Update()
    {
        if (!gameObject.activeSelf || currentNode == null)
            return;

        RuntimeBuilding runtimeBuilding = currentNode.GetRuntimeBuilding(baseProgression);
        if (runtimeBuilding != null && runtimeBuilding.State == BuildingState.Constructing)
            Refresh();
    }

    public void Open(BaseBuildingNode node)
    {
        if (node == null || node.Data == null)
            return;

        currentNode = node;
        gameObject.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        currentNode = null;
        gameObject.SetActive(false);
    }

    private void OnRenovateClicked()
    {
        if (currentNode == null || currentNode.Data == null || baseProgression == null)
            return;

        baseProgression.TryStartConstruction(currentNode.Data);
        Refresh();
    }

    private void OnBuildingStateChanged(RuntimeBuilding runtimeBuilding)
    {
        if (currentNode == null || runtimeBuilding == null)
            return;

        if (runtimeBuilding.Data != currentNode.Data)
            return;

        Refresh();
    }

    private void Refresh()
    {
        if (currentNode == null || currentNode.Data == null || baseProgression == null)
            return;

        RuntimeBuilding runtimeBuilding = currentNode.GetRuntimeBuilding(baseProgression);
        if (runtimeBuilding == null)
            return;

        BuildingData data = runtimeBuilding.Data;

        if (txtBuildingName != null)
            txtBuildingName.text = data.buildingName;

        if (txtDescription != null)
        {
            txtDescription.text = string.IsNullOrWhiteSpace(data.description)
                ? "No building description has been authored yet."
                : data.description;
        }

        if (txtStatus != null)
            txtStatus.text = BuildStatusText(runtimeBuilding);

        if (txtRequirements != null)
            txtRequirements.text = BuildRequirementsText(data);

        if (txtCost != null)
            txtCost.text = $"Renovation Cost: ${data.moneyCost}";

        if (txtBuildTime != null)
        {
            txtBuildTime.text = data.buildTime <= 0f
                ? "Renovation Time: Instant"
                : $"Renovation Time: {FormatTime(data.buildTime)}";
        }

        bool showRenovate = runtimeBuilding.State == BuildingState.Available;
        bool canRenovate = showRenovate && CanRenovate(data);

        if (btnRenovate != null)
        {
            btnRenovate.gameObject.SetActive(showRenovate);
            btnRenovate.interactable = canRenovate;
        }

        if (txtRenovateButtonLabel != null)
        {
            if (!showRenovate)
            {
                txtRenovateButtonLabel.text = "Renovate";
            }
            else if (gameState != null && gameState.Money < data.moneyCost)
            {
                txtRenovateButtonLabel.text = "Insufficient Funds";
            }
            else if (!baseProgression.IsProgressionActive)
            {
                txtRenovateButtonLabel.text = "Unavailable";
            }
            else
            {
                txtRenovateButtonLabel.text = "Renovate";
            }
        }
    }

    private bool CanRenovate(BuildingData data)
    {
        if (baseProgression == null || data == null)
            return false;

        if (!baseProgression.IsProgressionActive)
            return false;

        if (gameState != null && gameState.IsRunEnded)
            return false;

        if (gameState != null && gameState.Money < data.moneyCost)
            return false;

        RuntimeBuilding runtimeBuilding = baseProgression.GetRuntimeBuilding(data);
        return runtimeBuilding != null && runtimeBuilding.State == BuildingState.Available;
    }

    private string BuildStatusText(RuntimeBuilding runtimeBuilding)
    {
        switch (runtimeBuilding.State)
        {
            case BuildingState.Locked:
                if (!baseProgression.IsProgressionActive)
                    return "Status: Locked\nBase progression is not active yet.";

                return "Status: Locked\nComplete the missing prerequisites to make this building available.";

            case BuildingState.Available:
                return "Status: Available for renovation";

            case BuildingState.Constructing:
                return
                    $"Status: Renovating\n" +
                    $"Remaining: {FormatTime(runtimeBuilding.BuildTimeRemaining)} ({runtimeBuilding.BuildProgress * 100f:F0}%)";

            case BuildingState.Built:
                string operationalSummary = BuildOperationalSummary(runtimeBuilding.Data);
                return string.IsNullOrEmpty(operationalSummary)
                    ? "Status: Renovated and operational"
                    : $"Status: Renovated and operational\n{operationalSummary}";

            default:
                return "Status: Unknown";
        }
    }

    private string BuildRequirementsText(BuildingData data)
    {
        List<string> lines = new();

        if (data.prerequisites == null || data.prerequisites.Length == 0)
        {
            lines.Add("Prerequisites: None");
        }
        else
        {
            lines.Add("Prerequisites:");
            for (int i = 0; i < data.prerequisites.Length; i++)
            {
                BuildingData prereq = data.prerequisites[i];
                bool isBuilt = prereq != null && baseProgression != null && baseProgression.IsBuildingBuilt(prereq);
                string name = prereq != null ? prereq.buildingName : "Missing reference";
                lines.Add($"{(isBuilt ? "[Done]" : "[Missing]")} {name}");
            }
        }

        if (!baseProgression.IsProgressionActive)
            lines.Add("Progression: Not active yet");

        if (gameState != null)
            lines.Add($"Funds: ${gameState.Money} / ${data.moneyCost}");

        return string.Join("\n", lines);
    }

    private static string BuildOperationalSummary(BuildingData data)
    {
        List<string> lines = new();

        if (data.unlocksDomain != InfrastructureDomain.None)
            lines.Add($"Unlocks: {data.unlocksDomain}");

        if (data.agentCapacityBonus > 0)
            lines.Add($"+{data.agentCapacityBonus} agent capacity");

        if (data.missionSuccessBonus > 0f)
            lines.Add($"+{data.missionSuccessBonus * 100f:F0}% mission success");

        if (data.moneyGainBonus > 0f)
            lines.Add($"+{data.moneyGainBonus * 100f:F0}% money gain");

        return string.Join("\n", lines);
    }

    private static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
        int minutes = totalSeconds / 60;
        int secs = totalSeconds % 60;

        if (minutes <= 0)
            return $"{secs}s";

        return $"{minutes}m {secs:00}s";
    }
}