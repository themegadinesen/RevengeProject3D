using System;
using UnityEngine;

public class GameCalendar : MonoBehaviour
{
    private const int HoursPerDay = 24;
    private const int MinutesPerHour = 60;
    private const int MinutesPerDay = HoursPerDay * MinutesPerHour;

    [Header("References")]
    [SerializeField] private GameState gameState;

    [Header("Campaign Time")]
    [Min(1)]
    [SerializeField] private int campaignLengthDays = 90;
    [Min(0.01f)]
    [SerializeField] private float realSecondsPerInGameHour = 5f;

    public event Action<GameCalendar> OnCalendarChanged;
    public event Action<bool> OnPauseChanged;

    private float elapsedHours;
    private bool playerPaused;
    private int systemPauseRequests;
    private int lastNotifiedDay = -1;
    private int lastNotifiedHour = -1;
    private int lastNotifiedMinute = -1;

    public int CurrentDay
    {
        get
        {
            int displayMinute = GetDisplayElapsedMinutes();
            return Mathf.Clamp((displayMinute / MinutesPerDay) + 1, 1, CampaignLengthDays);
        }
    }

    public int CurrentHour => (GetDisplayElapsedMinutes() / MinutesPerHour) % HoursPerDay;
    public int CurrentMinute => GetDisplayElapsedMinutes() % MinutesPerHour;
    public float ElapsedDays => Mathf.Clamp(elapsedHours / HoursPerDay, 0f, CampaignLengthDays);
    public float ElapsedHours => Mathf.Clamp(elapsedHours, 0f, MaxElapsedHours);
    public float CampaignProgressNormalized => CampaignLengthDays > 0
        ? Mathf.Clamp01(ElapsedDays / CampaignLengthDays)
        : 0f;

    public int CampaignLengthDays => Mathf.Max(1, campaignLengthDays);
    public float RealSecondsPerInGameHour => Mathf.Max(0.01f, realSecondsPerInGameHour);
    public bool IsPaused => playerPaused || systemPauseRequests > 0;
    public bool IsPlayerPaused => playerPaused;

    private float MaxElapsedHours => CampaignLengthDays * HoursPerDay;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        NotifyCalendarChanged(force: true);
    }

    private void Update()
    {
        if (gameState != null && gameState.IsRunEnded)
            return;

        if (IsPaused || elapsedHours >= MaxElapsedHours)
            return;

        elapsedHours = Mathf.Min(
            elapsedHours + Time.deltaTime / RealSecondsPerInGameHour,
            MaxElapsedHours);

        NotifyCalendarChanged(force: false);
    }

    public void TogglePaused()
    {
        SetPaused(!playerPaused);
    }

    public void SetPaused(bool paused)
    {
        if (playerPaused == paused)
            return;

        bool wasPaused = IsPaused;
        playerPaused = paused;
        NotifyPauseChangedIfNeeded(wasPaused);
    }

    public void PushSystemPause()
    {
        bool wasPaused = IsPaused;
        systemPauseRequests++;
        NotifyPauseChangedIfNeeded(wasPaused);
    }

    public void PopSystemPause()
    {
        if (systemPauseRequests <= 0)
            return;

        bool wasPaused = IsPaused;
        systemPauseRequests--;
        NotifyPauseChangedIfNeeded(wasPaused);
    }

    private void ResolveReferences()
    {
        if (gameState == null)
            gameState = FindFirstObjectByType<GameState>();
    }

    private void NotifyCalendarChanged(bool force)
    {
        if (!force &&
            CurrentDay == lastNotifiedDay &&
            CurrentHour == lastNotifiedHour &&
            CurrentMinute == lastNotifiedMinute)
        {
            return;
        }

        lastNotifiedDay = CurrentDay;
        lastNotifiedHour = CurrentHour;
        lastNotifiedMinute = CurrentMinute;
        OnCalendarChanged?.Invoke(this);
    }

    private void NotifyPauseChangedIfNeeded(bool wasPaused)
    {
        if (wasPaused != IsPaused)
            OnPauseChanged?.Invoke(IsPaused);
    }

    private int GetDisplayElapsedMinutes()
    {
        int maxDisplayMinute = Mathf.Max(0, Mathf.RoundToInt(MaxElapsedHours * MinutesPerHour) - 1);
        int wholeMinutes = Mathf.FloorToInt(ElapsedHours * MinutesPerHour);
        return Mathf.Clamp(wholeMinutes, 0, maxDisplayMinute);
    }

    private void OnValidate()
    {
        campaignLengthDays = Mathf.Max(1, campaignLengthDays);
        realSecondsPerInGameHour = Mathf.Max(0.01f, realSecondsPerInGameHour);
    }
}
