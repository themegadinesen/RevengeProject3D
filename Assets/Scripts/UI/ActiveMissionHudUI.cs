using System;
using System.Collections.Generic;
using UnityEngine;

public class ActiveMissionHudUI : MonoBehaviour
{
    [Header("Required")]
    [SerializeField] private MissionManager missionManager;
    [SerializeField] private GameObject tooltipPreset;
    [SerializeField] private int visibleSlotCount = 3;
    [SerializeField] private Transform visibleItemsRoot;
    [SerializeField] private ActiveMissionHudItemUI visibleItemPrefab;
    [SerializeField] private ActiveMissionOverflowUI overflowController;

    private readonly List<ActiveMission> sortedMissions = new();
    private readonly Dictionary<ActiveMission, ActiveMissionHudItemUI> visibleItems = new();
    private readonly List<ActiveMission> missionsToRemove = new();

    private void Reset()
    {
        if (missionManager == null)
            missionManager = FindFirstObjectByType<MissionManager>();
    }

    private void Awake()
    {
        if (missionManager == null)
            missionManager = FindFirstObjectByType<MissionManager>();
    }

    private void OnEnable()
    {
        SubscribeToMissionEvents();
        RebuildHud();
    }

    private void OnDisable()
    {
        UnsubscribeFromMissionEvents();
    }

    private void Update()
    {
        foreach (KeyValuePair<ActiveMission, ActiveMissionHudItemUI> pair in visibleItems)
        {
            if (pair.Value != null)
                pair.Value.RefreshView();
        }

        if (overflowController != null)
            overflowController.RefreshView();
    }

    private void SubscribeToMissionEvents()
    {
        if (missionManager == null)
            return;

        missionManager.OnMissionStarted += HandleMissionStarted;
        missionManager.OnMissionResolved += HandleMissionResolved;
    }

    private void UnsubscribeFromMissionEvents()
    {
        if (missionManager == null)
            return;

        missionManager.OnMissionStarted -= HandleMissionStarted;
        missionManager.OnMissionResolved -= HandleMissionResolved;
    }

    private void HandleMissionStarted(ActiveMission _)
    {
        RebuildHud();
    }

    private void HandleMissionResolved(MissionResult _)
    {
        RebuildHud();
    }

    private void RebuildHud()
    {
        RebuildMissionCache();

        int maxVisible = Mathf.Max(1, visibleSlotCount);
        bool hasAnyMissions = sortedMissions.Count > 0;
        bool useOverflow = sortedMissions.Count > maxVisible;

        if (!hasAnyMissions)
        {
            if (visibleItemsRoot != null)
                visibleItemsRoot.gameObject.SetActive(false);

            ClearVisibleItems();

            if (overflowController != null)
                overflowController.Hide();

            return;
        }

        if (useOverflow)
        {
            if (visibleItemsRoot != null)
                visibleItemsRoot.gameObject.SetActive(IsOverflowHostedByVisibleItemsRoot());

            ClearVisibleItems();

            if (overflowController != null)
                overflowController.Rebuild(sortedMissions, tooltipPreset);

            return;
        }

        if (visibleItemsRoot != null)
            visibleItemsRoot.gameObject.SetActive(true);

        if (overflowController != null)
            overflowController.Hide();

        SyncVisibleItems(sortedMissions);
    }

    private void RebuildMissionCache()
    {
        sortedMissions.Clear();

        if (missionManager == null)
            return;

        IReadOnlyList<ActiveMission> activeMissions = missionManager.ActiveMissions;
        if (activeMissions == null)
            return;

        for (int i = 0; i < activeMissions.Count; i++)
        {
            ActiveMission mission = activeMissions[i];
            if (mission == null || mission.Data == null)
                continue;

            if (mission.Duration <= 0f)
                continue;

            if (mission.IsComplete)
                continue;

            sortedMissions.Add(mission);
        }

        sortedMissions.Sort(CompareMissionsByTimeRemaining);
    }

    private void SyncVisibleItems(IReadOnlyList<ActiveMission> missions)
    {
        if (visibleItemsRoot == null || visibleItemPrefab == null)
            return;

        missionsToRemove.Clear();

        foreach (KeyValuePair<ActiveMission, ActiveMissionHudItemUI> pair in visibleItems)
        {
            if (!ContainsMission(missions, pair.Key))
                missionsToRemove.Add(pair.Key);
        }

        for (int i = 0; i < missionsToRemove.Count; i++)
        {
            ActiveMission mission = missionsToRemove[i];

            if (visibleItems.TryGetValue(mission, out ActiveMissionHudItemUI item))
            {
                if (item != null)
                    Destroy(item.gameObject);

                visibleItems.Remove(mission);
            }
        }

        for (int i = 0; i < missions.Count; i++)
        {
            ActiveMission mission = missions[i];
            if (mission == null)
                continue;

            if (!visibleItems.TryGetValue(mission, out ActiveMissionHudItemUI item) || item == null)
            {
                item = Instantiate(visibleItemPrefab, visibleItemsRoot);
                item.Bind(mission, tooltipPreset);
                visibleItems[mission] = item;
            }

            item.transform.SetSiblingIndex(i);
            item.RefreshView();
        }
    }

    private void ClearVisibleItems()
    {
        foreach (KeyValuePair<ActiveMission, ActiveMissionHudItemUI> pair in visibleItems)
        {
            if (pair.Value != null)
                Destroy(pair.Value.gameObject);
        }

        visibleItems.Clear();
        missionsToRemove.Clear();
    }

    private static int CompareMissionsByTimeRemaining(ActiveMission a, ActiveMission b)
    {
        if (ReferenceEquals(a, b))
            return 0;

        if (a == null)
            return 1;

        if (b == null)
            return -1;

        int timeComparison = a.TimeRemaining.CompareTo(b.TimeRemaining);
        if (timeComparison != 0)
            return timeComparison;

        string aName = a.Data != null ? a.Data.missionName : string.Empty;
        string bName = b.Data != null ? b.Data.missionName : string.Empty;
        return string.Compare(aName, bName, StringComparison.Ordinal);
    }

    private bool IsOverflowHostedByVisibleItemsRoot()
    {
        if (visibleItemsRoot == null || overflowController == null)
            return false;

        Transform overflowTransform = overflowController.transform;
        return overflowTransform == visibleItemsRoot || overflowTransform.IsChildOf(visibleItemsRoot);
    }

    private static bool ContainsMission(IReadOnlyList<ActiveMission> missions, ActiveMission mission)
    {
        if (missions == null || mission == null)
            return false;

        for (int i = 0; i < missions.Count; i++)
        {
            if (ReferenceEquals(missions[i], mission))
                return true;
        }

        return false;
    }
}
