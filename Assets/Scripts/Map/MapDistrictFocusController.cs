using UnityEngine;

public class MapDistrictFocusController : MonoBehaviour
{
    private static readonly int DistrictMaskTexId = Shader.PropertyToID("_DistrictMaskTex");
    private static readonly int PreviousDistrictColorId = Shader.PropertyToID("_PreviousDistrictColor");
    private static readonly int HoveredDistrictColorId = Shader.PropertyToID("_HoveredDistrictColor");
    private static readonly int DistrictBlendId = Shader.PropertyToID("_DistrictBlend");
    private static readonly int FocusStrengthId = Shader.PropertyToID("_FocusStrength");
    private static readonly int OutsideBrightnessId = Shader.PropertyToID("_OutsideBrightness");
    private static readonly int OutsideSaturationId = Shader.PropertyToID("_OutsideSaturation");
    private static readonly int HoverBrightnessId = Shader.PropertyToID("_HoverBrightness");
    private static readonly int MaskToleranceId = Shader.PropertyToID("_MaskTolerance");
    private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");

    private static readonly int LandAlbedoTexId = Shader.PropertyToID("_LandAlbedoTex");
    private static readonly int LandNormalTexId = Shader.PropertyToID("_LandNormalTex");
    private static readonly int LandPackedTexId = Shader.PropertyToID("_LandPackedTex");
    private static readonly int LandAlbedoStrengthId = Shader.PropertyToID("_LandAlbedoStrength");
    private static readonly int LandNormalStrengthId = Shader.PropertyToID("_LandNormalStrength");
    private static readonly int LandHeightStrengthId = Shader.PropertyToID("_LandHeightStrength");
    private static readonly int LandAOStrengthId = Shader.PropertyToID("_LandAOStrength");
    private static readonly int DistrictTintStrengthId = Shader.PropertyToID("_DistrictTintStrength");
    private static readonly int BlendWidthId = Shader.PropertyToID("_BlendWidth");

    private static readonly int CoastlineWidthId = Shader.PropertyToID("_CoastlineWidth");
    private static readonly int CoastRimStrengthId = Shader.PropertyToID("_CoastRimStrength");
    private static readonly int CoastWetStrengthId = Shader.PropertyToID("_CoastWetStrength");
    private static readonly int CoastAlphaSoftnessId = Shader.PropertyToID("_CoastAlphaSoftness");
    private static readonly int CoastSandColorId = Shader.PropertyToID("_CoastSandColor");

    private static readonly int CapitalMaskColorId = Shader.PropertyToID("_CapitalMaskColor");
    private static readonly int TechMaskColorId = Shader.PropertyToID("_TechMaskColor");
    private static readonly int ForestMaskColorId = Shader.PropertyToID("_ForestMaskColor");
    private static readonly int MountainsMaskColorId = Shader.PropertyToID("_MountainsMaskColor");
    private static readonly int SnowMaskColorId = Shader.PropertyToID("_SnowMaskColor");
    private static readonly int CoastMaskColorId = Shader.PropertyToID("_CoastMaskColor");
    private static readonly int SandMaskColorId = Shader.PropertyToID("_SandMaskColor");

    private static readonly int CapitalColorId = Shader.PropertyToID("_CapitalColor");
    private static readonly int TechColorId = Shader.PropertyToID("_TechColor");
    private static readonly int ForestColorId = Shader.PropertyToID("_ForestColor");
    private static readonly int MountainsColorId = Shader.PropertyToID("_MountainsColor");
    private static readonly int SnowColorId = Shader.PropertyToID("_SnowColor");
    private static readonly int CoastColorId = Shader.PropertyToID("_CoastColor");
    private static readonly int SandColorId = Shader.PropertyToID("_SandColor");

    [Header("References")]
    [Tooltip("Renderer that draws the visible country map PNG.")]
    [SerializeField] private Renderer mapRenderer;
    [Tooltip("Mask PNG where each district is painted with a unique solid RGB color.")]
    [SerializeField] private Texture2D maskTexture;
    [Tooltip("District collider nodes that provide mask colors for hover focus.")]
    [SerializeField] private DistrictMapNode[] districtNodes;

    [Header("Material Updates")]
    [Tooltip("Uses per-renderer shader values without editing the shared material asset.")]
    [SerializeField] private bool useMaterialPropertyBlock = true;

    [Header("Focus Animation")]
    [Tooltip("Seconds for hover focus to fade in or out.")]
    [Min(0.01f)]
    [SerializeField] private float transitionDuration = 0.2f;

