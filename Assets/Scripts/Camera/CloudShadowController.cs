using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Drives a reusable, low-cost cloud shadow overlay for the 3D base views.
///
/// Setup:
/// 1. Create a material that uses the "Custom/CloudShadow" shader.
/// 2. Assign a soft, tileable grayscale or alpha cloud texture with Wrap Mode set to Repeat.
/// 3. Put a large plane or quad slightly above the base, assign the material, and attach this component.
/// 4. Assign the scene ViewManager so the shadow is visible only in Top/Base views and hidden on the 2D PNG map.
///
/// For Decal Projectors, assign the same material to the DecalProjector and this component will drive the
/// material properties and projector size. Mesh renderers use MaterialPropertyBlock by default to avoid
/// per-instance material allocations.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Camera/Cloud Shadow Controller")]
public class CloudShadowController : MonoBehaviour
{
    private enum FootprintScaleMode
    {
        DoNotScale,
        XZPlane,
        XYQuad,
        DecalProjector
    }

    [Header("View State")]
    [SerializeField] private ViewManager viewManager;
    [SerializeField] private bool autoFindViewManager = true;
    [SerializeField] private bool onlyShowInBaseViews = true;
    [SerializeField] private bool visibleWhenViewManagerMissing = true;

    [Header("Targets")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private DecalProjector targetDecalProjector;
    [SerializeField] private Material targetMaterial;
    [SerializeField] private bool toggleTargetRenderer = true;

    [Header("Shadow")]
    [SerializeField] private Texture2D cloudShadowTexture;
    [SerializeField, Range(0f, 1f)] private float opacity = 0.22f;
    [SerializeField] private Color shadowColor = new Color(0.52f, 0.56f, 0.58f, 1f);

    [Header("Motion")]
    [SerializeField, Min(0f)] private float scrollSpeed = 0.025f;
    [SerializeField] private Vector2 scrollDirection = new Vector2(1f, 0.35f);

    [Header("Mapping")]
    [SerializeField] private Vector2 tiling = new Vector2(2f, 2f);
    [SerializeField] private Vector2 uvOffset;
    [SerializeField] private FootprintScaleMode footprintScaleMode = FootprintScaleMode.XZPlane;
    [SerializeField] private Vector2 footprintScale = new Vector2(140f, 140f);

    private static readonly int MainTexID = Shader.PropertyToID("_MainTex");
    private static readonly int OpacityID = Shader.PropertyToID("_Opacity");
    private static readonly int ShadowColorID = Shader.PropertyToID("_ShadowColor");
    private static readonly int ScrollDirectionID = Shader.PropertyToID("_ScrollDirection");
    private static readonly int ScrollSpeedID = Shader.PropertyToID("_ScrollSpeed");
    private static readonly int TilingID = Shader.PropertyToID("_Tiling");
    private static readonly int UVOffsetID = Shader.PropertyToID("_UVOffset");

    private MaterialPropertyBlock propertyBlock;
    private Material runtimeDecalMaterial;
    private bool warnedMissingTarget;

    private void Reset()
    {
        targetRenderer = GetComponent<Renderer>();
        targetDecalProjector = GetComponent<DecalProjector>();
        TryFindViewManager();
    }

    private void Awake()
    {
        CacheTargets();
        EnsureDecalMaterialInstance();
        ApplyFootprintScale();
        ApplySettings();
        RefreshVisibility();
    }

    private void OnEnable()
    {
        CacheTargets();
        EnsureDecalMaterialInstance();
        ApplySettings();
        RefreshVisibility();
    }

    private void Update()
    {
        if (autoFindViewManager && viewManager == null)
            TryFindViewManager();

        ApplyFootprintScale();
        ApplySettings();
        RefreshVisibility();
    }

    private void OnValidate()
    {
        opacity = Mathf.Clamp01(opacity);
        scrollSpeed = Mathf.Max(0f, scrollSpeed);
        tiling.x = Mathf.Max(0.001f, tiling.x);
        tiling.y = Mathf.Max(0.001f, tiling.y);
        footprintScale.x = Mathf.Max(0.001f, footprintScale.x);
        footprintScale.y = Mathf.Max(0.001f, footprintScale.y);

        CacheTargets();
        ApplyFootprintScale();
        ApplySettings();
        RefreshVisibility();
    }

    private void OnDisable()
    {
        SetTargetsVisible(false);
    }

    private void OnDestroy()
    {
        if (runtimeDecalMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(runtimeDecalMaterial);
        else
            DestroyImmediate(runtimeDecalMaterial);
    }

    [ContextMenu("Log Setup Instructions")]
    private void LogSetupInstructions()
    {
        Debug.Log(
            "CloudShadowController setup:\n" +
            "1. Create a material with shader Custom/CloudShadow.\n" +
            "2. Assign a tileable grayscale/alpha cloud texture and set the texture Wrap Mode to Repeat.\n" +
            "3. Add the material to a large plane/quad above the base, or to a URP DecalProjector.\n" +
            "4. Assign ViewManager so the shadow is hidden in the 2D PNG map view and visible in Top/Base views.",
            this);
    }

    private void CacheTargets()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        if (targetDecalProjector == null)
            targetDecalProjector = GetComponent<DecalProjector>();

        if (autoFindViewManager && viewManager == null)
            TryFindViewManager();
    }

    private void TryFindViewManager()
    {
#if UNITY_2023_1_OR_NEWER
        viewManager = FindFirstObjectByType<ViewManager>();
#else
        viewManager = FindObjectOfType<ViewManager>();
#endif
    }

    private void EnsureDecalMaterialInstance()
    {
        if (!Application.isPlaying || targetDecalProjector == null || runtimeDecalMaterial != null)
            return;

        Material source = targetMaterial != null ? targetMaterial : targetDecalProjector.material;
        if (source == null)
            return;

        runtimeDecalMaterial = new Material(source)
        {
            name = source.name + " Runtime Cloud Shadow",
            hideFlags = HideFlags.HideAndDontSave
        };

        targetDecalProjector.material = runtimeDecalMaterial;
    }

    private void ApplySettings()
    {
        bool activeForView = ShouldRenderForCurrentView();
        float activeOpacity = activeForView ? opacity : 0f;

        if (targetRenderer != null)
        {
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();

            targetRenderer.GetPropertyBlock(propertyBlock);
            ApplySettings(propertyBlock, activeOpacity);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        Material material = GetWritableMaterial();
        if (material != null)
            ApplySettings(material, activeOpacity);

        if (targetRenderer == null && targetDecalProjector == null && targetMaterial == null && !warnedMissingTarget)
        {
            warnedMissingTarget = true;
            Debug.LogWarning("CloudShadowController: Assign a Renderer, DecalProjector, or target Material.", this);
        }
    }

    private void ApplySettings(MaterialPropertyBlock block, float activeOpacity)
    {
        if (cloudShadowTexture != null)
            block.SetTexture(MainTexID, cloudShadowTexture);

        block.SetFloat(OpacityID, activeOpacity);
        block.SetColor(ShadowColorID, shadowColor);
        block.SetVector(ScrollDirectionID, GetNormalizedDirection());
        block.SetFloat(ScrollSpeedID, scrollSpeed);
        block.SetVector(TilingID, new Vector4(tiling.x, tiling.y, 0f, 0f));
        block.SetVector(UVOffsetID, new Vector4(uvOffset.x, uvOffset.y, 0f, 0f));
    }

    private void ApplySettings(Material material, float activeOpacity)
    {
        if (cloudShadowTexture != null)
            material.SetTexture(MainTexID, cloudShadowTexture);

        material.SetFloat(OpacityID, activeOpacity);
        material.SetColor(ShadowColorID, shadowColor);
        material.SetVector(ScrollDirectionID, GetNormalizedDirection());
        material.SetFloat(ScrollSpeedID, scrollSpeed);
        material.SetVector(TilingID, new Vector4(tiling.x, tiling.y, 0f, 0f));
        material.SetVector(UVOffsetID, new Vector4(uvOffset.x, uvOffset.y, 0f, 0f));
    }

    private Material GetWritableMaterial()
    {
        if (runtimeDecalMaterial != null)
            return runtimeDecalMaterial;

        if (targetMaterial != null)
            return targetMaterial;

        if (targetDecalProjector != null)
            return targetDecalProjector.material;

        return null;
    }

    private Vector4 GetNormalizedDirection()
    {
        Vector2 direction = scrollDirection.sqrMagnitude > 0.0001f
            ? scrollDirection.normalized
            : Vector2.right;

        return new Vector4(direction.x, direction.y, 0f, 0f);
    }

    private void ApplyFootprintScale()
    {
        switch (footprintScaleMode)
        {
            case FootprintScaleMode.XZPlane:
            {
                Vector3 scale = transform.localScale;
                scale.x = footprintScale.x;
                scale.z = footprintScale.y;
                transform.localScale = scale;
                break;
            }
            case FootprintScaleMode.XYQuad:
            {
                Vector3 scale = transform.localScale;
                scale.x = footprintScale.x;
                scale.y = footprintScale.y;
                transform.localScale = scale;
                break;
            }
            case FootprintScaleMode.DecalProjector:
            {
                if (targetDecalProjector == null)
                    break;

                Vector3 size = targetDecalProjector.size;
                size.x = footprintScale.x;
                size.y = footprintScale.y;
                targetDecalProjector.size = size;
                break;
            }
        }
    }

    private void RefreshVisibility()
    {
        SetTargetsVisible(ShouldRenderForCurrentView() && opacity > 0.001f);
    }

    private void SetTargetsVisible(bool visible)
    {
        if (targetRenderer != null && toggleTargetRenderer)
            targetRenderer.enabled = visible;

        if (targetDecalProjector != null)
            targetDecalProjector.enabled = visible;
    }

    private bool ShouldRenderForCurrentView()
    {
        if (!onlyShowInBaseViews)
            return true;

        if (viewManager == null)
            return visibleWhenViewManagerMissing;

        return viewManager.CurrentState == ViewManager.ViewState.Top ||
               viewManager.CurrentState == ViewManager.ViewState.Base;
    }
}
