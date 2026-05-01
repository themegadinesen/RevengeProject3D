using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class ImpactEventPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorldEventDirector worldEventDirector;

    [Header("Panel")]
    [Tooltip("Optional visible root. Prefer assigning a child root so this subscriber can stay enabled.")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI txtTitle;
    [SerializeField] private TextMeshProUGUI txtDescription;

    [Header("Startup")]
    [SerializeField] private bool hideOnStart = true;
    [SerializeField] private bool hideInEditMode = true;

    [Header("Choices")]
    [SerializeField] private Button[] choiceButtons;
    [Tooltip("Optional labels matching Choice Buttons. If empty, labels are found on each button.")]
    [SerializeField] private TextMeshProUGUI[] choiceLabels;
    [SerializeField] private string unavailableSuffix = " (Unavailable)";

    private WorldEventContext currentContext;
    private CanvasGroup selfCanvasGroup;

    private void Reset()
    {
        HideInEditorIfNeeded();
    }

    private void Awake()
    {
        ResolveDirector();
        EnsureSelfCanvasGroupIfNeeded();
    }

    private void Start()
    {
        if (hideOnStart)
            SetVisible(false);
    }

    private void OnEnable()
    {
        ResolveDirector();

        if (worldEventDirector != null)
        {
            worldEventDirector.OnImpactEventStarted += ShowEvent;
            worldEventDirector.OnImpactEventResolved += OnImpactResolved;
            worldEventDirector.OnImpactEventCancelled += HideEvent;
        }
    }

    private void OnDisable()
    {
        if (worldEventDirector != null)
        {
            worldEventDirector.OnImpactEventStarted -= ShowEvent;
            worldEventDirector.OnImpactEventResolved -= OnImpactResolved;
            worldEventDirector.OnImpactEventCancelled -= HideEvent;
        }

        ClearChoiceListeners();
    }

    private void ShowEvent(WorldEventContext context)
    {
        if (context == null || context.ImpactDefinition == null)
            return;

        currentContext = context;

        if (txtTitle != null)
            txtTitle.text = context.Title;

        if (txtDescription != null)
            txtDescription.text = context.Message;

        RefreshChoices();
        SetVisible(true);
    }

    private void OnImpactResolved(WorldEventContext context, ImpactEventChoice choice)
    {
        HideEvent();
    }

    private void HideEvent()
    {
        currentContext = null;
        ClearChoiceListeners();
        SetVisible(false);
    }

    private void RefreshChoices()
    {
        ClearChoiceListeners();

        if (choiceButtons == null)
            return;

        ImpactEventChoice[] choices = currentContext?.ImpactDefinition?.choices;
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            Button button = choiceButtons[i];
            if (button == null)
                continue;

            bool hasChoice = choices != null && i < choices.Length && choices[i] != null;
            button.gameObject.SetActive(hasChoice);

            if (!hasChoice)
                continue;

            int choiceIndex = i;
            bool canApply = worldEventDirector != null &&
                            worldEventDirector.CanApplyImpactChoice(choiceIndex);

            button.interactable = canApply;
            button.onClick.AddListener(() => OnChoiceClicked(choiceIndex));

            TextMeshProUGUI label = GetChoiceLabel(choiceIndex, button);
            if (label != null)
                label.text = BuildChoiceText(choices[choiceIndex], canApply);
        }
    }

    private void OnChoiceClicked(int choiceIndex)
    {
        if (worldEventDirector == null)
            return;

        if (!worldEventDirector.TryResolveImpactChoice(choiceIndex))
            RefreshChoices();
    }

    private string BuildChoiceText(ImpactEventChoice choice, bool canApply)
    {
        if (choice == null)
            return string.Empty;

        string label = string.IsNullOrWhiteSpace(choice.label)
            ? "Choice"
            : choice.label;

        if (!canApply)
            label += unavailableSuffix;

        string text = $"<b>{label}</b>";

        if (!string.IsNullOrWhiteSpace(choice.description))
            text += $"\n{choice.description}";

        if (!string.IsNullOrWhiteSpace(choice.consequencePreview))
            text += $"\n{choice.consequencePreview}";

        return text;
    }

    private TextMeshProUGUI GetChoiceLabel(int index, Button button)
    {
        if (choiceLabels != null &&
            index >= 0 &&
            index < choiceLabels.Length &&
            choiceLabels[index] != null)
        {
            return choiceLabels[index];
        }

        return button != null
            ? button.GetComponentInChildren<TextMeshProUGUI>(true)
            : null;
    }

    private void ClearChoiceListeners()
    {
        if (choiceButtons == null)
            return;

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] != null)
                choiceButtons[i].onClick.RemoveAllListeners();
        }
    }

    private void SetVisible(bool visible)
    {
        if (panelRoot != null && panelRoot != gameObject)
        {
            panelRoot.SetActive(visible);
            return;
        }

        SetSelfCanvasGroupVisible(visible);
    }

    private void ResolveDirector()
    {
        if (worldEventDirector == null)
            worldEventDirector = FindFirstObjectByType<WorldEventDirector>();
    }

    private void OnValidate()
    {
        HideInEditorIfNeeded();
    }

    private void EnsureSelfCanvasGroupIfNeeded()
    {
        if (panelRoot != null && panelRoot != gameObject)
            return;

        if (selfCanvasGroup == null)
            selfCanvasGroup = GetComponent<CanvasGroup>();

        if (selfCanvasGroup == null)
            selfCanvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void SetSelfCanvasGroupVisible(bool visible)
    {
        EnsureSelfCanvasGroupIfNeeded();

        if (selfCanvasGroup == null)
            return;

        selfCanvasGroup.alpha = visible ? 1f : 0f;
        selfCanvasGroup.interactable = visible;
        selfCanvasGroup.blocksRaycasts = visible;
    }

    private void HideInEditorIfNeeded()
    {
        if (Application.isPlaying || !hideInEditMode)
            return;

        if (panelRoot != null && panelRoot != gameObject)
        {
            panelRoot.SetActive(false);
            return;
        }

        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}
