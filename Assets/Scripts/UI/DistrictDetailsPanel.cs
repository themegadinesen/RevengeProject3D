// Assets/Scripts/UI/DistrictDetailsPanel.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panel that opens when the player clicks an unlocked district on the map.
/// Shows only static profile info (name, industry, lore).
/// Live stats are shown by DistrictHoverUI on hover instead.
/// </summary>
public class DistrictDetailsPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DistrictManager districtManager;

    [Header("Profile")]
    [SerializeField] private TextMeshProUGUI txtDistrictName;
    [SerializeField] private TextMeshProUGUI txtIndustry;
    [SerializeField] private TextMeshProUGUI txtLore;
    [SerializeField] private TextMeshProUGUI txtHeat;
    [SerializeField] private TextMeshProUGUI txtResponseState;
    [SerializeField] private TextMeshProUGUI txtPressure;

    [Header("Buttons")]
    [SerializeField] private Button btnClose;

    private RuntimeDistrict currentDistrict;

    private void OnEnable()
    {
        btnClose.onClick.AddListener(Close);
    }

    private void OnDisable()
    {
        btnClose.onClick.RemoveListener(Close);
    }

    private void Update()
    {
        if (!gameObject.activeSelf || currentDistrict == null) return;
        RefreshLiveStats();
    }

    // ── Public API (called by DistrictMapInput) ───────────────────────
    public void Open(RuntimeDistrict rd)
    {
        currentDistrict = rd;
        txtDistrictName.text = rd.Data.districtName;
        if (txtIndustry != null) txtIndustry.text = $"Industry: {rd.Data.industryFocus}";
        if (txtLore     != null) txtLore.text     = rd.Data.loreText;
        RefreshLiveStats();

        gameObject.SetActive(true);
    }

    public void Close()
    {
        currentDistrict = null;
        gameObject.SetActive(false);
    }

    private void RefreshLiveStats()
    {
        if (currentDistrict == null) return;

        if (txtHeat != null)
            txtHeat.text = $"Heat: {currentDistrict.LocalHeat:F1} / {currentDistrict.MaxHeat}";

        if (txtResponseState != null)
        {
            DistrictResponseState state = districtManager != null
                ? districtManager.GetResponseState(currentDistrict)
                : DistrictResponseState.Calm;

            txtResponseState.text = $"Response: {state}";
        }

        if (txtPressure != null)
        {
            float pressure = districtManager != null
                ? districtManager.GetInvestigationPressure(currentDistrict)
                : 0f;

            txtPressure.text = $"Investigation Pressure: +{pressure:F1} Cure/s";
        }
    }
}