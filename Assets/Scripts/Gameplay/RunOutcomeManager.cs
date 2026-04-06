// Assets/Scripts/Gameplay/RunOutcomeManager.cs
using UnityEngine;

public class RunOutcomeManager : MonoBehaviour
{
    // ── References ────────────────────────────────────────────────────
    [Header("References")]
    [SerializeField] private GameState gameState;
    [SerializeField] private CameraController cameraController;
    [SerializeField] private BaseCameraController baseCameraController;
    [SerializeField] private MissionPanelUI missionPanel;
    [SerializeField] private EndingPanelUI endingPanel;

    // ── Hollow Victory ────────────────────────────────────────────────
    [Header("Hollow Victory")]
    [Tooltip("People Affected count that triggers the Hollow Victory ending.")]
    [SerializeField] private int hollowVictoryPopulation = 80_000_000;

    // ── Civil Unrest ──────────────────────────────────────────────────
    [Header("Civil Unrest")]
    [Tooltip("Chaos must stay above this value to start the unrest timer.")]
    [SerializeField] private float civilUnrestChaosThreshold = 85f;

    [Tooltip("Seconds Chaos must remain above the threshold before Civil Unrest triggers.")]
    [SerializeField] private float civilUnrestGracePeriod = 10f;

    // ── Ending Narratives ─────────────────────────────────────────────
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

    // ── Runtime ───────────────────────────────────────────────────────
    private float chaosAboveThresholdTimer;
    private bool ended;

    // ── Lifecycle ─────────────────────────────────────────────────────
    private void Update()
    {
        if (ended || gameState.IsRunEnded) return;

        RunOutcome outcome = CheckOutcomes();
        if (outcome != RunOutcome.None)
            TriggerEnding(outcome);
    }

    // ── Outcome Checks ────────────────────────────────────────────────
    private RunOutcome CheckOutcomes()
    {
        // Priority 1: Hollow Victory — people affected threshold
        if (gameState.PeopleAffected >= hollowVictoryPopulation)
            return RunOutcome.HollowVictory;

        // Priority 2: Game Over — cure reached maximum
        if (gameState.Cure >= gameState.MaxCure)
            return RunOutcome.GameOver;

        // Priority 3: Civil Unrest — chaos above threshold for too long
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

    // ── Trigger ───────────────────────────────────────────────────────
    private void TriggerEnding(RunOutcome outcome)
    {
        ended = true;
        gameState.EndRun();

        // Lock both camera controllers (only the active one matters,
        // but locking both is safe and avoids checking which is active).
        cameraController.LockInput(true);
        baseCameraController.LockInput(true);

        // Close mission panel if open.
        if (missionPanel != null)
            missionPanel.Close();

        // Resolve narrative text.
        string narrative = outcome switch
        {
            RunOutcome.HollowVictory => hollowVictoryText,
            RunOutcome.GameOver      => gameOverText,
            RunOutcome.CivilUnrest   => civilUnrestText,
            _                        => ""
        };

        endingPanel.Show(outcome, narrative, gameState);
    }
}