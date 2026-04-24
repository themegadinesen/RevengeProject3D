using UnityEngine;

public enum BaseBuildingOpenMode
{
    GenericDetails,
    Headquarters
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class BaseBuildingNode : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private BuildingData buildingData;
    [SerializeField] private BaseBuildingOpenMode openMode = BaseBuildingOpenMode.GenericDetails;

    public BuildingData Data => buildingData;
    public BaseBuildingOpenMode OpenMode => openMode;

    public RuntimeBuilding GetRuntimeBuilding(BaseProgressionManager baseProgression)
    {
        return baseProgression != null ? baseProgression.GetRuntimeBuilding(buildingData) : null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = false;
    }
#endif
}