// Assets/Scripts/Gameplay/PendingRecruitCandidate.cs
using UnityEngine;

[System.Serializable]
public class PendingRecruitCandidate
{
    public AgentData Template { get; private set; }
    public string CandidateName { get; private set; }
    public Sprite Portrait { get; private set; }
    public int INT { get; private set; }
    public int STR { get; private set; }
    public int AGI { get; private set; }
    public MissionData SourceMission { get; private set; }
    public RuntimeDistrict SourceDistrict { get; private set; }
    public float ArrivalTime { get; private set; }

    public PendingRecruitCandidate(
        AgentData template,
        MissionData sourceMission,
        RuntimeDistrict sourceDistrict)
    {
        Template = template;
        SourceMission = sourceMission;
        SourceDistrict = sourceDistrict;
        ArrivalTime = Time.time;

        if (template == null)
        {
            CandidateName = "Unknown Candidate";
            INT = 1;
            STR = 1;
            AGI = 1;
            return;
        }

        CandidateName = template.agentName;
        Portrait = template.portrait;
        INT = template.baseINT;
        STR = template.baseSTR;
        AGI = template.baseAGI;
    }
}