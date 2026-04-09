// Assets/Scripts/UI/BaseBuildPanelUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BaseBuildPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BaseProgressionManager baseProgression;
    [SerializeField] private GameState gameState;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI txtTitle;
    [SerializeField] private Transform       buildingListContent;
    [SerializeField] private BuildingSlotUI  buildingSlotPrefab;
    [SerializeField] private Button          btnClose;

    [Header("Optional Info")]
    [Tooltip("Shows active bonuses from built buildings.")]
    [SerializeField] private TextMeshProUGUI txtBonusSummary;

    private readonly List<BuildingSlotUI> spawnedSlots = new();

    private void OnEnable()
    {
        btnClose.onClick.AddListener(Close);
        if (baseProgression != null)
            baseProgression.OnBuildingStateChanged += OnBuildingChanged;

        RebuildList();
    }

    private void OnDisable()
    {
        btnClose.onClick.RemoveListener(Close);
        if (baseProgression != null)
            baseProgression.OnBuildingStateChanged -= OnBuildingChanged;
    }

    private void Update()
    {
        // Live-update construction progress bars.
        foreach (var slot in spawnedSlots)
            if (slot != null) slot.Refresh();
    }

    public void Open()
    {
        if (baseProgression == null || !baseProgression.IsProgressionActive) return;
        gameObject.SetActive(true);
        RebuildList();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    private void OnBuildingChanged(RuntimeBuilding rb)
    {
        RebuildList();
    }

    private void RebuildList()
    {
        foreach (var slot in spawnedSlots)
            if (slot != null) Destroy(slot.gameObject);
        spawnedSlots.Clear();

        if (baseProgression == null) return;

        RuntimeBuilding[] all = baseProgression.GetAllRuntimeBuildings();
        if (all == null) return;

        if (txtTitle != null)
            txtTitle.text = "Base Buildings";

        for (int i = 0; i < all.Length; i++)
        {
            BuildingSlotUI slot = Instantiate(buildingSlotPrefab, buildingListContent);
            slot.Setup(all[i], OnBuildClicked);
            spawnedSlots.Add(slot);
        }

        RefreshBonusSummary();
    }

    private void OnBuildClicked(BuildingData data)
    {
        if (baseProgression == null) return;
        if (gameState.Money < data.moneyCost) return;

        baseProgression.TryStartConstruction(data);
        // List rebuilds via OnBuildingStateChanged event.
    }

    private void RefreshBonusSummary()
    {
        if (txtBonusSummary == null) return;

        float succ  = baseProgression.TotalMissionSuccessBonus * 100f;
        float money = baseProgression.TotalMoneyGainBonus * 100f;
        int   cap   = baseProgression.TotalAgentCapacityBonus;

        txtBonusSummary.text =
            $"Bonuses — Success: +{succ:F0}%  |  " +
            $"Money: +{money:F0}%  |  " +
            $"Agent Cap: +{cap}";
    }
}