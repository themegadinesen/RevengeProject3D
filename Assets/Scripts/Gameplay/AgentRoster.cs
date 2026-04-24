using System;
using System.Collections.Generic;
using UnityEngine;

public class AgentRoster : MonoBehaviour
{
    [Header("Starting Roster")]
    [Tooltip("Authored agent definitions spawned at game start.")]
    [SerializeField] private RecruitmentCandidateDefinition[] startingAgents;

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
            foreach (var definition in startingAgents)
            {
                if (definition != null)
                    agents.Add(new RuntimeAgent(definition));
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

    public RuntimeAgent RecruitAgent(RecruitmentCandidateDefinition definition)
    {
        if (definition == null)
            return null;

        RuntimeAgent recruitedAgent = new RuntimeAgent(definition);
        agents.Add(recruitedAgent);
        OnRosterChanged?.Invoke();
        return recruitedAgent;
    }

    public RuntimeAgent RecruitCandidate(PendingRecruitCandidate candidate)
    {
        return RecruitCandidate(candidate, 1);
    }

    public RuntimeAgent RecruitCandidate(
        PendingRecruitCandidate candidate,
        int rookieMissionsRequired)
    {
        if (candidate == null)
            return null;

        RuntimeAgent recruitedAgent = new RuntimeAgent(
            candidate.Definition,
            candidate.CandidateName,
            candidate.INT,
            candidate.STR,
            candidate.AGI,
            true,
            rookieMissionsRequired);

        agents.Add(recruitedAgent);

        OnRosterChanged?.Invoke();
        return recruitedAgent;
    }
}