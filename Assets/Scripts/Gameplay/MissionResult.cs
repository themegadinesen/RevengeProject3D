// Assets/Scripts/Gameplay/MissionResult.cs
using System.Collections.Generic;

/// <summary>
/// Carries mission resolution details to UI via the OnMissionResolved event.
/// </summary>
public class MissionResult
{
    public MissionData Data;
    public bool Success;
    public float Score;
    public int ActualMoneyReward;
    public List<RuntimeAgent> LostAgents;   // null or empty on success
}