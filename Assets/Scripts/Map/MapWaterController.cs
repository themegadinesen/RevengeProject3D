using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Applies cheap map-water shader settings to a SpriteRenderer, SpriteShapeRenderer, MeshRenderer, or other Renderer.
/// The distance mask should be authored in the same 0-1 layout as the visible map/water geometry UVs:
/// low values near the coast produce shallow water, high values farther out produce deep water.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Map/Map Water Controller")]
public class MapWaterController : MonoBehaviour
{
    private enum DistanceMaskMapping
    {
        RendererUV,
        WaterRendererBounds,
        ReferenceRendererBounds
    }

    private enum DebugView
    {
        Off,
        DistanceMask
    }

    private static readonly int ShallowColorId = Shader.PropertyToID("_ShallowColor");
    private static readonly int DeepColorId = Shader.PropertyToID("_DeepColor");
    private static readonly int DistanceMaskId = Shader.PropertyToID("_DistanceMask");
    private static readonly int DistanceMaskScaleOffsetId = Shader.PropertyToID("_DistanceMaskScaleOffset");
    private static readonly int UseWorldMaskUVId = Shader.PropertyToID("_UseWorldMaskUV");
    private static readonly int DistanceMaskWorldBoundsId = Shader.PropertyToID("_DistanceMaskWorldBounds");
    private static readonly int DebugViewId = Shader.PropertyToID("_DebugView");
    private static readonly int NoiseTexId = Shader.PropertyToID("_NoiseTex");
    private static readonly int ShoreStartId = Shader.PropertyToID("_ShoreStart");
    private static readonly int ShoreEndId = Shader.PropertyToID("_ShoreEnd");
    private static readonly int NoiseTilingId = Shader.PropertyToID("_NoiseTiling");
    private static readonly int NoiseSpeedId = Shader.PropertyToID("_NoiseSpeed");
    private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int FocusStrengthId = Shader.PropertyToID("_FocusStrength");
    private static readonly int FocusBrightnessId = Shader.PropertyToID("_FocusBrightness");
    private static readonly int FocusSaturationId = Shader.PropertyToID("_FocusSaturation");
    private static readonly int FoamColorId = Shader.PropertyToID("_FoamColor");
    private static readonly int FoamAmountId = Shader.PropertyToID("_FoamAmount");
    private static readonly int FoamWidthId = Shader.PropertyToID("_FoamWidth");
    private static readonly int FoamOpacityId = Shader.PropertyToID("_FoamOpacity");
    private static readonly int TextureStrengthId = Shader.PropertyToID("_TextureStrength");
    private static readonly int FoamNoiseTilingId = Shader.PropertyToID("_FoamNoiseTiling");
    private static readonly int FoamNoiseStrengthId = Shader.PropertyToID("_FoamNoiseStrength");
    private static readonly int FoamSpeedId = Shader.PropertyToID("_FoamSpeed");
    private static readonly int FoamFlowSpeedId = Shader.PropertyToID("_FoamFlowSpeed");
    private static readonly int ShoreFoamFrequencyId = Shader.PropertyToID("_ShoreFoamFrequency");
    private static readonly int ShoreFoamSpeedId = Shader.PropertyToID("_ShoreFoamSpeed");
    private static readonly int ShoreWaveSpeedId = Shader.PropertyToID("_ShoreWaveSpeed");
    private static readonly int ShoreWaveSyncId = Shader.PropertyToID("_ShoreWaveSync");
    private static readonly int ShoreBounceDistanceId = Shader.PropertyToID("_ShoreBounceDistance");
    private static readonly int ShoreBounceStrengthId = Shader.PropertyToID("_ShoreBounceStrength");
    private static readonly int WaveTilingId = Shader.PropertyToID("_WaveTiling");
    private static readonly int WaveDetailTilingId = Shader.PropertyToID("_WaveDetailTiling");
    private static readonly int WaveRoughnessId = Shader.PropertyToID("_WaveRoughness");
    private static readonly int WaveIrregularityId = Shader.PropertyToID("_WaveIrregularity");
    private static readonly int WaveSpeedAId = Shader.PropertyToID("_WaveSpeedA");
    private static readonly int WaveSpeedBId = Shader.PropertyToID("_WaveSpeedB");
    private static readonly int WaveDirectionId = Shader.PropertyToID("_WaveDirection");
    private static readonly int WaveFrequencyId = Shader.PropertyToID("_WaveFrequency");
    private static readonly int WaveSpeedId = Shader.PropertyToID("_WaveSpeed");
    private static readonly int WaveContrastId = Shader.PropertyToID("_WaveContrast");
    private static readonly int WaveBandSharpnessId = Shader.PropertyToID("_WaveBandSharpness");
    private static readonly int WaveNormalStrengthId = Shader.PropertyToID("_WaveNormalStrength");
    private static readonly int NormalStrengthId = Shader.PropertyToID("_NormalStrength");
    private static readonly int SpecularIntensityId = Shader.PropertyToID("_SpecularIntensity");
    private static readonly int SpecularPowerId = Shader.PropertyToID("_SpecularPower");
    private static readonly int HighlightColorId = Shader.PropertyToID("_HighlightColor");
    private static readonly int HighlightStrengthId = Shader.PropertyToID("_HighlightStrength");
    private static readonly int HighlightPowerId = Shader.PropertyToID("_HighlightPower");
    private static readonly int CrestColorId = Shader.PropertyToID("_CrestColor");
    private static readonly int CrestStrengthId = Shader.PropertyToID("_CrestStrength");
    private static readonly int CrestFoamAmountId = Shader.PropertyToID("_CrestFoamAmount");
    private static readonly int CrestFoamThresholdId = Shader.PropertyToID("_CrestFoamThreshold");
    private static readonly int ReflectionStrengthId = Shader.PropertyToID("_ReflectionStrength");

