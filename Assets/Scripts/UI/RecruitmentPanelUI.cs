// Assets/Scripts/UI/RecruitmentPanelUI.cs
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RecruitmentPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RecruitmentManager recruitmentManager;
    [SerializeField] private GameState gameState;

    [Header("List")]
    [SerializeField] private TextMeshProUGUI txtTitle;
    [SerializeField] private Transform candidateListContent;
    [SerializeField] private RecruitCandidateSlotUI candidateSlotPrefab;
    [SerializeField] private Button btnClose;

    [Header("Details")]
    [SerializeField] private TextMeshProUGUI txtCandidateName;
    [SerializeField] private Image imgPortrait;
    [SerializeField] private TextMeshProUGUI txtStats;
    [SerializeField] private TextMeshProUGUI txtSource;
    [SerializeField] private TextMeshProUGUI txtStatus;
    [SerializeField] private Button btnEvaluate;

    private readonly List<RecruitCandidateSlotUI> spawnedSlots = new();
    private PendingRecruitCandidate selectedCandidate;
    private string lastResultMessage = "";

    private void OnEnable()
    {
        if (btnClose != null)
            btnClose.onClick.AddListener(Close);

        if (btnEvaluate != null)
            btnEvaluate.onClick.AddListener(OnEvaluateClicked);

        if (recruitmentManager != null)
            recruitmentManager.OnPendingCandidatesChanged += OnPendingCandidatesChanged;

        RebuildList();
    }

    private void OnDisable()
    {
        if (btnClose != null)
            btnClose.onClick.RemoveListener(Close);

        if (btnEvaluate != null)
            btnEvaluate.onClick.RemoveListener(OnEvaluateClicked);

        if (recruitmentManager != null)
            recruitmentManager.OnPendingCandidatesChanged -= OnPendingCandidatesChanged;
    }

    public void Open()
    {
        if (gameState != null && gameState.IsRunEnded)
            return;

        gameObject.SetActive(true);
        RebuildList();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    private void OnPendingCandidatesChanged()
    {
        RebuildList();
    }

    private void RebuildList()
    {
        foreach (var slot in spawnedSlots)
        {
            if (slot != null)
                Destroy(slot.gameObject);
        }

        spawnedSlots.Clear();

        if (txtTitle != null)
            txtTitle.text = "Candidate Review";

        if (recruitmentManager == null)
        {
            selectedCandidate = null;
            RefreshDetails();
            return;
        }

        IReadOnlyList<PendingRecruitCandidate> candidates = recruitmentManager.PendingCandidates;

        if (selectedCandidate != null && !ContainsCandidate(candidates, selectedCandidate))
            selectedCandidate = null;

        for (int i = 0; i < candidates.Count; i++)
        {
            PendingRecruitCandidate candidate = candidates[i];
            RecruitCandidateSlotUI slot = Instantiate(candidateSlotPrefab, candidateListContent);
            slot.Setup(candidate, candidate == selectedCandidate, OnCandidateSelected);
            spawnedSlots.Add(slot);
        }

        if (selectedCandidate == null && candidates.Count > 0)
            selectedCandidate = candidates[0];

        RefreshSlotSelection();
        RefreshDetails();
    }

    private bool ContainsCandidate(
        IReadOnlyList<PendingRecruitCandidate> candidates,
        PendingRecruitCandidate target)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] == target)
                return true;
        }

        return false;
    }

    private void OnCandidateSelected(PendingRecruitCandidate candidate)
    {
        selectedCandidate = candidate;
        RefreshSlotSelection();
        RefreshDetails();
    }

    private void RefreshSlotSelection()
    {
        for (int i = 0; i < spawnedSlots.Count; i++)
            spawnedSlots[i].SetSelected(spawnedSlots[i].Candidate == selectedCandidate);
    }

    private void RefreshDetails()
    {
        bool hasCandidate = selectedCandidate != null;

        if (txtCandidateName != null)
            txtCandidateName.text = hasCandidate ? selectedCandidate.CandidateName : "No pending candidates";

        if (imgPortrait != null)
        {
            bool hasPortrait = hasCandidate && selectedCandidate.Portrait != null;
            imgPortrait.sprite = hasPortrait ? selectedCandidate.Portrait : null;
            imgPortrait.enabled = hasPortrait;
        }

        if (txtStats != null)
        {
            txtStats.text = hasCandidate
                ? $"INT {selectedCandidate.INT}  |  STR {selectedCandidate.STR}  |  AGI {selectedCandidate.AGI}"
                : "No one is waiting for review.";
        }

        if (txtSource != null)
        {
            if (hasCandidate)
            {
                string districtName = selectedCandidate.SourceDistrict != null
                    ? selectedCandidate.SourceDistrict.Data.districtName
                    : "Unknown District";

                string missionName = selectedCandidate.SourceMission != null
                    ? selectedCandidate.SourceMission.missionName
                    : "Unknown Mission";

                txtSource.text = $"Arrived from {districtName} via {missionName}";
            }
            else
            {
                txtSource.text = "";
            }
        }

        if (txtStatus != null)
        {
            txtStatus.text = hasCandidate
                ? "Run the placeholder vetting check. Current implementation is a 50/50 reveal."
                : lastResultMessage;
        }

        if (btnEvaluate != null)
            btnEvaluate.interactable = hasCandidate && (gameState == null || !gameState.IsRunEnded);
    }

    private void OnEvaluateClicked()
    {
        if (selectedCandidate == null || recruitmentManager == null)
            return;

        PendingRecruitCandidate evaluatedCandidate = selectedCandidate;
        CandidateVettingOutcome outcome =
            recruitmentManager.EvaluateCandidatePlaceholder(evaluatedCandidate);

        lastResultMessage = outcome switch
        {
            CandidateVettingOutcome.Loyal =>
                $"{evaluatedCandidate.CandidateName} proved loyal and joined the roster.",
            CandidateVettingOutcome.Implant =>
                $"{evaluatedCandidate.CandidateName} was exposed as an implant and discarded.",
            _ =>
                "Candidate review could not be completed."
        };

        selectedCandidate = null;
        RebuildList();
    }
}