// Assets/Scripts/UI/DistrictSlotUI.cs
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A single entry in the district selection list inside MissionPanelUI.
/// Reusable prefab, similar to AgentSlotUI.
/// </summary>
public class DistrictSlotUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI txtName;
    [SerializeField] private TextMeshProUGUI txtInfo;
    [SerializeField] private Image  background;
    [SerializeField] private Button button;

    [Header("Colors")]
    [SerializeField] private Color normalColor   = new Color(0.2f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.1f, 0.5f, 0.1f, 1f);

    private RuntimeDistrict district;
    private Action<RuntimeDistrict> onClicked;

    public void Setup(RuntimeDistrict rd, Action<RuntimeDistrict> onClick)
    {
        district   = rd;
        onClicked  = onClick;

        txtName.text = rd.Data.districtName;

        if (txtInfo != null)
        {
            int missionCount = rd.Data.missions != null ? rd.Data.missions.Length : 0;
            txtInfo.text = $"{rd.Data.industryFocus}  •  {missionCount} mission{(missionCount != 1 ? "s" : "")}";
        }

        background.color = normalColor;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClicked?.Invoke(district));
    }

    public void SetSelected(bool selected)
    {
        background.color = selected ? selectedColor : normalColor;
    }
}