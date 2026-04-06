// Assets/Scripts/Data/AgentData.cs
using UnityEngine;

[CreateAssetMenu(fileName = "NewAgent", menuName = "Gameplay/Agent")]
public class AgentData : ScriptableObject
{
    public string agentName = "New Agent";

    [Tooltip("Optional portrait for UI.")]
    public Sprite portrait;

    [Header("Base Stats")]
    [Min(1)] public int baseINT = 1;
    [Min(1)] public int baseSTR = 1;
    [Min(1)] public int baseAGI = 1;
}