    [Header("References")]
    [Tooltip("Renderer that draws the water geometry. Supports SpriteRenderer, SpriteShapeRenderer, MeshRenderer, and similar renderers.")]
    [SerializeField] private Renderer waterRenderer = null;
    [Tooltip("Per-object material instance. If empty, the controller creates one from the renderer material so the shared asset is not edited.")]
    [SerializeField] private Material waterMaterialInstance = null;
    [Tooltip("Grayscale mask aligned to the map UVs. Black/low values are shallow near coast, white/high values are deeper water.")]
    [SerializeField] private Texture2D distanceMask = null;
    [Tooltip("Optional tileable grayscale noise texture. Procedural shader waves still animate when this is unassigned.")]
    [SerializeField] private Texture2D noiseTexture = null;
    [Tooltip("Optional map/land renderer used to align the distance mask in world space. Use this when Sprite Shape UVs do not match the mask.")]
    [SerializeField] private Renderer maskReferenceRenderer = null;

    [Header("Map State")]
    [SerializeField] private ViewManager viewManager = null;
    [SerializeField] private bool autoFindViewManager = true;
    [SerializeField] private bool hideInBaseViews = true;
    [SerializeField] private bool visibleWhenViewManagerMissing = true;
    [SerializeField] private MapDistrictFocusController districtFocusController = null;
    [SerializeField] private bool autoFindDistrictFocusController = true;
    [SerializeField] private bool dimWithDistrictFocus = true;
    [SerializeField] private bool useDistrictFocusLook = true;
    [SerializeField, Range(0f, 1f)] private float focusBrightness = 0.68f;
    [SerializeField, Range(0f, 1f)] private float focusSaturation = 0.35f;
    [Tooltip("Puts the water on the same layer as the map sprite so ViewManager culling hides both at the same time.")]
    [SerializeField] private bool matchMapLayer = true;
    [Tooltip("Used when no mask reference renderer is assigned. This should match ViewManager's map layer.")]
    [SerializeField] private string fallbackMapLayerName = "MapView";

