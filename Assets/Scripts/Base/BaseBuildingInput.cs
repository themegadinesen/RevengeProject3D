using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class BaseBuildingInput : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private ViewManager viewManager;
    [SerializeField] private GameState gameState;
    [SerializeField] private BaseProgressionManager baseProgression;
    [SerializeField] private BaseBuildingDetailsPanelUI buildingDetailsPanel;
    [SerializeField] private HQPanelUI hqPanel;

    [Header("Raycast")]
    [Tooltip("Only colliders on this layer are treated as clickable base buildings.")]
    [SerializeField] private LayerMask buildingLayer;
    [SerializeField] private float maxRayDistance = 500f;

    private BaseBuildingNode selectedNode;

    private void Update()
    {
        if (cam == null || viewManager == null || baseProgression == null)
            return;

        if (viewManager.CurrentState != ViewManager.ViewState.Base)
        {
            ClearSelection();
            return;
        }

        if (gameState != null && gameState.IsRunEnded)
        {
            ClearSelection();
            return;
        }

        if (Mouse.current == null)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        BaseBuildingNode clickedNode = RaycastNode(Mouse.current.position.ReadValue());

        if (clickedNode != null)
        {
            selectedNode = clickedNode;
            OpenFor(clickedNode);
        }
        else
        {
            ClearSelection();
        }
    }

    private BaseBuildingNode RaycastNode(Vector2 screenPosition)
    {
        Ray ray = cam.ScreenPointToRay(screenPosition);

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                maxRayDistance,
                buildingLayer,
                QueryTriggerInteraction.Ignore))
        {
            return null;
        }

        return hit.collider.GetComponentInParent<BaseBuildingNode>();
    }

    private void OpenFor(BaseBuildingNode node)
    {
        if (node == null || node.Data == null)
            return;

        RuntimeBuilding runtimeBuilding = node.GetRuntimeBuilding(baseProgression);
        if (runtimeBuilding == null)
            return;

        if (runtimeBuilding.State == BuildingState.Built &&
            node.OpenMode == BaseBuildingOpenMode.Headquarters &&
            hqPanel != null)
        {
            if (buildingDetailsPanel != null)
                buildingDetailsPanel.Close();

            hqPanel.Open();
            return;
        }

        if (hqPanel != null)
            hqPanel.Close();

        if (buildingDetailsPanel != null)
            buildingDetailsPanel.Open(node);
    }

    private void ClearSelection()
    {
        selectedNode = null;

        if (buildingDetailsPanel != null)
            buildingDetailsPanel.Close();

        if (hqPanel != null)
            hqPanel.Close();
    }
}