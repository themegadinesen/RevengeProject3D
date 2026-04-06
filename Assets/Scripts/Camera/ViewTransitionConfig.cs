using UnityEngine;

[CreateAssetMenu(fileName = "NewViewTransitionConfig", menuName = "Camera/View Transition Config")]
public class ViewTransitionConfig : ScriptableObject
{
    [Header("Threshold")]
    [Tooltip("When camera ortho size drops to this, begin zooming into base.")]
    public float enterBaseThreshold = 6f;

    [Header("Zoom Blur")]
    [Range(0.05f, 1f)]
    public float peakBlurStrength = 0.4f;

    [Range(0.1f, 1f)]
    public float transitionDuration = 0.3f;

    public AnimationCurve blurCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 4f),
        new Keyframe(0.5f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, -4f, 0f)
    );

    [Range(0.1f, 0.9f)]
    public float swapPoint = 0.5f;
}