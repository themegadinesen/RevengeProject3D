using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NewsTickerUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorldEventDirector worldEventDirector;

    [Header("Feed")]
    [Tooltip("Optional. When assigned with an Entry Prefab, each item is spawned into this content root.")]
    [SerializeField] private Transform feedContent;
    [SerializeField] private TextMeshProUGUI entryPrefab;
    [Tooltip("Optional fallback text. Shows the latest entries when no prefab/content is assigned.")]
    [SerializeField] private TextMeshProUGUI txtFeed;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Display")]
    [Min(1)]
    [SerializeField] private int maxEntries = 8;
    [SerializeField] private string entryPrefix = "NEWS";

    private readonly List<TextMeshProUGUI> spawnedEntries = new();
    private readonly List<string> textEntries = new();

    private void Awake()
    {
        ResolveDirector();
    }

    private void OnEnable()
    {
        ResolveDirector();

        if (worldEventDirector != null)
            worldEventDirector.OnFlavorEvent += OnFlavorEvent;
    }

    private void OnDisable()
    {
        if (worldEventDirector != null)
            worldEventDirector.OnFlavorEvent -= OnFlavorEvent;
    }

    private void OnFlavorEvent(WorldEventContext context)
    {
        if (context == null || string.IsNullOrWhiteSpace(context.Message))
            return;

        AddEntry(context.Message);
    }

    private void AddEntry(string message)
    {
        string entry = string.IsNullOrWhiteSpace(entryPrefix)
            ? message
            : $"{entryPrefix}: {message}";

        textEntries.Add(entry);
        while (textEntries.Count > maxEntries)
            textEntries.RemoveAt(0);

        if (feedContent != null && entryPrefab != null)
            AddSpawnedEntry(entry);

        RefreshTextFeed();
        ScrollToLatest();
    }

    private void AddSpawnedEntry(string entry)
    {
        TextMeshProUGUI spawned = Instantiate(entryPrefab, feedContent);
        spawned.text = entry;
        spawned.gameObject.SetActive(true);
        spawnedEntries.Add(spawned);

        while (spawnedEntries.Count > maxEntries)
        {
            TextMeshProUGUI oldest = spawnedEntries[0];
            spawnedEntries.RemoveAt(0);

            if (oldest != null)
                Destroy(oldest.gameObject);
        }
    }

    private void RefreshTextFeed()
    {
        if (txtFeed == null)
            return;

        txtFeed.text = string.Join("\n", textEntries);
    }

    private void ScrollToLatest()
    {
        if (scrollRect == null)
            return;

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }

    private void ResolveDirector()
    {
        if (worldEventDirector == null)
            worldEventDirector = FindFirstObjectByType<WorldEventDirector>();
    }

    private void OnValidate()
    {
        maxEntries = Mathf.Max(1, maxEntries);
    }
}
