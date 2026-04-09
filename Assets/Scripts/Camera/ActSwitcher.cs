// Assets/Scripts/Camera/ActSwitcher.cs
using UnityEngine;

/// <summary>
/// Swaps the active base and zoom anchor between acts.
/// Call SwitchToAct2() from your game logic when Act 1 is completed.
/// </summary>
public class ActSwitcher : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraController cameraController;
    [SerializeField] private ViewManager viewManager;

    [Header("Base Progression")]
    [Tooltip("Optional. If assigned, progression activates on Act 2 switch.")]
    [SerializeField] private BaseProgressionManager baseProgression;

    [Header("Act 1")]
    [SerializeField] private GameObject act1Base;
    [SerializeField] private ZoomAnchor act1Anchor;
    [SerializeField] private CameraZoomConfig act1Config;

    [Header("Act 2")]
    [SerializeField] private GameObject act2Base;
    [SerializeField] private ZoomAnchor act2Anchor;
    [Tooltip("Optional: assign a different config if Act 2 needs different zoom limits. " +
             "Leave empty to keep using Act 1's config.")]
    [SerializeField] private CameraZoomConfig act2Config;

    /// <summary>
    /// Call this when Act 1 is completed.
    /// Forces the player back to map view first, then swaps everything.
    /// </summary>
    public void SwitchToAct2()
    {
        // If currently in base view, kick back to map first.
        if (viewManager.CurrentState == ViewManager.ViewState.Base)
        {
            cameraController.GoToMapView();
        }

        // Swap base geometry.
        act1Base.SetActive(false);
        act2Base.SetActive(true);

        // Swap anchor — CameraController and ViewManager read this automatically.
        cameraController.SetAnchor(act2Anchor);

        // Swap config if provided.
        if (act2Config != null)
            cameraController.SetConfig(act2Config);

        // Activate base progression.
        if (baseProgression != null)
            baseProgression.ActivateProgression();
    }
}