// Assets/Scripts/Gameplay/GameState.cs
using System;
using UnityEngine;

public class GameState : MonoBehaviour
{
    // ── Inspector Tuning ──────────────────────────────────────────────
    [Header("Starting Values")]
    [SerializeField] private float startingChaos = 5f;
    [SerializeField] private float startingCure  = 0f;
    [SerializeField] private int   startingMoney = 200;

    [Header("Chaos")]
    [SerializeField] private float chaosDecayRate = 0.5f;
    [SerializeField] private float maxChaos       = 100f;

    [Header("Cure")]
    [SerializeField] private float cureFillRate = 0.3f;
    [SerializeField] private float maxCure      = 100f;

    [Header("People Affected")]
    [SerializeField] private float peopleGrowthMultiplier = 0.1f;

    // ── Runtime State ─────────────────────────────────────────────────
    public float Chaos          { get; private set; }
    public float Cure           { get; private set; }
    public float MaxCure        => maxCure;
    public float MaxChaos       => maxChaos;
    public int   PeopleAffected { get; private set; }
    public int   Money          { get; private set; }

    public float CureNormalized  => maxCure  > 0f ? Cure  / maxCure  : 0f;
    public float ChaosNormalized => maxChaos > 0f ? Chaos / maxChaos : 0f;

    public bool IsRunEnded { get; private set; }

    // ── Events ────────────────────────────────────────────────────────
    public event Action OnStateChanged;

    // ── Lifecycle ─────────────────────────────────────────────────────
    private void Start()
    {
        Chaos          = startingChaos;
        Cure           = startingCure;
        Money          = startingMoney;
        PeopleAffected = 0;
        IsRunEnded     = false;
        OnStateChanged?.Invoke();
    }

    private void Update()
    {
        if (IsRunEnded) return;

        TickChaosDecay();
        TickCureFill();
        TickPeopleAffected();
        OnStateChanged?.Invoke();
    }

    // ── Public Mutators ───────────────────────────────────────────────
    public void AddChaos(float amount)
    {
        Chaos = Mathf.Clamp(Chaos + amount, 0f, maxChaos);
    }

    public void AddCure(float amount)
    {
        Cure = Mathf.Clamp(Cure + amount, 0f, maxCure);
    }

    public void AddMoney(int amount)
    {
        Money += amount;
    }

    public void EndRun()
    {
        IsRunEnded = true;
        OnStateChanged?.Invoke();
    }

    // ── Tick Logic ────────────────────────────────────────────────────
    private void TickChaosDecay()
    {
        if (Chaos > 0f)
            Chaos = Mathf.Max(0f, Chaos - chaosDecayRate * Time.deltaTime);
    }

    private void TickCureFill()
    {
        if (Cure < maxCure)
            Cure = Mathf.Min(maxCure, Cure + cureFillRate * Time.deltaTime);
    }

    private void TickPeopleAffected()
    {
        float growth = Chaos * peopleGrowthMultiplier * Time.deltaTime;
        if (growth > 0f)
            PeopleAffected += Mathf.CeilToInt(growth);
    }
}