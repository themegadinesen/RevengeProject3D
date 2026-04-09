// Assets/Scripts/Map/DistrictMapInput.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Single script that handles district hover and click via Physics2D raycasting.
/// Drives DistrictHoverUI, DistrictDetailsPanel, and DistrictMapNode visuals.
/// Place on any always-active GameObject (e.g. a "MapInput" empty in the scene).
/// </summary>
public class DistrictMapInput : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera            cam;
    [SerializeField] private ViewManager       viewManager;
    [SerializeField] private GameState         gameState;
    [SerializeField] private DistrictManager   districtManager;
    [SerializeField] private DistrictHoverUI   hoverUI;
    [SerializeField] private DistrictDetailsPanel detailsPanel;

    [Header("Raycast")]
    [Tooltip("Layer(s) your district GameObjects are on.")]
    [SerializeField] private LayerMask districtLayer;

    // ── Runtime ───────────────────────────────────────────────────────
    private DistrictMapNode[] allNodes;
    private DistrictMapNode   hoveredNode;
    private DistrictMapNode   selectedNode;

    private void Start()
    {
        allNodes = FindObjectsByType<DistrictMapNode>(FindObjectsSortMode.None);
    }

    private void Update()
    {
        // Only process in Map view.
        if (viewManager.CurrentState != ViewManager.ViewState.Map)
        {
            ClearHover();
            if (selectedNode != null) CloseDetails();
            return;
        }

        if (gameState.IsRunEnded)
        {
            ClearHover();
            return;
        }

        // Don't raycast when pointer is over UI.
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            ClearHover();
            return;
        }

        if (Mouse.current == null) return;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Vector2 mouseWorld  = cam.ScreenToWorldPoint(mouseScreen);

        Collider2D hit = Physics2D.OverlapPoint(mouseWorld, districtLayer);

        if (hit != null && hit.TryGetComponent(out DistrictMapNode node))
        {
            SetHovered(node, mouseScreen);

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                RuntimeDistrict rd = districtManager.GetRuntimeDistrict(node.Data);
                if (rd != null && rd.IsUnlocked)
                    SetSelected(node, rd);
            }
        }
        else
        {
            ClearHover();

            if (Mouse.current.leftButton.wasPressedThisFrame)
                CloseDetails();
        }

        UpdateNodeVisuals();
    }

    // ── Hover ─────────────────────────────────────────────────────────
    private void SetHovered(DistrictMapNode node, Vector2 screenPos)
    {
        hoveredNode = node;

        RuntimeDistrict rd = districtManager.GetRuntimeDistrict(node.Data);
        if (rd != null)
            hoverUI.Show(rd, screenPos);
    }

    private void ClearHover()
    {
        if (hoveredNode == null) return;
        hoveredNode = null;
        hoverUI.Hide();
    }

    // ── Selection ─────────────────────────────────────────────────────
    private void SetSelected(DistrictMapNode node, RuntimeDistrict rd)
    {
        selectedNode = node;
        districtManager.SelectDistrict(rd);
        detailsPanel.Open(rd);
    }

    private void CloseDetails()
    {
        selectedNode = null;
        detailsPanel.Close();
        // Don't clear DistrictManager.SelectedDistrict here —
        // keep last selection for mission access convenience.
    }

    // ── Node Visuals ──────────────────────────────────────────────────
    private void UpdateNodeVisuals()
    {
        bool isHovering = hoveredNode != null;

        for (int i = 0; i < allNodes.Length; i++)
        {
            DistrictMapNode node = allNodes[i];
            RuntimeDistrict rd   = districtManager.GetRuntimeDistrict(node.Data);

            if (rd == null || !rd.IsUnlocked)
            {
                node.SetVisualState(DistrictVisualState.Locked);
                continue;
            }

            if (node == hoveredNode)
                node.SetVisualState(DistrictVisualState.Hovered);
            else if (node == selectedNode)
                node.SetVisualState(DistrictVisualState.Selected);
            else if (isHovering)
                node.SetVisualState(DistrictVisualState.Dimmed);
            else
                node.SetVisualState(DistrictVisualState.Normal);
        }
    }
}