    [Header("Focus Look")]
    [Range(0f, 1f)]
    [SerializeField] private float outsideBrightness = 0.68f;
    [Range(0f, 1f)]
    [SerializeField] private float outsideSaturation = 0.35f;
    [Range(0.5f, 1.5f)]
    [SerializeField] private float hoverBrightness = 1.08f;
    [Range(0f, 0.25f)]
    [SerializeField] private float maskTolerance = 0.025f;
    [Range(0.001f, 0.25f)]
    [SerializeField] private float edgeSoftness = 0.035f;

    [Header("Authored Land Textures")]
    [Tooltip("Map-aligned natural terrain albedo. Same resolution and UV layout as the visible map PNG.")]
    [SerializeField] private Texture2D landAlbedoTexture;
    [Tooltip("Map-aligned tangent/sprite-space normal map.")]
    [SerializeField] private Texture2D landNormalTexture;
    [Tooltip("Map-aligned packed texture. R height, G ambient occlusion, B detail/roughness, A optional district/detail intensity.")]
    [SerializeField] private Texture2D landPackedTexture;

    [Header("Authored Land Response")]
    [Range(0f, 1f)]
    [SerializeField] private float landAlbedoStrength = 1f;
    [Range(0f, 2f)]
    [SerializeField] private float landNormalStrength = 0.55f;
    [Range(0f, 2f)]
    [SerializeField] private float landHeightStrength = 0.35f;
    [Range(0f, 1f)]
    [SerializeField] private float landAOStrength = 0.45f;
    [Range(0f, 1f)]
    [SerializeField] private float districtTintStrength = 0.25f;
    [Tooltip("Mask-neighbor sample distance in texture pixels. Higher values soften district tint transitions.")]
    [Range(0f, 16f)]
    [SerializeField] private float blendWidth = 5f;

    [Header("Coastline Material")]
    [Tooltip("Coastline effect width in source texture pixels.")]
    [Range(0f, 32f)]
    [SerializeField] private float coastlineWidth = 8f;
    [Tooltip("Strength of the light sandy rim at the land edge.")]
    [Range(0f, 1f)]
    [SerializeField] private float coastRimStrength = 0.42f;
    [Tooltip("Strength of the darker inland wet edge just inside coastlines.")]
    [Range(0f, 1f)]
    [SerializeField] private float coastWetStrength = 0.22f;
    [Tooltip("How much land alpha softens at the very edge for smoother water transition.")]
    [Range(0f, 1f)]
    [SerializeField] private float coastAlphaSoftness = 0.16f;
    [ColorUsage(false, false)]
    [SerializeField] private Color coastSandColor = new(0.92f, 0.82f, 0.58f, 1f);

    [Header("District Tint Colors")]
    [ColorUsage(false, false)]
    [SerializeField] private Color capitalColor = new(0.72f, 0.7f, 0.72f, 1f);
    [ColorUsage(false, false)]
    [SerializeField] private Color techColor = new(0.5f, 0.62f, 0.68f, 1f);
    [ColorUsage(false, false)]
    [SerializeField] private Color forestColor = new(0.48f, 0.62f, 0.38f, 1f);
    [ColorUsage(false, false)]
    [SerializeField] private Color mountainsColor = new(0.66f, 0.6f, 0.52f, 1f);
    [ColorUsage(false, false)]
    [SerializeField] private Color snowColor = new(0.86f, 0.9f, 0.92f, 1f);
    [ColorUsage(false, false)]
    [SerializeField] private Color coastColor = new(0.58f, 0.7f, 0.66f, 1f);
    [ColorUsage(false, false)]
    [SerializeField] private Color sandColor = new(0.86f, 0.72f, 0.46f, 1f);

    private MaterialPropertyBlock propertyBlock;
    private Material runtimeMaterial;
    private DistrictMapNode focusedNode;
    private Color previousMaskColor = Color.red;
    private Color hoveredMaskColor = Color.red;
    private float districtBlend = 1f;
    private float focusStrength;
    private float targetFocusStrength;

