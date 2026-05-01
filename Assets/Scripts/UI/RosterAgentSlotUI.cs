using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RosterAgentSlotUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI txtName;
    [SerializeField] private TextMeshProUGUI txtStats;
    [SerializeField] private TextMeshProUGUI txtTrait;
    [SerializeField] private TextMeshProUGUI txtMeta;
    [SerializeField] private Image background;

    [Header("Colors")]
    [SerializeField] private Color availableColor = new(0.18f, 0.24f, 0.18f, 0.95f);
    [SerializeField] private Color busyColor = new(0.28f, 0.22f, 0.12f, 0.95f);
    [SerializeField] private Color lostColor = new(0.20f, 0.20f, 0.20f, 0.85f);

    public RuntimeAgent Agent { get; private set; }

    public void Setup(RuntimeAgent agent, int rookieStatPenalty)
    {
        Agent = agent;
        EnsureReferences();

        if (agent == null)
        {
            SetEmpty();
            return;
        }

        if (txtName != null)
            txtName.text = $"{agent.Name}  |  {agent.RankDisplayName}";

        int missionINT = agent.GetMissionINT(rookieStatPenalty);
        int missionSTR = agent.GetMissionSTR(rookieStatPenalty);
        int missionAGI = agent.GetMissionAGI(rookieStatPenalty);

        if (txtStats != null)
        {
            txtStats.text = $"INT {missionINT}  |  STR {missionSTR}  |  AGI {missionAGI}";

            if (missionINT != agent.INT || missionSTR != agent.STR || missionAGI != agent.AGI)
                txtStats.text += $"  (Base {agent.INT}/{agent.STR}/{agent.AGI})";
        }

        if (txtTrait != null)
            txtTrait.text = BuildTraitLine(agent);

        if (txtMeta != null)
        {
            string rookieText = agent.IsRookie
                ? $"Rookie {agent.CompletedMissionCount}/{agent.RookieMissionsRequired}"
                : "Rookie: No";

            txtMeta.text =
                $"Status: {agent.Status}  |  {rookieText}  |  Completed: {agent.CompletedMissionCount}";
        }

        RefreshBackground(agent.Status);
    }

    private void SetEmpty()
    {
        if (txtName != null)
            txtName.text = "Missing Agent";

        if (txtStats != null)
            txtStats.text = string.Empty;

        if (txtTrait != null)
            txtTrait.text = string.Empty;

        if (txtMeta != null)
            txtMeta.text = string.Empty;

        if (background != null)
            background.color = lostColor;
    }

    private static string BuildTraitLine(RuntimeAgent agent)
    {
        bool hasTraitName = !string.IsNullOrWhiteSpace(agent.FlavorTrait);
        bool hasTraitEffect = agent.TraitGameplayEffect != RecruitmentTraitGameplayEffect.None;

        if (!hasTraitName && !hasTraitEffect)
            return "Trait: None";

        string traitName = hasTraitName ? agent.FlavorTrait : "Trait";
        string gameplay = hasTraitEffect ? agent.TraitGameplaySummary : "No gameplay effect";
        return $"Trait: {traitName} ({gameplay})";
    }

    private void RefreshBackground(AgentStatus status)
    {
        if (background == null)
            return;

        background.color = status switch
        {
            AgentStatus.Busy => busyColor,
            AgentStatus.Lost => lostColor,
            _ => availableColor
        };
    }

    private void EnsureReferences()
    {
        if (background == null)
            background = GetComponent<Image>();

        if (background == null)
            background = gameObject.AddComponent<Image>();

        if (txtName != null && txtStats != null && txtTrait != null && txtMeta != null)
            return;

        TextMeshProUGUI[] existingTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < existingTexts.Length; i++)
        {
            TextMeshProUGUI text = existingTexts[i];
            if (text == null)
                continue;

            string objectName = text.gameObject.name;
            if (txtName == null && objectName.Contains("Name"))
                txtName = text;
            else if (txtStats == null && objectName.Contains("Stats"))
                txtStats = text;
            else if (txtTrait == null && objectName.Contains("Trait"))
                txtTrait = text;
            else if (txtMeta == null && objectName.Contains("Meta"))
                txtMeta = text;
        }

        if (txtName == null)
            txtName = CreateText("TxtName", 20, FontStyles.Bold);

        if (txtStats == null)
            txtStats = CreateText("TxtStats", 16, FontStyles.Normal);

        if (txtTrait == null)
            txtTrait = CreateText("TxtTrait", 15, FontStyles.Normal);

        if (txtMeta == null)
            txtMeta = CreateText("TxtMeta", 14, FontStyles.Italic);
    }

    private TextMeshProUGUI CreateText(string objectName, float fontSize, FontStyles style)
    {
        GameObject textObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(transform, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = TextAlignmentOptions.Left;

        LayoutElement layout = textObject.AddComponent<LayoutElement>();
        layout.minHeight = fontSize + 4f;
        layout.preferredHeight = fontSize + 6f;
        layout.flexibleWidth = 1f;

        return text;
    }
}
