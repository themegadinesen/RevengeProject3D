// Assets/Scripts/UI/EndingPanelUI.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class EndingPanelUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI txtOutcomeTitle;
    [SerializeField] private TextMeshProUGUI txtNarrative;
    [SerializeField] private TextMeshProUGUI txtFinalStats;
    [SerializeField] private Button btnRestart;

    private void Awake()
    {
        panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        btnRestart.onClick.AddListener(OnRestartClicked);
    }

    private void OnDisable()
    {
        btnRestart.onClick.RemoveListener(OnRestartClicked);
    }

    public void Show(RunOutcome outcome, string narrative, GameState state)
    {
        txtOutcomeTitle.text = outcome switch
        {
            RunOutcome.HollowVictory => "HOLLOW VICTORY",
            RunOutcome.GameOver      => "GAME OVER",
            RunOutcome.CivilUnrest   => "CIVIL UNREST",
            _                        => "END"
        };

        txtNarrative.text = narrative;

        txtFinalStats.text =
            $"People Affected: {state.PeopleAffected:#,0}\n" +
            $"Chaos: {state.Chaos:F1}\n" +
            $"Cure: {state.Cure:F1} / {state.MaxCure}\n" +
            $"Money: ${state.Money}";

        panelRoot.SetActive(true);
    }

    private void OnRestartClicked()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}