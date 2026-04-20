// Assets/Scripts/UI/CameraZoomControlUI.cs
using UnityEngine;
using UnityEngine.UI;

public class CameraZoomControlUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ViewManager viewManager;
    [SerializeField] private GameState gameState;

    [Header("Controls")]
    [SerializeField] private Slider zoomSlider;
    [SerializeField] private Button btnBase;
    [SerializeField] private Button btnTopView;
    [SerializeField] private Button btnMapView;

    private void OnEnable()
    {
        if (zoomSlider != null)
            zoomSlider.onValueChanged.AddListener(OnSliderValueChanged);

        if (btnBase != null)
            btnBase.onClick.AddListener(OnBaseClicked);

        if (btnTopView != null)
            btnTopView.onClick.AddListener(OnTopViewClicked);

        if (btnMapView != null)
            btnMapView.onClick.AddListener(OnMapViewClicked);

        SyncFromView();
        RefreshInteractableState();
    }

    private void OnDisable()
    {
        if (zoomSlider != null)
            zoomSlider.onValueChanged.RemoveListener(OnSliderValueChanged);

        if (btnBase != null)
            btnBase.onClick.RemoveListener(OnBaseClicked);

        if (btnTopView != null)
            btnTopView.onClick.RemoveListener(OnTopViewClicked);

        if (btnMapView != null)
            btnMapView.onClick.RemoveListener(OnMapViewClicked);
    }

    private void LateUpdate()
    {
        SyncFromView();
        RefreshInteractableState();
    }

    private void OnSliderValueChanged(float value)
    {
        if (viewManager == null)
            return;

        viewManager.SetZoomNormalized(value);
    }

    private void OnBaseClicked()
    {
        if (viewManager == null || !viewManager.HasBaseZoomTargets)
            return;

        viewManager.SetZoomNormalized(viewManager.GetBaseWaypointNormalized());
    }

    private void OnTopViewClicked()
    {
        if (viewManager == null || !viewManager.HasBaseZoomTargets)
            return;

        viewManager.SetZoomNormalized(viewManager.GetTopViewWaypointNormalized());
    }

    private void OnMapViewClicked()
    {
        if (viewManager == null)
            return;

        viewManager.SetZoomNormalized(viewManager.GetMapWaypointNormalized());
    }

    private void SyncFromView()
    {
        if (viewManager == null || zoomSlider == null)
            return;

        zoomSlider.SetValueWithoutNotify(viewManager.GetZoomNormalized());
    }

    private void RefreshInteractableState()
    {
        bool runActive = gameState == null || !gameState.IsRunEnded;
        bool hasViewManager = viewManager != null;
        bool canUseMapZoom = runActive && hasViewManager;
        bool canUseBaseTargets = canUseMapZoom && viewManager.HasBaseZoomTargets;

        if (zoomSlider != null)
            zoomSlider.interactable = canUseMapZoom;

        if (btnBase != null)
            btnBase.interactable = canUseBaseTargets;

        if (btnTopView != null)
            btnTopView.interactable = canUseBaseTargets;

        if (btnMapView != null)
            btnMapView.interactable = canUseMapZoom;
    }
}