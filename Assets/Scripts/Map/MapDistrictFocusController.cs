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

    private MaterialPropertyBlock propertyBlock;
    private Material runtimeMaterial;
    private DistrictMapNode focusedNode;
    private Color previousMaskColor = Color.red;
    private Color hoveredMaskColor = Color.red;
    private float districtBlend = 1f;
    private float focusStrength;
    private float targetFocusStrength;

    public bool HasFocusedDistrict => focusedNode != null;

    private void Reset()
    {
        ResolveReferences();
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

    private static bool ColorsDiffer(Color a, Color b)
    {
        float r = a.r - b.r;
        float g = a.g - b.g;
        float bl = a.b - b.b;
        return r * r + g * g + bl * bl > 0.0001f;
    }

    public void ClearFocus(DistrictMapNode node = null)
    {
        if (node != null && focusedNode != node)
            return;

        focusedNode = null;
        targetFocusStrength = 0f;
        ApplyAllProperties();
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
    }
}
