// Assets/Scripts/Gameplay/RuntimeAgent.cs

public enum AgentStatus { Available, Busy, Lost }

/// <summary>
/// Mutable runtime instance of an agent. Plain C# — not a MonoBehaviour.
/// </summary>
[System.Serializable]
public class RuntimeAgent
{
    public AgentData Template;
    public string Name;
    public int INT;
    public int STR;
    public int AGI;
    public AgentStatus Status;

    public RuntimeAgent(AgentData template)
    {
        Template = template;
        Name     = template.agentName;
        INT      = template.baseINT;
        STR      = template.baseSTR;
        AGI      = template.baseAGI;
        Status   = AgentStatus.Available;
    }
}