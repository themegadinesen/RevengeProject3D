// Assets/Scripts/UI/AgentSlotUI.cs
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AgentSlotUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI txtName;
    [SerializeField] private TextMeshProUGUI txtStats;
    [SerializeField] private Image background;
    [SerializeField] private Button button;

    [Header("Colors")]
    [SerializeField] private Color normalColor   = new Color(0.2f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.1f, 0.5f, 0.1f, 1f);

    private RuntimeAgent agent;
    private Action<RuntimeAgent> onClicked;

    public void Setup(
        RuntimeAgent agent,
        bool selected,
        Action<RuntimeAgent> onClick,
        int rookieStatPenalty)
    {
        this.agent     = agent;
        this.onClicked = onClick;

        string rookieLabel = agent.IsRookie ? " (Rookie)" : "";
        txtName.text = $"{agent.Name}{rookieLabel}";

        int missionINT = agent.GetMissionINT(rookieStatPenalty);
        int missionSTR = agent.GetMissionSTR(rookieStatPenalty);
        int missionAGI = agent.GetMissionAGI(rookieStatPenalty);

        txtStats.text = $"INT {missionINT}   STR {missionSTR}   AGI {missionAGI}";

        string modifierSummary = agent.GetMissionModifierSummary(rookieStatPenalty);
        if (!string.IsNullOrWhiteSpace(modifierSummary))
            txtStats.text += $"\n{modifierSummary}";

        background.color = selected ? selectedColor : normalColor;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClicked?.Invoke(this.agent));
    }

    public void SetSelected(bool selected)
    {
        background.color = selected ? selectedColor : normalColor;
    }
}