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
    [Header("Profile")]
    [SerializeField] private TextMeshProUGUI txtDistrictName;
    [SerializeField] private TextMeshProUGUI txtIndustry;
    [SerializeField] private TextMeshProUGUI txtLore;

    [Header("Buttons")]
    [SerializeField] private Button btnClose;

    private void OnEnable()
    {
        btnClose.onClick.AddListener(Close);
    }

    private void OnDisable()
    {
        btnClose.onClick.RemoveListener(Close);
    }

    // ── Public API (called by DistrictMapInput) ───────────────────────
    public void Open(RuntimeDistrict rd)
    {
        txtDistrictName.text = rd.Data.districtName;
        if (txtIndustry != null) txtIndustry.text = $"Industry: {rd.Data.industryFocus}";
        if (txtLore     != null) txtLore.text     = rd.Data.loreText;

        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}