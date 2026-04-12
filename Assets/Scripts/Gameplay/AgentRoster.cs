using System;
using System.Collections.Generic;
using UnityEngine;

public class AgentRoster : MonoBehaviour
{
    [Header("Starting Roster")]
    [Tooltip("Agent templates spawned at game start.")]
    [SerializeField] private AgentData[] startingAgents;

    private readonly List<RuntimeAgent> agents = new();

    public IReadOnlyList<RuntimeAgent> AllAgents => agents;

    public event Action OnRosterChanged;

    public int TotalCount
    {
        get
        {
            int count = 0;
            foreach (var agent in agents)
            {
                if (agent.Status != AgentStatus.Lost)
                    count++;
            }

            return count;
        }
    }

    public int AvailableCount
    {
        get
        {
            int count = 0;
            foreach (var agent in agents)
            {
                if (agent.Status == AgentStatus.Available)
                    count++;
            }

            return count;
        }
    }

    public int BusyCount
    {
        get
        {
            int count = 0;
            foreach (var agent in agents)
            {
                if (agent.Status == AgentStatus.Busy)
                    count++;
            }

            return count;
        }
    }

    private void Start()
    {
        if (startingAgents != null)
        {
            foreach (var template in startingAgents)
            {
                if (template != null)
                    agents.Add(new RuntimeAgent(template));
            }
        }

        OnRosterChanged?.Invoke();
    }

    public List<RuntimeAgent> GetAvailableAgents()
    {
        var result = new List<RuntimeAgent>();

        foreach (var agent in agents)
        {
            if (agent.Status == AgentStatus.Available)
                result.Add(agent);
        }

        return result;
    }

    public void SetBusy(RuntimeAgent agent)
    {
        if (agent == null) return;

        agent.Status = AgentStatus.Busy;
        OnRosterChanged?.Invoke();
    }

    public void SetAvailable(RuntimeAgent agent)
    {
        if (agent == null) return;

        agent.Status = AgentStatus.Available;
        OnRosterChanged?.Invoke();
    }

    public void LoseAgent(RuntimeAgent agent)
    {
        if (agent == null) return;

        agent.Status = AgentStatus.Lost;
        OnRosterChanged?.Invoke();
    }

    public void RecruitAgent(AgentData template)
    {
        if (template == null) return;

        agents.Add(new RuntimeAgent(template));
        OnRosterChanged?.Invoke();
    }

    public void RecruitCandidate(PendingRecruitCandidate candidate)
    {
        if (candidate == null) return;

        agents.Add(new RuntimeAgent(
            candidate.Template,
            candidate.CandidateName,
            candidate.INT,
            candidate.STR,
            candidate.AGI));

        OnRosterChanged?.Invoke();
    }
}