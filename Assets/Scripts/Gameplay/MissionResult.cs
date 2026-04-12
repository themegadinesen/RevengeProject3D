using System.Collections.Generic;

public class MissionResult
{
    public MissionData Data;
    public RuntimeDistrict District;
    public bool Success;
    public float Score;
    public float SuccessChance;
    public float DistrictCureMultiplier;
    public float BonusAgentLossChance;
    public float DistrictHeatBeforeOutcome;
    public float DistrictHeatAfterOutcome;
    public DistrictResponseState ResponseState;
    public int ActualMoneyReward;
    public List<RuntimeAgent> LostAgents;
    public PendingRecruitCandidate ArrivedCandidate;
}