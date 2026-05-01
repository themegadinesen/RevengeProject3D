using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DistrictSlotUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI txtName;
    [SerializeField] private TextMeshProUGUI txtInfo;
    [SerializeField] private Image background;
    [SerializeField] private Button button;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new(0.2f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color selectedColor = new(0.1f, 0.5f, 0.1f, 1f);
    [SerializeField] private Color lockedColor = new(0.12f, 0.12f, 0.12f, 0.8f);

    private RuntimeDistrict district;
    private Action<RuntimeDistrict> onClicked;
    private bool selected;
    private bool interactable;

    public void Setup(RuntimeDistrict rd, Action<RuntimeDistrict> onClick)
    {
        district = rd;
        onClicked = onClick;
        selected = false;
        interactable = rd != null && rd.IsUnlocked;

        txtName.text = rd.Data.districtName;

        if (txtInfo != null)
        {
            int missionCount = rd.Data.missions != null ? rd.Data.missions.Length : 0;
            txtInfo.text = rd.IsUnlocked
                ? $"{rd.Data.industryFocus} | {missionCount} mission{(missionCount != 1 ? "s" : "")}"
                : $"LOCKED | {rd.Data.peopleAffectedUnlockThreshold:#,0} people affected";
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClicked?.Invoke(district));
        RefreshVisual();
    }

    public void SetSelected(bool selected)
    {
        this.selected = selected;
        RefreshVisual();
    }

    public void SetInteractable(bool interactable)
    {
        this.interactable = interactable;
        RefreshVisual();
    }

    private void RefreshVisual()
    {
        if (button != null)
            button.interactable = interactable;

        if (background != null)
            background.color = !interactable ? lockedColor : selected ? selectedColor : normalColor;
    }
}