// Assets/Scripts/Map/DistrictMapNode.cs
using UnityEngine;

public enum DistrictVisualState
{
    Normal,
    Hovered,
    Selected,
    Dimmed,
    Locked
}

/// <summary>
/// Attach to each district's scene GameObject alongside a SpriteRenderer
/// and a Collider2D (PolygonCollider2D recommended for irregular shapes).
/// The SpriteRenderer can be a semi-transparent overlay covering the district region.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class DistrictMapNode : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("Drag the matching DistrictData ScriptableObject here.")]
    [SerializeField] private DistrictData districtData;

    [Header("Colors")]
    [SerializeField] private Color normalColor   = new Color(1f, 1f, 1f, 0.3f);
    [SerializeField] private Color hoverColor    = new Color(1f, 0.9f, 0.4f, 0.6f);
    [SerializeField] private Color selectedColor = new Color(0.4f, 1f, 0.5f, 0.6f);
    [SerializeField] private Color dimmedColor   = new Color(0.5f, 0.5f, 0.5f, 0.15f);
    [SerializeField] private Color lockedColor   = new Color(0.3f, 0.3f, 0.3f, 0.2f);

    private SpriteRenderer sr;

    public DistrictData Data => districtData;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        sr.color = normalColor;
    }

    public void SetVisualState(DistrictVisualState state)
    {
        sr.color = state switch
        {
            DistrictVisualState.Hovered  => hoverColor,
            DistrictVisualState.Selected => selectedColor,
            DistrictVisualState.Dimmed   => dimmedColor,
            DistrictVisualState.Locked   => lockedColor,
            _                            => normalColor
        };
    }
}