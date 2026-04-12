using UnityEngine;

public class RunOutcomeManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameState gameState;
    [SerializeField] private CameraController cameraController;
    [SerializeField] private BaseCameraController baseCameraController;
    [SerializeField] private MissionPanelUI missionPanel;
    [SerializeField] private RecruitmentPanelUI recruitmentPanel;
    [SerializeField] private EndingPanelUI endingPanel;

    [Header("Hollow Victory")]
    [Tooltip("People Affected count that triggers the Hollow Victory ending.")]
    [SerializeField] private int hollowVictoryPopulation = 80_000_000;

    [Header("Civil Unrest")]
    [Tooltip("Chaos must stay above this value to start the unrest timer.")]
    [SerializeField] private float civilUnrestChaosThreshold = 85f;
    [Tooltip("Seconds Chaos must remain above the threshold before Civil Unrest triggers.")]
    [SerializeField] private float civilUnrestGracePeriod = 10f;

    [Header("Ending Narratives")]
    [TextArea(2, 4)]
    [SerializeField] private string hollowVictoryText =
        "The cure was sabotaged. Millions were affected, but the regime calls it a victory.";

    [TextArea(2, 4)]
    [SerializeField] private string gameOverText =
        "The cure has been completed. Your efforts were in vain.";

    [TextArea(2, 4)]
    [SerializeField] private string civilUnrestText =
        "Chaos spiraled out of control. The streets burn, and order has collapsed.";

    private float chaosAboveThresholdTimer;
    private bool ended;

    private void Update()
    {
        if (ended || gameState.IsRunEnded) return;

        RunOutcome outcome = CheckOutcomes();
        if (outcome != RunOutcome.None)
            TriggerEnding(outcome);
    }

    private RunOutcome CheckOutcomes()
    {
        if (gameState.PeopleAffected >= hollowVictoryPopulation)
            return RunOutcome.HollowVictory;

        if (gameState.Cure >= gameState.MaxCure)
            return RunOutcome.GameOver;

        if (gameState.Chaos >= civilUnrestChaosThreshold)
        {
            chaosAboveThresholdTimer += Time.deltaTime;
            if (chaosAboveThresholdTimer >= civilUnrestGracePeriod)
                return RunOutcome.CivilUnrest;
        }
        else
        {
            chaosAboveThresholdTimer = 0f;
        }

        return RunOutcome.None;
    }

    private void TriggerEnding(RunOutcome outcome)
    {
        ended = true;
        gameState.EndRun();

        cameraController.LockInput(true);
        baseCameraController.LockInput(true);

        if (missionPanel != null)
            missionPanel.Close();

        if (recruitmentPanel != null)
            recruitmentPanel.Close();

        string narrative = outcome switch
        {
            RunOutcome.HollowVictory => hollowVictoryText,
            RunOutcome.GameOver => gameOverText,
            RunOutcome.CivilUnrest => civilUnrestText,
            _ => ""
        };

        endingPanel.Show(outcome, narrative, gameState);
    }
}