    [Header("Water Colors")]
    [SerializeField] private Color shallowColor = new Color(0.55f, 0.9f, 0.96f, 1f);
    [SerializeField] private Color deepColor = new Color(0.08f, 0.32f, 0.58f, 1f);
    [SerializeField, Range(0f, 1f)] private float alpha = 0.92f;

    [Header("Shore Mask")]
    [SerializeField, Range(0f, 1f)] private float shoreStart = 0.08f;
    [SerializeField, Range(0f, 1f)] private float shoreEnd = 0.85f;
    [Tooltip("Use this to line the distance mask up with water UVs without editing the texture.")]
    [SerializeField] private Vector2 distanceMaskTiling = Vector2.one;
    [Tooltip("Use this to nudge the distance mask when the water mesh/Sprite Shape UVs are offset from the map.")]
    [SerializeField] private Vector2 distanceMaskOffset = Vector2.zero;
    [Tooltip("Sprite Shape fill UVs are often tiled. Use Reference Renderer Bounds and assign the map sprite renderer for map-aligned masks.")]
    [SerializeField] private DistanceMaskMapping distanceMaskMapping = DistanceMaskMapping.ReferenceRendererBounds;

    [Header("Motion")]
    [SerializeField, Min(0.001f)] private float noiseTiling = 6f;
    [SerializeField] private Vector2 noiseSpeed = new Vector2(0.015f, 0.01f);
    [SerializeField, Range(0f, 0.25f)] private float noiseStrength = 0.035f;
    [SerializeField, Range(0f, 0.5f)] private float textureStrength = 0.13f;
    [SerializeField, Min(0.001f)] private float waveTiling = 7.5f;
    [SerializeField, Min(0.001f)] private float waveDetailTiling = 18f;
    [SerializeField, Range(0f, 2f)] private float waveRoughness = 0.8f;
    [SerializeField, Range(0f, 1f)] private float waveIrregularity = 0.65f;
    [SerializeField] private Vector2 waveSpeedA = new Vector2(0.06f, 0.025f);
    [SerializeField] private Vector2 waveSpeedB = new Vector2(-0.035f, 0.055f);
    [SerializeField] private Vector2 waveDirection = new Vector2(0.78f, 0.38f);
    [SerializeField, Min(0.001f)] private float waveFrequency = 8f;
    [SerializeField] private float waveSpeed = 0.28f;
    [SerializeField, Range(0f, 0.5f)] private float waveContrast = 0.18f;
    [SerializeField, Range(0.25f, 8f)] private float waveBandSharpness = 2.8f;
    [SerializeField, Range(0f, 2f)] private float waveNormalStrength = 0.75f;
    [SerializeField, Range(0f, 3f)] private float normalStrength = 1.35f;

