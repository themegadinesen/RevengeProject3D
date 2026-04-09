// Assets/Scripts/Gameplay/RuntimeDistrict.cs
using UnityEngine;

/// <summary>
/// Mutable per-district runtime state. Plain C# — like RuntimeAgent.
/// </summary>
[System.Serializable]
public class RuntimeDistrict
{
    public DistrictData Data;
    public float LocalChaos;
    public float LocalCure;
    public float MaxChaos;
    public float MaxCure;
    public int   LocalPeopleAffected;
    public bool  IsUnlocked;

    public RuntimeDistrict(DistrictData data, float maxChaos, float maxCure)
    {
        Data                = data;
        MaxChaos            = maxChaos;
        MaxCure             = maxCure;
        LocalChaos          = data.startingChaos;
        LocalCure           = data.startingCure;
        LocalPeopleAffected = 0;
        IsUnlocked          = false;
    }

    public void AddChaos(float amount)
    {
        LocalChaos = Mathf.Clamp(LocalChaos + amount, 0f, MaxChaos);
    }

    public void AddCure(float amount)
    {
        LocalCure = Mathf.Clamp(LocalCure + amount, 0f, MaxCure);
    }

    public float ChaosNormalized => MaxChaos > 0f ? LocalChaos / MaxChaos : 0f;
    public float CureNormalized  => MaxCure  > 0f ? LocalCure  / MaxCure  : 0f;
}