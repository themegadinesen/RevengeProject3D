// Assets/Scripts/UI/MissionSlotUI.cs
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionSlotUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI txtName;
    [SerializeField] private TextMeshProUGUI txtInfo;
    [SerializeField] private Image background;
    [SerializeField] private Button button;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new(0.2f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color selectedColor = new(0.1f, 0.5f, 0.1f, 1f);
    [SerializeField] private Color activeColor = new(0.35f, 0.25f, 0.08f, 1f);

    private MissionData mission;
    private Action<MissionData> onClicked;

    public void Setup(MissionData mission, bool selected, bool active, Action<MissionData> onClick)
    {
        this.mission = mission;
        onClicked = onClick;

        txtName.text = mission != null ? mission.missionName : "Missing Mission";

        if (txtInfo != null && mission != null)
        {
            string duration = mission.duration <= 0f ? "Instant" : $"{mission.duration:F0}s";
            string activeLabel = active ? " | IN PROGRESS" : "";
            txtInfo.text = $"${mission.moneyCost} | INT {mission.requiredINT} STR {mission.requiredSTR} AGI {mission.requiredAGI} | {duration}{activeLabel}";
        }

        SetSelected(selected, active);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClicked?.Invoke(this.mission));
    }

    public void SetSelected(bool selected, bool active)
    {
        if (background == null) return;
        background.color = selected ? selectedColor : active ? activeColor : normalColor;
    }
}