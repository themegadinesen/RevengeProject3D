// Assets/Scripts/Gameplay/DistrictManager.cs
using System;
using UnityEngine;

public class DistrictManager : MonoBehaviour
{
    [SerializeField] private GameState gameState;

    [Header("Districts")]
    [Tooltip("Add all districts here. First unlocked district is the default.")]
    [SerializeField] private DistrictData[] allDistricts;

    /// <summary>Fired when a district becomes unlocked.</summary>
    public event Action<DistrictData> OnDistrictUnlocked;

    // Track which districts have been unlocked (by index).
    private bool[] unlocked;

    /// <summary>Currently selected district (for the mission panel).</summary>
    public DistrictData ActiveDistrict { get; private set; }

    private void Start()
    {
        unlocked = new bool[allDistricts.Length];

        // First district starts unlocked.
        if (allDistricts.Length > 0)
        {
            unlocked[0] = true;
            ActiveDistrict = allDistricts[0];
        }
    }

    private void Update()
    {
        CheckUnlocks();
    }

    public bool IsUnlocked(int index)
    {
        return index >= 0 && index < unlocked.Length && unlocked[index];
    }

    public DistrictData[] GetAllDistricts() => allDistricts;

    public void SetActiveDistrict(int index)
    {
        if (IsUnlocked(index))
            ActiveDistrict = allDistricts[index];
    }

    private void CheckUnlocks()
    {
        for (int i = 0; i < allDistricts.Length; i++)
        {
            if (!unlocked[i] && gameState.Chaos >= allDistricts[i].chaosUnlockThreshold)
            {
                unlocked[i] = true;
                OnDistrictUnlocked?.Invoke(allDistricts[i]);
            }
        }
    }
}