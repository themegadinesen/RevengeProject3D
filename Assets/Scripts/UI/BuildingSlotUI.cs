// Assets/Scripts/UI/BuildingSlotUI.cs
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuildingSlotUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI txtName;
    [SerializeField] private TextMeshProUGUI txtStatus;
    [SerializeField] private TextMeshProUGUI txtCost;
    [SerializeField] private TextMeshProUGUI txtDomain;
    [SerializeField] private Slider          progressBar;
    [SerializeField] private Button          btnBuild;

    private RuntimeBuilding runtimeBuilding;
    private Action<BuildingData> onBuildClicked;

    public void Setup(RuntimeBuilding rb, Action<BuildingData> buildCallback)
    {
        runtimeBuilding = rb;
        onBuildClicked  = buildCallback;

        txtName.text = rb.Data.buildingName;

        if (txtCost != null)
            txtCost.text = $"${rb.Data.moneyCost}";

        if (txtDomain != null)
        {
            txtDomain.text = rb.Data.unlocksDomain != InfrastructureDomain.None
                ? $"Unlocks: {rb.Data.unlocksDomain}"
                : "";
        }

        btnBuild.onClick.RemoveAllListeners();
        btnBuild.onClick.AddListener(() => onBuildClicked?.Invoke(rb.Data));

        Refresh();
    }

    /// <summary>
    /// Call each frame or when state changes to update visuals.
    /// </summary>
    public void Refresh()
    {
        if (runtimeBuilding == null) return;

        switch (runtimeBuilding.State)
        {
            case BuildingState.Locked:
                txtStatus.text = "<color=grey>LOCKED</color>";
                if (runtimeBuilding.Data.prerequisites != null && runtimeBuilding.Data.prerequisites.Length > 0)
                {
                    string prereqNames = "";
                    foreach (var p in runtimeBuilding.Data.prerequisites)
                        prereqNames += (prereqNames.Length > 0 ? ", " : "") + p.buildingName;
                    txtStatus.text += $"\nRequires: {prereqNames}";
                }
                btnBuild.gameObject.SetActive(false);
                if (progressBar) progressBar.gameObject.SetActive(false);
                break;

            case BuildingState.Available:
                txtStatus.text = "<color=yellow>AVAILABLE</color>";
                btnBuild.gameObject.SetActive(true);
                btnBuild.interactable = true;
                if (progressBar) progressBar.gameObject.SetActive(false);
                break;

            case BuildingState.Constructing:
                float pct = runtimeBuilding.BuildProgress * 100f;
                txtStatus.text = $"<color=cyan>BUILDING — {pct:F0}%</color>";
                btnBuild.gameObject.SetActive(false);
                if (progressBar)
                {
                    progressBar.gameObject.SetActive(true);
                    progressBar.value = runtimeBuilding.BuildProgress;
                }
                break;

            case BuildingState.Built:
                txtStatus.text = "<color=green>BUILT</color>";
                btnBuild.gameObject.SetActive(false);
                if (progressBar) progressBar.gameObject.SetActive(false);
                break;
        }
    }
}