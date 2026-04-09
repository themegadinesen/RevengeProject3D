// Assets/Scripts/Gameplay/RuntimeBuilding.cs

public enum BuildingState
{
    Locked,
    Available,
    Constructing,
    Built
}

/// <summary>
/// Mutable per-building runtime state. Plain C# — like RuntimeAgent / RuntimeDistrict.
/// </summary>
[System.Serializable]
public class RuntimeBuilding
{
    public BuildingData Data;
    public BuildingState State;
    public float BuildTimeRemaining;

    public RuntimeBuilding(BuildingData data)
    {
        Data = data;
        State = BuildingState.Locked;
        BuildTimeRemaining = data.buildTime;
    }

    /// <summary>0 → 1 construction progress. 1 when Built or buildTime is 0.</summary>
    public float BuildProgress
    {
        get
        {
            if (State == BuildingState.Built) return 1f;
            if (State != BuildingState.Constructing) return 0f;
            if (Data.buildTime <= 0f) return 1f;
            return 1f - (BuildTimeRemaining / Data.buildTime);
        }
    }
}