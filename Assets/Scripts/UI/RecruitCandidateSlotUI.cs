// Assets/Scripts/UI/RecruitCandidateSlotUI.cs
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RecruitCandidateSlotUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI txtName;
    [SerializeField] private TextMeshProUGUI txtStats;
    [SerializeField] private Image imgPortrait;
    [SerializeField] private Graphic selectionGraphic;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(0.75f, 0.95f, 0.75f, 1f);

    private Action<PendingRecruitCandidate> clickCallback;

    public PendingRecruitCandidate Candidate { get; private set; }

    private void OnEnable()
    {
        if (button != null)
            button.onClick.AddListener(HandleClicked);
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }

    public void Setup(
        PendingRecruitCandidate candidate,
        bool isSelected,
        Action<PendingRecruitCandidate> onClicked)
    {
        Candidate = candidate;
        clickCallback = onClicked;

        if (txtName != null)
            txtName.text = candidate != null ? candidate.CandidateName : "Unknown Candidate";

        if (txtStats != null)
        {
            txtStats.text = candidate != null
                ? $"INT {candidate.INT}  STR {candidate.STR}  AGI {candidate.AGI}"
                : "";
        }

        if (imgPortrait != null)
        {
            bool hasPortrait = candidate != null && candidate.Portrait != null;
            imgPortrait.sprite = hasPortrait ? candidate.Portrait : null;
            imgPortrait.enabled = hasPortrait;
        }

        SetSelected(isSelected);
    }

    public void SetSelected(bool isSelected)
    {
        if (selectionGraphic != null)
            selectionGraphic.color = isSelected ? selectedColor : normalColor;
    }

    private void HandleClicked()
    {
        clickCallback?.Invoke(Candidate);
    }
}