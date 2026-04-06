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

    public void Setup(RuntimeAgent agent, bool selected, Action<RuntimeAgent> onClick)
    {
        this.agent     = agent;
        this.onClicked = onClick;

        txtName.text  = agent.Name;
        txtStats.text = $"INT {agent.INT}   STR {agent.STR}   AGI {agent.AGI}";

        background.color = selected ? selectedColor : normalColor;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClicked?.Invoke(this.agent));
    }

    public void SetSelected(bool selected)
    {
        background.color = selected ? selectedColor : normalColor;
    }
}