    [Header("Foam")]
    [SerializeField] private Color foamColor = new Color(0.88f, 1f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float foamAmount = 0.12f;
    [SerializeField, Range(0.001f, 0.25f)] private float foamWidth = 0.055f;
    [SerializeField, Range(0f, 1f)] private float foamOpacity = 1f;
    [SerializeField, Min(0.001f)] private float foamNoiseTiling = 22f;
    [SerializeField, Range(0f, 1f)] private float foamNoiseStrength = 0.65f;
    [SerializeField] private Vector2 foamSpeed = new Vector2(0.035f, -0.02f);
    [SerializeField] private float foamFlowSpeed = 1.2f;
    [SerializeField, Min(0.001f)] private float shoreFoamFrequency = 42f;
    [SerializeField] private float shoreFoamSpeed = 0.55f;
    [SerializeField] private float shoreWaveSpeed = 1.35f;
    [SerializeField, Range(0f, 1f)] private float shoreWaveSync = 0.35f;
    [SerializeField, Range(0.01f, 0.6f)] private float shoreBounceDistance = 0.22f;
    [SerializeField, Range(0f, 1f)] private float shoreBounceStrength = 0.65f;

    [Header("Highlights")]
    [SerializeField] private Color highlightColor = new Color(0.82f, 1f, 1f, 1f);
    [SerializeField, Range(0f, 2f)] private float highlightStrength = 0.7f;
    [SerializeField, Range(1f, 96f)] private float highlightPower = 28f;
    [SerializeField, Range(0f, 2f)] private float specularIntensity = 0.38f;
    [SerializeField, Range(1f, 96f)] private float specularPower = 34f;
    [SerializeField] private Color crestColor = new Color(0.7f, 0.95f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float crestStrength = 0.22f;
    [SerializeField, Range(0f, 1f)] private float crestFoamAmount = 0.18f;
    [SerializeField, Range(0f, 1f)] private float crestFoamThreshold = 0.68f;
    [SerializeField, Range(0f, 1f)] private float reflectionStrength = 0f;

    [Header("Editor Gizmo")]
    [SerializeField] private bool drawAlignmentGizmo = true;
    [SerializeField] private Color gizmoColor = new Color(0.25f, 0.8f, 1f, 0.45f);
    [SerializeField] private DebugView debugView = DebugView.Off;

    public Renderer WaterRenderer => waterRenderer;
    public Material WaterMaterialInstance => waterMaterialInstance;

    private void Reset()
    {
        waterRenderer = GetComponent<Renderer>();
        EnsureMaterialInstance();
        ApplySettings();
    }

    private void Awake()
    {
        ResolveStateReferences();
        ApplyMapLayer();
        EnsureMaterialInstance();
        ApplySettings();
        RefreshVisibility();
    }

    private void OnEnable()
    {
        ResolveStateReferences();
        ApplyMapLayer();
        EnsureMaterialInstance();
        ApplySettings();
        RefreshVisibility();
    }

    private void Update()
    {
        ResolveStateReferences();
        ApplyMapLayer();
        ApplySettings();
        RefreshVisibility();
    }

    private void OnValidate()
    {
        shoreEnd = Mathf.Max(shoreStart + 0.0001f, shoreEnd);
        noiseTiling = Mathf.Max(0.001f, noiseTiling);
        waveTiling = Mathf.Max(0.001f, waveTiling);
        waveDetailTiling = Mathf.Max(0.001f, waveDetailTiling);
        waveFrequency = Mathf.Max(0.001f, waveFrequency);
        foamWidth = Mathf.Max(0.001f, foamWidth);
        foamNoiseTiling = Mathf.Max(0.001f, foamNoiseTiling);
        shoreFoamFrequency = Mathf.Max(0.001f, shoreFoamFrequency);
        shoreBounceDistance = Mathf.Max(0.01f, shoreBounceDistance);

        ResolveStateReferences();
        ApplyMapLayer();
        EnsureMaterialInstance();
        ApplySettings();
        RefreshVisibility();
    }

    [ContextMenu("Apply Water Settings")]
    public void ApplySettings()
    {
        Material material = GetWritableMaterial();
        if (material == null)
            return;

        material.SetColor(ShallowColorId, shallowColor);
        material.SetColor(DeepColorId, deepColor);
        material.SetFloat(ShoreStartId, shoreStart);
        material.SetFloat(ShoreEndId, shoreEnd);
        material.SetVector(DistanceMaskScaleOffsetId, new Vector4(
            distanceMaskTiling.x,
            distanceMaskTiling.y,
            distanceMaskOffset.x,
            distanceMaskOffset.y));
        material.SetFloat(UseWorldMaskUVId, distanceMaskMapping == DistanceMaskMapping.RendererUV ? 0f : 1f);
        material.SetVector(DistanceMaskWorldBoundsId, GetMaskWorldBoundsVector());
        material.SetFloat(DebugViewId, debugView == DebugView.DistanceMask ? 1f : 0f);
        material.SetFloat(NoiseTilingId, noiseTiling);
        material.SetVector(NoiseSpeedId, new Vector4(noiseSpeed.x, noiseSpeed.y, 0f, 0f));
        material.SetFloat(NoiseStrengthId, noiseStrength);
        material.SetFloat(TextureStrengthId, textureStrength);
        material.SetFloat(AlphaId, alpha);
        material.SetFloat(FocusStrengthId, GetDistrictFocusStrength());
        material.SetFloat(FocusBrightnessId, GetFocusBrightness());
        material.SetFloat(FocusSaturationId, GetFocusSaturation());
        material.SetColor(FoamColorId, foamColor);
        material.SetFloat(FoamAmountId, foamAmount);
        material.SetFloat(FoamWidthId, foamWidth);
        material.SetFloat(FoamOpacityId, foamOpacity);
        material.SetFloat(FoamNoiseTilingId, foamNoiseTiling);
        material.SetFloat(FoamNoiseStrengthId, foamNoiseStrength);
        material.SetVector(FoamSpeedId, new Vector4(foamSpeed.x, foamSpeed.y, 0f, 0f));
        material.SetFloat(FoamFlowSpeedId, foamFlowSpeed);
        material.SetFloat(ShoreFoamFrequencyId, shoreFoamFrequency);
        material.SetFloat(ShoreFoamSpeedId, shoreFoamSpeed);
        material.SetFloat(ShoreWaveSpeedId, shoreWaveSpeed);
        material.SetFloat(ShoreWaveSyncId, shoreWaveSync);
        material.SetFloat(ShoreBounceDistanceId, shoreBounceDistance);
        material.SetFloat(ShoreBounceStrengthId, shoreBounceStrength);
        material.SetFloat(WaveTilingId, waveTiling);
        material.SetFloat(WaveDetailTilingId, waveDetailTiling);
        material.SetFloat(WaveRoughnessId, waveRoughness);
        material.SetFloat(WaveIrregularityId, waveIrregularity);
        material.SetVector(WaveSpeedAId, new Vector4(waveSpeedA.x, waveSpeedA.y, 0f, 0f));
        material.SetVector(WaveSpeedBId, new Vector4(waveSpeedB.x, waveSpeedB.y, 0f, 0f));
        material.SetVector(WaveDirectionId, new Vector4(waveDirection.x, waveDirection.y, 0f, 0f));
        material.SetFloat(WaveFrequencyId, waveFrequency);
        material.SetFloat(WaveSpeedId, waveSpeed);
        material.SetFloat(WaveContrastId, waveContrast);
        material.SetFloat(WaveBandSharpnessId, waveBandSharpness);
        material.SetFloat(WaveNormalStrengthId, waveNormalStrength);
        material.SetFloat(NormalStrengthId, normalStrength);
        material.SetFloat(SpecularIntensityId, specularIntensity);
        material.SetFloat(SpecularPowerId, specularPower);
        material.SetColor(HighlightColorId, highlightColor);
        material.SetFloat(HighlightStrengthId, highlightStrength);
        material.SetFloat(HighlightPowerId, highlightPower);
        material.SetColor(CrestColorId, crestColor);
        material.SetFloat(CrestStrengthId, crestStrength);
        material.SetFloat(CrestFoamAmountId, crestFoamAmount);
        material.SetFloat(CrestFoamThresholdId, crestFoamThreshold);
        material.SetFloat(ReflectionStrengthId, reflectionStrength);

        if (distanceMask != null)
            material.SetTexture(DistanceMaskId, distanceMask);

        if (noiseTexture != null)
            material.SetTexture(NoiseTexId, noiseTexture);
    }

    private void EnsureMaterialInstance()
    {
        if (waterRenderer == null)
            waterRenderer = GetComponent<Renderer>();

        if (waterRenderer == null)
            return;

        if (waterMaterialInstance != null)
        {
#if UNITY_EDITOR
            if (AssetDatabase.Contains(waterMaterialInstance))
                waterMaterialInstance = CreateMaterialInstance(waterMaterialInstance);
#endif

            if (waterRenderer.sharedMaterial != waterMaterialInstance)
                waterRenderer.sharedMaterial = waterMaterialInstance;
            return;
        }

        Material source = waterRenderer.sharedMaterial;
        if (source == null)
            return;

        waterMaterialInstance = CreateMaterialInstance(source);
        waterRenderer.sharedMaterial = waterMaterialInstance;
    }

    private static Material CreateMaterialInstance(Material source)
    {
        return new Material(source)
        {
            name = source.name + " Instance"
        };
    }

    private Material GetWritableMaterial()
    {
        if (waterMaterialInstance != null)
            return waterMaterialInstance;

        EnsureMaterialInstance();
        return waterMaterialInstance;
    }

    private void ResolveStateReferences()
    {
        if (autoFindViewManager && viewManager == null)
        {
#if UNITY_2023_1_OR_NEWER
            viewManager = FindFirstObjectByType<ViewManager>();
#else
            viewManager = FindObjectOfType<ViewManager>();
#endif
        }

        if (autoFindDistrictFocusController && districtFocusController == null)
        {
#if UNITY_2023_1_OR_NEWER
            districtFocusController = FindFirstObjectByType<MapDistrictFocusController>();
#else
            districtFocusController = FindObjectOfType<MapDistrictFocusController>();
#endif
        }
    }

    private void ApplyMapLayer()
    {
        if (!matchMapLayer)
            return;

        int targetLayer = -1;
        if (maskReferenceRenderer != null)
            targetLayer = maskReferenceRenderer.gameObject.layer;

        if (targetLayer < 0 && !string.IsNullOrWhiteSpace(fallbackMapLayerName))
            targetLayer = LayerMask.NameToLayer(fallbackMapLayerName);

        if (targetLayer < 0 || gameObject.layer == targetLayer)
            return;

        gameObject.layer = targetLayer;
    }

    private float GetDistrictFocusStrength()
    {
        if (!dimWithDistrictFocus || districtFocusController == null || !districtFocusController.isActiveAndEnabled)
            return 0f;

        return districtFocusController.CurrentFocusStrength;
    }

    private float GetFocusBrightness()
    {
        if (useDistrictFocusLook && districtFocusController != null)
            return districtFocusController.OutsideBrightness;

        return focusBrightness;
    }

    private float GetFocusSaturation()
    {
        if (useDistrictFocusLook && districtFocusController != null)
            return districtFocusController.OutsideSaturation;

        return focusSaturation;
    }

    private void RefreshVisibility()
    {
        if (waterRenderer == null)
            waterRenderer = GetComponent<Renderer>();

        if (waterRenderer == null)
            return;

        waterRenderer.enabled = ShouldRenderForCurrentView();
    }

    private bool ShouldRenderForCurrentView()
    {
        if (!hideInBaseViews)
            return true;

        if (viewManager == null)
            return visibleWhenViewManagerMissing;

        return viewManager.CurrentState != ViewManager.ViewState.Top &&
               viewManager.CurrentState != ViewManager.ViewState.Base;
    }

    private Vector4 GetMaskWorldBoundsVector()
    {
        Renderer boundsRenderer = distanceMaskMapping == DistanceMaskMapping.ReferenceRendererBounds && maskReferenceRenderer != null
            ? maskReferenceRenderer
            : waterRenderer;

        if (boundsRenderer == null)
            return new Vector4(0f, 0f, 1f, 1f);

        Bounds bounds = boundsRenderer.bounds;
        float width = Mathf.Max(bounds.size.x, 0.0001f);
        float height = Mathf.Max(bounds.size.y, 0.0001f);
        return new Vector4(bounds.min.x, bounds.min.y, width, height);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawAlignmentGizmo)
            return;

        if (waterRenderer == null)
            waterRenderer = GetComponent<Renderer>();

        if (waterRenderer == null)
            return;

        Bounds bounds = waterRenderer.bounds;
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(bounds.center, bounds.size);

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3 center = bounds.center;
        Gizmos.DrawLine(new Vector3(min.x, center.y, center.z), new Vector3(max.x, center.y, center.z));
        Gizmos.DrawLine(new Vector3(center.x, min.y, center.z), new Vector3(center.x, max.y, center.z));
    }
}
