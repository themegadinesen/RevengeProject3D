// Assets/Scripts/Gameplay/AgentRoster.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public class AgentRoster : MonoBehaviour
{
    [Header("Starting Roster")]
    [Tooltip("Agent templates spawned at game start.")]
    [SerializeField] private AgentData[] startingAgents;

    private readonly List<RuntimeAgent> agents = new();

    /// <summary>Full roster (including Lost agents) — read-only for UI/debug.</summary>
    public IReadOnlyList<RuntimeAgent> AllAgents => agents;

    /// <summary>Fired whenever any agent's status changes.</summary>
    public event Action OnRosterChanged;

    // ── Counts ────────────────────────────────────────────────────────
    public int TotalCount
    {
        get
        {
            int c = 0;
            foreach (var a in agents)
                if (a.Status != AgentStatus.Lost) c++;
            return c;
        }
    }

    public int AvailableCount
    {
        get
        {
            int c = 0;
            foreach (var a in agents)
                if (a.Status == AgentStatus.Available) c++;
            return c;
        }
    }

    public int BusyCount
    {
        get
        {
            int c = 0;
            foreach (var a in agents)
                if (a.Status == AgentStatus.Busy) c++;
            return c;
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────
    private void Start()
    {
        foreach (var template in startingAgents)
            agents.Add(new RuntimeAgent(template));

        OnRosterChanged?.Invoke();
    }

    // ── Queries ───────────────────────────────────────────────────────
    public List<RuntimeAgent> GetAvailableAgents()
    {
        var result = new List<RuntimeAgent>();
        foreach (var a in agents)
            if (a.Status == AgentStatus.Available)
                result.Add(a);
        return result;
    }

    // ── Mutators ──────────────────────────────────────────────────────
    public void SetBusy(RuntimeAgent agent)
    {
        agent.Status = AgentStatus.Busy;
        OnRosterChanged?.Invoke();
    }

    public void SetAvailable(RuntimeAgent agent)
    {
        agent.Status = AgentStatus.Available;
        OnRosterChanged?.Invoke();
    }

    public void LoseAgent(RuntimeAgent agent)
    {
        agent.Status = AgentStatus.Lost;
        OnRosterChanged?.Invoke();
    }

    /// <summary>Recruit a new agent from a template (e.g., mission reward).</summary>
    public void RecruitAgent(AgentData template)
    {
        agents.Add(new RuntimeAgent(template));
        OnRosterChanged?.Invoke();
    }
}