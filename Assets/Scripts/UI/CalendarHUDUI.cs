using TMPro;
using UnityEngine;
using UnityEngine.UI;
using EvoButton = Evo.UI.Button;

public class CalendarHUDUI : MonoBehaviour
{
    private enum CalendarDisplayMode
    {
        DayAndHour,
        MonthAndDay
    }

    [Header("References")]
    [SerializeField] private GameCalendar calendar;
    [SerializeField] private TextMeshProUGUI txtCalendar;
    [SerializeField] private EvoButton btnPause;
    [SerializeField] private TextMeshProUGUI txtPauseLabel;
    [SerializeField] private TextMeshProUGUI txtPauseText;

    [Header("Display")]
    [SerializeField] private CalendarDisplayMode displayMode = CalendarDisplayMode.DayAndHour;
    [Min(1)]
    [SerializeField] private int daysPerMonth = 30;
    [SerializeField] private string pauseText = "Pause";
    [SerializeField] private string resumeText = "Resume";

    private void Awake()
    {
        ResolveCalendar();
    }

    private void OnEnable()
    {
        ResolveCalendar();

        if (calendar != null)
        {
            calendar.OnCalendarChanged += OnCalendarChanged;
            calendar.OnPauseChanged += OnPauseChanged;
        }

        if (btnPause != null)
            btnPause.onClick.AddListener(OnPauseClicked);

        Refresh();
    }

    private void OnDisable()
    {
        if (calendar != null)
        {
            calendar.OnCalendarChanged -= OnCalendarChanged;
            calendar.OnPauseChanged -= OnPauseChanged;
        }

        if (btnPause != null)
            btnPause.onClick.RemoveListener(OnPauseClicked);
    }

    private void OnCalendarChanged(GameCalendar changedCalendar)
    {
        Refresh();
    }

    private void OnPauseChanged(bool isPaused)
    {
        RefreshPauseLabel();
        RefreshPauseText();
    }

    private void OnPauseClicked()
    {
        if (calendar == null)
            return;

        calendar.TogglePaused();
        RefreshPauseLabel();
        RefreshPauseText();
    }

    private void Refresh()
    {
        if (calendar == null)
        {
            if (txtCalendar != null)
                txtCalendar.text = string.Empty;

            RefreshPauseLabel();
            return;
        }

        if (txtCalendar != null)
            txtCalendar.text = BuildCalendarText();

        RefreshPauseLabel();
        RefreshPauseText();
    }

    private string BuildCalendarText()
    {
        if (displayMode == CalendarDisplayMode.MonthAndDay)
        {
            int month = ((calendar.CurrentDay - 1) / daysPerMonth) + 1;
            int dayOfMonth = ((calendar.CurrentDay - 1) % daysPerMonth) + 1;
            return $"Month {month}, Day {dayOfMonth}";
        }

        return $"Day {calendar.CurrentDay}, {calendar.CurrentHour:00}:{calendar.CurrentMinute:00}";
    }

    private void RefreshPauseLabel()
    {
        if (txtPauseLabel != null)
            txtPauseLabel.text = calendar != null && calendar.IsPaused ? resumeText : pauseText;
    }

    private void ResolveCalendar()
    {
        if (calendar == null)
            calendar = FindFirstObjectByType<GameCalendar>();
    }

    private void RefreshPauseText()
    {
        if (txtPauseText == null)
            return;

        bool isPaused = calendar != null && calendar.IsPaused;
        txtPauseText.text = isPaused ? "Paused" : string.Empty;
    }

    private void OnValidate()
    {
        daysPerMonth = Mathf.Max(1, daysPerMonth);
    }
}
