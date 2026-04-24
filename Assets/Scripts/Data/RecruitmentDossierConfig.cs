using UnityEngine;

[CreateAssetMenu(
    fileName = "RecruitmentDossierConfig",
    menuName = "Gameplay/Recruitment/Dossier Config")]
public class RecruitmentDossierConfig : ScriptableObject
{
    [Header("Recruitment Center Progression")]
    [Tooltip("Ordered buildings that count as Recruitment Center intel levels. " +
             "If the first building is built, the Center is level 1. " +
             "If the first and second are built, it is level 2, and so on.")]
    public BuildingData[] intelLevelBuildings = new BuildingData[0];

    [Tooltip("How many verified true facts are revealed at each level. " +
             "Index 0 = no relevant building built, index 1 = Center level 1, index 2 = level 2, etc. " +
             "If the current level exceeds the array length, the last value is reused.")]
    public int[] verifiedFactsRevealedByLevel = new int[] { 0, 2, 4 };

    [Header("Presentation")]
    [Tooltip("Shown when a fact exists but is not yet verified by the Recruitment Center.")]
    public string hiddenFactPlaceholder = "???";

    public int GetRecruitmentCenterLevel(BaseProgressionManager baseProgression)
    {
        if (baseProgression == null || intelLevelBuildings == null || intelLevelBuildings.Length == 0)
            return 0;

        int level = 0;

        for (int i = 0; i < intelLevelBuildings.Length; i++)
        {
            BuildingData levelBuilding = intelLevelBuildings[i];
            if (levelBuilding == null)
                break;

            if (!baseProgression.IsBuildingBuilt(levelBuilding))
                break;

            level++;
        }

        return level;
    }

    public int GetVerifiedFactRevealCount(BaseProgressionManager baseProgression)
    {
        return GetVerifiedFactRevealCount(GetRecruitmentCenterLevel(baseProgression));
    }

    public int GetVerifiedFactRevealCount(int recruitmentCenterLevel)
    {
        if (verifiedFactsRevealedByLevel == null || verifiedFactsRevealedByLevel.Length == 0)
            return 0;

        int index = Mathf.Clamp(recruitmentCenterLevel, 0, verifiedFactsRevealedByLevel.Length - 1);
        return Mathf.Max(0, verifiedFactsRevealedByLevel[index]);
    }

    public string GetHiddenFactPlaceholder()
    {
        return string.IsNullOrWhiteSpace(hiddenFactPlaceholder)
            ? "???"
            : hiddenFactPlaceholder;
    }

    private void OnValidate()
    {
        if (verifiedFactsRevealedByLevel != null)
        {
            for (int i = 0; i < verifiedFactsRevealedByLevel.Length; i++)
                verifiedFactsRevealedByLevel[i] = Mathf.Max(0, verifiedFactsRevealedByLevel[i]);
        }

        if (string.IsNullOrWhiteSpace(hiddenFactPlaceholder))
            hiddenFactPlaceholder = "???";
    }
}