    public bool HasFocusedDistrict => focusedNode != null;
    public float CurrentFocusStrength => Mathf.SmoothStep(0f, 1f, focusStrength);
    public float OutsideBrightness => outsideBrightness;
    public float OutsideSaturation => outsideSaturation;

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
        ApplyAllProperties();
    }

    private void Awake()
    {
        ResolveReferences();
        ApplyAllProperties();
    }

    private void OnDisable()
    {
        focusedNode = null;
        districtBlend = 1f;
        focusStrength = 0f;
        targetFocusStrength = 0f;
        ApplyAllProperties();
    }

    private void Update()
    {
        bool changed = false;
        float step = Time.unscaledDeltaTime / transitionDuration;

        if (!Mathf.Approximately(focusStrength, targetFocusStrength))
        {
            focusStrength = Mathf.MoveTowards(focusStrength, targetFocusStrength, step);
            changed = true;
        }

        if (!Mathf.Approximately(districtBlend, 1f))
        {
            districtBlend = Mathf.MoveTowards(districtBlend, 1f, step);
            changed = true;
        }

        if (changed)
            ApplyAllProperties();
    }

    public void FocusDistrict(DistrictMapNode node)
    {
        if (node == null)
        {
            ClearFocus();
            return;
        }

        Color newMaskColor = node.MaskColor;
        bool isChangingDistrict = focusStrength > 0.001f &&
                                  ColorsDiffer(hoveredMaskColor, newMaskColor);

        previousMaskColor = isChangingDistrict ? hoveredMaskColor : newMaskColor;
        hoveredMaskColor = newMaskColor;
        districtBlend = isChangingDistrict ? 0f : 1f;
        focusedNode = node;
        targetFocusStrength = 1f;
        ApplyAllProperties();
    }

    public void ClearFocus(DistrictMapNode node = null)
    {
        if (node != null && focusedNode != node)
            return;

        focusedNode = null;
        targetFocusStrength = 0f;
        ApplyAllProperties();
    }

    private static bool ColorsDiffer(Color a, Color b)
    {
        float r = a.r - b.r;
        float g = a.g - b.g;
        float bl = a.b - b.b;
        return r * r + g * g + bl * bl > 0.0001f;
    }

    private void ResolveReferences()
    {
        if (mapRenderer == null)
            mapRenderer = GetComponent<Renderer>();

        if (districtNodes == null || districtNodes.Length == 0)
            districtNodes = FindObjectsByType<DistrictMapNode>(FindObjectsSortMode.None);

        if (useMaterialPropertyBlock && propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        if (!useMaterialPropertyBlock && mapRenderer != null && runtimeMaterial == null)
            runtimeMaterial = mapRenderer.material;
    }

    private void ApplyAllProperties()
    {
        if (mapRenderer == null)
            return;

        if (useMaterialPropertyBlock)
        {
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();

            mapRenderer.GetPropertyBlock(propertyBlock);
            WriteProperties(propertyBlock);
            mapRenderer.SetPropertyBlock(propertyBlock);
            return;
        }

        if (runtimeMaterial == null)
            runtimeMaterial = mapRenderer.material;

        WriteProperties(runtimeMaterial);
    }

    private void WriteProperties(MaterialPropertyBlock block)
    {
        if (maskTexture != null)
            block.SetTexture(DistrictMaskTexId, maskTexture);

        block.SetColor(HoveredDistrictColorId, hoveredMaskColor);
        block.SetColor(PreviousDistrictColorId, previousMaskColor);
        block.SetFloat(DistrictBlendId, Mathf.SmoothStep(0f, 1f, districtBlend));
        block.SetFloat(FocusStrengthId, Mathf.SmoothStep(0f, 1f, focusStrength));
        block.SetFloat(OutsideBrightnessId, outsideBrightness);
        block.SetFloat(OutsideSaturationId, outsideSaturation);
        block.SetFloat(HoverBrightnessId, hoverBrightness);
        block.SetFloat(MaskToleranceId, maskTolerance);
        block.SetFloat(EdgeSoftnessId, edgeSoftness);

        WriteLandProperties(block);
        WriteTerrainMaskColors(block);
    }

    private void WriteProperties(Material material)
    {
        if (material == null)
            return;

        if (maskTexture != null)
            material.SetTexture(DistrictMaskTexId, maskTexture);

        material.SetColor(HoveredDistrictColorId, hoveredMaskColor);
        material.SetColor(PreviousDistrictColorId, previousMaskColor);
        material.SetFloat(DistrictBlendId, Mathf.SmoothStep(0f, 1f, districtBlend));
        material.SetFloat(FocusStrengthId, Mathf.SmoothStep(0f, 1f, focusStrength));
        material.SetFloat(OutsideBrightnessId, outsideBrightness);
        material.SetFloat(OutsideSaturationId, outsideSaturation);
        material.SetFloat(HoverBrightnessId, hoverBrightness);
        material.SetFloat(MaskToleranceId, maskTolerance);
        material.SetFloat(EdgeSoftnessId, edgeSoftness);

        WriteLandProperties(material);
        WriteTerrainMaskColors(material);
    }

    private void WriteLandProperties(MaterialPropertyBlock block)
    {
        if (landAlbedoTexture != null)
            block.SetTexture(LandAlbedoTexId, landAlbedoTexture);

        if (landNormalTexture != null)
            block.SetTexture(LandNormalTexId, landNormalTexture);

        if (landPackedTexture != null)
            block.SetTexture(LandPackedTexId, landPackedTexture);

        block.SetFloat(LandAlbedoStrengthId, landAlbedoStrength);
        block.SetFloat(LandNormalStrengthId, landNormalStrength);
        block.SetFloat(LandHeightStrengthId, landHeightStrength);
        block.SetFloat(LandAOStrengthId, landAOStrength);
        block.SetFloat(DistrictTintStrengthId, districtTintStrength);
        block.SetFloat(BlendWidthId, blendWidth);

        block.SetFloat(CoastlineWidthId, coastlineWidth);
        block.SetFloat(CoastRimStrengthId, coastRimStrength);
        block.SetFloat(CoastWetStrengthId, coastWetStrength);
        block.SetFloat(CoastAlphaSoftnessId, coastAlphaSoftness);
        block.SetColor(CoastSandColorId, coastSandColor);

        block.SetColor(CapitalColorId, capitalColor);
        block.SetColor(TechColorId, techColor);
        block.SetColor(ForestColorId, forestColor);
        block.SetColor(MountainsColorId, mountainsColor);
        block.SetColor(SnowColorId, snowColor);
        block.SetColor(CoastColorId, coastColor);
        block.SetColor(SandColorId, sandColor);
    }

    private void WriteLandProperties(Material material)
    {
        if (landAlbedoTexture != null)
            material.SetTexture(LandAlbedoTexId, landAlbedoTexture);

        if (landNormalTexture != null)
            material.SetTexture(LandNormalTexId, landNormalTexture);

        if (landPackedTexture != null)
            material.SetTexture(LandPackedTexId, landPackedTexture);

        material.SetFloat(LandAlbedoStrengthId, landAlbedoStrength);
        material.SetFloat(LandNormalStrengthId, landNormalStrength);
        material.SetFloat(LandHeightStrengthId, landHeightStrength);
        material.SetFloat(LandAOStrengthId, landAOStrength);
        material.SetFloat(DistrictTintStrengthId, districtTintStrength);
        material.SetFloat(BlendWidthId, blendWidth);

        material.SetFloat(CoastlineWidthId, coastlineWidth);
        material.SetFloat(CoastRimStrengthId, coastRimStrength);
        material.SetFloat(CoastWetStrengthId, coastWetStrength);
        material.SetFloat(CoastAlphaSoftnessId, coastAlphaSoftness);
        material.SetColor(CoastSandColorId, coastSandColor);

        material.SetColor(CapitalColorId, capitalColor);
        material.SetColor(TechColorId, techColor);
        material.SetColor(ForestColorId, forestColor);
        material.SetColor(MountainsColorId, mountainsColor);
        material.SetColor(SnowColorId, snowColor);
        material.SetColor(CoastColorId, coastColor);
        material.SetColor(SandColorId, sandColor);
    }

    private void WriteTerrainMaskColors(MaterialPropertyBlock block)
    {
        if (districtNodes == null)
            return;

        for (int i = 0; i < districtNodes.Length; i++)
        {
            DistrictMapNode node = districtNodes[i];
            if (TryGetTerrainMaskColorId(node, out int colorId))
                block.SetColor(colorId, node.MaskColor);
        }
    }

    private void WriteTerrainMaskColors(Material material)
    {
        if (districtNodes == null)
            return;

        for (int i = 0; i < districtNodes.Length; i++)
        {
            DistrictMapNode node = districtNodes[i];
            if (TryGetTerrainMaskColorId(node, out int colorId))
                material.SetColor(colorId, node.MaskColor);
        }
    }

    private static bool TryGetTerrainMaskColorId(DistrictMapNode node, out int colorId)
    {
        colorId = 0;

        if (node == null)
            return false;

        string districtName = node.Data != null ? node.Data.districtName : node.name;
        if (string.IsNullOrWhiteSpace(districtName))
            return false;

        if (ContainsDistrictName(districtName, "capital"))
        {
            colorId = CapitalMaskColorId;
            return true;
        }

        if (ContainsDistrictName(districtName, "tech"))
        {
            colorId = TechMaskColorId;
            return true;
        }

        if (ContainsDistrictName(districtName, "forest"))
        {
            colorId = ForestMaskColorId;
            return true;
        }

        if (ContainsDistrictName(districtName, "mountain"))
        {
            colorId = MountainsMaskColorId;
            return true;
        }

        if (ContainsDistrictName(districtName, "snow"))
        {
            colorId = SnowMaskColorId;
            return true;
        }

        if (ContainsDistrictName(districtName, "coast"))
        {
            colorId = CoastMaskColorId;
            return true;
        }

        if (ContainsDistrictName(districtName, "sand"))
        {
            colorId = SandMaskColorId;
            return true;
        }

        return false;
    }

    private static bool ContainsDistrictName(string districtName, string value)
    {
        return districtName.IndexOf(value, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
