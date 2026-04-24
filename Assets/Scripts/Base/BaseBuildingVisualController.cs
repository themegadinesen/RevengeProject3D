using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class BaseBuildingVisualController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BaseBuildingNode buildingNode;
    [SerializeField] private BaseProgressionManager baseProgression;

    [Header("State Visual Roots")]
    [Tooltip("Abandoned / locked look.")]
    [SerializeField] private GameObject lockedVisualRoot;

    [Tooltip("Optional. If empty, Available falls back to Locked visuals.")]
    [SerializeField] private GameObject availableVisualRoot;

    [Tooltip("Optional. If empty, Constructing falls back to Available/Locked visuals.")]
    [SerializeField] private GameObject constructionVisualRoot;

    [Tooltip("Renovated / active look.")]
    [SerializeField] private GameObject builtVisualRoot;

    [Header("Optional Built Accents")]
    [Tooltip("Optional emissive mesh variants, lit signs, smoke stacks, etc.")]
    [SerializeField] private GameObject[] renovatedOnlyObjects;

    [Tooltip("Optional lights enabled only when renovated.")]
    [SerializeField] private Light[] renovatedLights;

    private void Awake()
    {
        if (buildingNode == null)
            buildingNode = GetComponent<BaseBuildingNode>();

        SetObjectsActive(renovatedOnlyObjects, false);
        SetLightsEnabled(renovatedLights, false);
    }

    private void OnEnable()
    {
        if (baseProgression != null)
            baseProgression.OnBuildingStateChanged += OnBuildingStateChanged;

        RefreshImmediate();
    }

    private IEnumerator Start()
    {
        // Let BaseProgressionManager finish its Start initialization first.
        yield return null;
        RefreshImmediate();
    }

    private void OnDisable()
    {
        if (baseProgression != null)
            baseProgression.OnBuildingStateChanged -= OnBuildingStateChanged;
    }

    public void RefreshImmediate()
    {
        if (buildingNode == null || buildingNode.Data == null)
        {
            ApplyVisualState(BuildingState.Locked);
            return;
        }

        RuntimeBuilding runtimeBuilding = buildingNode.GetRuntimeBuilding(baseProgression);
        if (runtimeBuilding == null)
            return;

        ApplyVisualState(runtimeBuilding.State);
    }

    private void OnBuildingStateChanged(RuntimeBuilding runtimeBuilding)
    {
        if (buildingNode == null || runtimeBuilding == null)
            return;

        if (runtimeBuilding.Data != buildingNode.Data)
            return;

        ApplyVisualState(runtimeBuilding.State);
    }

    private void ApplyVisualState(BuildingState state)
    {
        GameObject activeRoot = ResolveVisualRoot(state);

        SetRootState(lockedVisualRoot, activeRoot == lockedVisualRoot);
        SetRootState(availableVisualRoot, activeRoot == availableVisualRoot);
        SetRootState(constructionVisualRoot, activeRoot == constructionVisualRoot);
        SetRootState(builtVisualRoot, activeRoot == builtVisualRoot);

        bool isBuilt = state == BuildingState.Built;
        SetObjectsActive(renovatedOnlyObjects, isBuilt);
        SetLightsEnabled(renovatedLights, isBuilt);
    }

    private GameObject ResolveVisualRoot(BuildingState state)
    {
        switch (state)
        {
            case BuildingState.Built:
                if (builtVisualRoot != null) return builtVisualRoot;
                if (availableVisualRoot != null) return availableVisualRoot;
                return lockedVisualRoot;

            case BuildingState.Constructing:
                if (constructionVisualRoot != null) return constructionVisualRoot;
                if (availableVisualRoot != null) return availableVisualRoot;
                return lockedVisualRoot;

            case BuildingState.Available:
                if (availableVisualRoot != null) return availableVisualRoot;
                return lockedVisualRoot;

            default:
                if (lockedVisualRoot != null) return lockedVisualRoot;
                if (availableVisualRoot != null) return availableVisualRoot;
                return builtVisualRoot;
        }
    }

    private static void SetRootState(GameObject target, bool isActive)
    {
        if (target != null)
            target.SetActive(isActive);
    }

    private static void SetObjectsActive(GameObject[] targets, bool isActive)
    {
        if (targets == null)
            return;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
                targets[i].SetActive(isActive);
        }
    }

    private static void SetLightsEnabled(Light[] lights, bool isEnabled)
    {
        if (lights == null)
            return;

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null)
                lights[i].enabled = isEnabled;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (buildingNode == null)
            buildingNode = GetComponent<BaseBuildingNode>();
    }
#endif
}