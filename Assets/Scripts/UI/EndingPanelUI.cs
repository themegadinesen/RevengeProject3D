using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class EndingPanelUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI txtOutcomeTitle;
    [SerializeField] private TextMeshProUGUI txtNarrative;
    [SerializeField] private TextMeshProUGUI txtFinalStats;
    [SerializeField] private Button btnRestart;

    private void OnEnable()
    {
        if (btnRestart != null)
            btnRestart.onClick.AddListener(OnRestartClicked);
    }

    private void OnDisable()
    {
        if (btnRestart != null)
            btnRestart.onClick.RemoveListener(OnRestartClicked);
    }

    public void Show(RunOutcome outcome, string narrative, GameState state)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

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
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void OnRestartClicked()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}