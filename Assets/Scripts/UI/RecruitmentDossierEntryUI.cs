using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RecruitmentDossierEntryUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI txtLabel;
    [SerializeField] private TextMeshProUGUI txtBody;
    [SerializeField] private GameObject tagRoot;
    [SerializeField] private TextMeshProUGUI txtTag;
    [SerializeField] private Graphic backgroundGraphic;
    [SerializeField] private Graphic accentGraphic;

    public void Setup(
        string label,
        string body,
        string tag,
        Color backgroundColor,
        Color accentColor)
    {
        if (txtLabel != null)
            txtLabel.text = string.IsNullOrWhiteSpace(label) ? "Untitled" : label;

        if (txtBody != null)
            txtBody.text = string.IsNullOrWhiteSpace(body) ? "-" : body;

        bool showTag = !string.IsNullOrWhiteSpace(tag);

        if (tagRoot != null)
            tagRoot.SetActive(showTag);

        if (txtTag != null)
            txtTag.text = showTag ? tag : string.Empty;

        if (backgroundGraphic != null)
            backgroundGraphic.color = backgroundColor;

        if (accentGraphic != null)
            accentGraphic.color = accentColor;
    }
}