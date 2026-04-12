using UnityEngine;

public enum AgentStatus
{
    Available,
    Busy,
    Lost
}

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
        : this(
            template,
            template != null ? template.agentName : "Unnamed Agent",
            template != null ? template.baseINT : 1,
            template != null ? template.baseSTR : 1,
            template != null ? template.baseAGI : 1)
    {
    }

    public RuntimeAgent(
        AgentData template,
        string agentName,
        int intelligence,
        int strength,
        int agility)
    {
        Template = template;
        Name = string.IsNullOrWhiteSpace(agentName) ? "Unnamed Agent" : agentName;
        INT = Mathf.Max(1, intelligence);
        STR = Mathf.Max(1, strength);
        AGI = Mathf.Max(1, agility);
        Status = AgentStatus.Available;
    }
}