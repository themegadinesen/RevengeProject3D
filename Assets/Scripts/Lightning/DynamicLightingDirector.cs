using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[AddComponentMenu("Lighting/Dynamic Lighting Director")]
public class DynamicLightingDirector : MonoBehaviour
{
    private static readonly int MapLightDirectionId = Shader.PropertyToID("_MapLightDirection");
    private static readonly int MapLightColorId = Shader.PropertyToID("_MapLightColor");
    private static readonly int MapLightIntensityId = Shader.PropertyToID("_MapLightIntensity");
    private static readonly int MapAmbientStrengthId = Shader.PropertyToID("_MapAmbientStrength");
    private static readonly int MapSpecularIntensityScaleId = Shader.PropertyToID("_MapSpecularIntensityScale");
    private static readonly int MapLightingBlendId = Shader.PropertyToID("_MapLightingBlend");
    private static readonly int MapLightTintStrengthId = Shader.PropertyToID("_MapLightTintStrength");
    private static readonly int ShallowColorId = Shader.PropertyToID("_ShallowColor");
    private static readonly int DeepColorId = Shader.PropertyToID("_DeepColor");
    private static readonly int FoamColorId = Shader.PropertyToID("_FoamColor");
    private static readonly int HighlightColorId = Shader.PropertyToID("_HighlightColor");
    private static readonly int MapWaterLightTintStrengthId = Shader.PropertyToID("_MapWaterLightTintStrength");


    [Header("References")]
    [SerializeField] private GameCalendar gameCalendar;
    [SerializeField] private ViewManager viewManager;
    [SerializeField] private Light baseDirectionalLight;
    [SerializeField] private Light[] baseAccentLights;
    [SerializeField] private Renderer[] mapRenderers;
    [SerializeField] private Material[] mapMaterials;
    [SerializeField] private Volume basePostProcessVolume;

    [Header("Time")]
    [SerializeField] private bool updateContinuously = true;
    [SerializeField, Range(-1f, 1f)] private float calendarTimeOffset;

    [Header("Sun Direction")]
    [SerializeField] private AnimationCurve sunElevationDegreesByTime = new(
        new Keyframe(0f, -35f),
        new Keyframe(0.23f, -6f),
        new Keyframe(0.34f, 28f),
        new Keyframe(0.5f, 68f),
        new Keyframe(0.66f, 28f),
        new Keyframe(0.77f, -6f),
        new Keyframe(1f, -35f)
    );
    [SerializeField] private AnimationCurve sunAzimuthDegreesByTime = AnimationCurve.Linear(0f, 70f, 1f, 290f);

    [Header("Base Output")]
    [SerializeField] private Gradient baseLightColorByTime = CreateDefaultSunGradient();
    [SerializeField] private AnimationCurve baseDirectionalIntensityByTime = new(
        new Keyframe(0f, 0.05f),
        new Keyframe(0.25f, 0.25f),
        new Keyframe(0.5f, 1.35f),
        new Keyframe(0.75f, 0.35f),
        new Keyframe(1f, 0.05f)
    );
    [SerializeField] private Gradient baseAmbientColorByTime = CreateDefaultAmbientGradient();
    [SerializeField] private AnimationCurve baseAmbientIntensityByTime = new(
        new Keyframe(0f, 0.18f),
        new Keyframe(0.5f, 0.9f),
        new Keyframe(1f, 0.18f)
    );
    [SerializeField] private AnimationCurve baseAccentIntensityScaleByTime = new(
        new Keyframe(0f, 1f),
        new Keyframe(0.5f, 0f),
        new Keyframe(1f, 1f)
    );
    [SerializeField] private bool driveFog;
    [SerializeField] private Gradient fogColorByTime = CreateDefaultAmbientGradient();
    [SerializeField] private AnimationCurve fogDensityByTime = new(
        new Keyframe(0f, 0.018f),
        new Keyframe(0.5f, 0.004f),
        new Keyframe(1f, 0.018f)
    );
    [SerializeField] private bool drivePostExposure;
    [SerializeField] private AnimationCurve postExposureByTime = new(
        new Keyframe(0f, -0.35f),
        new Keyframe(0.5f, 0.1f),
        new Keyframe(1f, -0.35f)
    );

    [Header("Map Water Night Color")]
    [SerializeField] private bool driveMapWaterColors = true;
    [SerializeField] private Renderer[] mapWaterRenderers;

    [SerializeField] private Gradient waterShallowColorByTime = CreateDefaultWaterShallowGradient();
    [SerializeField] private Gradient waterDeepColorByTime = CreateDefaultWaterDeepGradient();
    [SerializeField] private Gradient waterFoamColorByTime = CreateDefaultWaterFoamGradient();
    [SerializeField] private Gradient waterHighlightColorByTime = CreateDefaultWaterHighlightGradient();
    [SerializeField] private AnimationCurve mapWaterLightTintStrengthByTime = new(
        new Keyframe(0f, 0.22f),
        new Keyframe(0.25f, 0.28f),
        new Keyframe(0.5f, 0.12f),
        new Keyframe(0.75f, 0.28f),
        new Keyframe(1f, 0.22f)
    );


    [Header("Map Output")]
    [SerializeField] private Gradient mapLightColorByTime = CreateDefaultMapGradient();
    [SerializeField] private AnimationCurve mapLightIntensityByTime = new(
        new Keyframe(0f, 0.38f),
        new Keyframe(0.25f, 0.5f),
        new Keyframe(0.5f, 0.82f),
        new Keyframe(0.75f, 0.52f),
        new Keyframe(1f, 0.38f)
    );
    [SerializeField] private AnimationCurve mapAmbientStrengthByTime = new(
        new Keyframe(0f, 0.5f),
        new Keyframe(0.5f, 0.42f),
        new Keyframe(1f, 0.5f)
    );
    [SerializeField] private AnimationCurve mapSpecularScaleByTime = new(
        new Keyframe(0f, 0.45f),
        new Keyframe(0.5f, 1f),
        new Keyframe(1f, 0.45f)
    );
    [SerializeField] private AnimationCurve mapLightingBlendByTime = AnimationCurve.Constant(0f, 1f, 1f);
    [SerializeField] private AnimationCurve mapLightTintStrengthByTime = new(
        new Keyframe(0f, 0.16f),
        new Keyframe(0.25f, 0.2f),
        new Keyframe(0.5f, 0.08f),
        new Keyframe(0.75f, 0.2f),
        new Keyframe(1f, 0.16f)
    );
    [SerializeField] private AnimationCurve mapNightDesaturationByTime = new(
        new Keyframe(0f, 0.35f),
        new Keyframe(0.5f, 0f),
        new Keyframe(1f, 0.35f)
    );

    private MaterialPropertyBlock propertyBlock;
    private float[] baseAccentInitialIntensities;
    private bool manualTimeActive;
    private float manualNormalizedDayTime;

    private void Reset()
    {
        gameCalendar = FindFirstObjectByType<GameCalendar>();
        viewManager = FindFirstObjectByType<ViewManager>();
        baseDirectionalLight = RenderSettings.sun;
    }

    private void Awake()
    {
        CacheBaseAccentIntensities();
    }

    private void OnEnable()
    {
        CacheBaseAccentIntensities();

        if (gameCalendar != null)
            gameCalendar.OnCalendarChanged += OnCalendarChanged;

        ApplyLightingImmediate();
    }

    private void OnDisable()
    {
        if (gameCalendar != null)
            gameCalendar.OnCalendarChanged -= OnCalendarChanged;
    }

    private void LateUpdate()
    {
        if (updateContinuously)
            ApplyLightingImmediate();
    }

    public void SetLightingTime(float normalizedDayTime)
    {
        manualTimeActive = true;
        manualNormalizedDayTime = Mathf.Repeat(normalizedDayTime, 1f);
        ApplyLightingImmediate();
    }

    public void ApplyLightingImmediate()
    {
        float t = manualTimeActive ? manualNormalizedDayTime : GetCalendarNormalizedTime();

        Vector3 directionToLight = EvaluateDirectionToLight(t);
        Color baseLightColor = baseLightColorByTime.Evaluate(t);
        Color baseAmbientColor = baseAmbientColorByTime.Evaluate(t);

        ApplyBaseLighting(t, directionToLight, baseLightColor, baseAmbientColor);
        ApplyMapLighting(t, directionToLight);
    }

    public void UseCalendarTime()
    {
        manualTimeActive = false;
        ApplyLightingImmediate();
    }

    private void OnCalendarChanged(GameCalendar calendar)
    {
        if (!updateContinuously)
            ApplyLightingImmediate();
    }

    private float GetCalendarNormalizedTime()
    {
        if (gameCalendar == null)
            return Mathf.Repeat(calendarTimeOffset, 1f);

        float hourOfDay = Mathf.Repeat(gameCalendar.ElapsedHours, 24f);
        return Mathf.Repeat(hourOfDay / 24f + calendarTimeOffset, 1f);
    }

    private Vector3 EvaluateDirectionToLight(float normalizedTime)
    {
        float elevation = sunElevationDegreesByTime.Evaluate(normalizedTime) * Mathf.Deg2Rad;
        float azimuth = sunAzimuthDegreesByTime.Evaluate(normalizedTime) * Mathf.Deg2Rad;

        return new Vector3(
            Mathf.Cos(elevation) * Mathf.Sin(azimuth),
            Mathf.Sin(elevation),
            Mathf.Cos(elevation) * Mathf.Cos(azimuth)
        ).normalized;
    }

    private void ApplyBaseLighting(float t, Vector3 directionToLight, Color lightColor, Color ambientColor)
    {
        if (baseDirectionalLight != null)
        {
            baseDirectionalLight.transform.rotation = Quaternion.LookRotation(-directionToLight, Vector3.up);
            baseDirectionalLight.color = lightColor;
            baseDirectionalLight.intensity = Mathf.Max(0f, baseDirectionalIntensityByTime.Evaluate(t));
        }

        RenderSettings.sun = baseDirectionalLight;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
        RenderSettings.ambientIntensity = Mathf.Max(0f, baseAmbientIntensityByTime.Evaluate(t));

        ApplyBaseAccentLights(t);

        if (driveFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogColor = fogColorByTime.Evaluate(t);
            RenderSettings.fogDensity = Mathf.Max(0f, fogDensityByTime.Evaluate(t));
        }

        if (drivePostExposure && basePostProcessVolume != null && basePostProcessVolume.profile != null &&
            basePostProcessVolume.profile.TryGet(out ColorAdjustments colorAdjustments))
        {
            colorAdjustments.postExposure.Override(postExposureByTime.Evaluate(t));
        }
    }

    private void ApplyBaseAccentLights(float t)
    {
        if (baseAccentLights == null || baseAccentLights.Length == 0)
            return;

        CacheBaseAccentIntensities();

        float scale = Mathf.Max(0f, baseAccentIntensityScaleByTime.Evaluate(t));

        for (int i = 0; i < baseAccentLights.Length; i++)
        {
            if (baseAccentLights[i] == null)
                continue;

            float initialIntensity = i < baseAccentInitialIntensities.Length
                ? baseAccentInitialIntensities[i]
                : baseAccentLights[i].intensity;

            baseAccentLights[i].intensity = initialIntensity * scale;
        }
    }

    private void ApplyMapLighting(float t, Vector3 directionToLight)
    {
        Color mapLightColor = Desaturate(mapLightColorByTime.Evaluate(t), mapNightDesaturationByTime.Evaluate(t));
        float mapIntensity = Mathf.Max(0f, mapLightIntensityByTime.Evaluate(t));
        float mapAmbient = Mathf.Clamp01(mapAmbientStrengthByTime.Evaluate(t));
        float specularScale = Mathf.Max(0f, mapSpecularScaleByTime.Evaluate(t));
        float lightingBlend = Mathf.Clamp01(mapLightingBlendByTime.Evaluate(t));
        float tintStrength = Mathf.Clamp01(mapLightTintStrengthByTime.Evaluate(t));
        Vector3 mapDirectionToLight = GetMapDirectionToLight(directionToLight);
        Vector4 direction = new(mapDirectionToLight.x, mapDirectionToLight.y, mapDirectionToLight.z, 0f);

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        if (mapRenderers != null)
        {
            for (int i = 0; i < mapRenderers.Length; i++)
            {
                Renderer target = mapRenderers[i];
                if (target == null)
                    continue;

                target.GetPropertyBlock(propertyBlock);
                WriteMapLighting(propertyBlock, direction, mapLightColor, mapIntensity, mapAmbient, specularScale, lightingBlend, tintStrength);
                target.SetPropertyBlock(propertyBlock);
            }
        }

        if (mapMaterials != null)
        {
            for (int i = 0; i < mapMaterials.Length; i++)
            {
                Material target = mapMaterials[i];
                if (target == null)
                    continue;

                WriteMapLighting(target, direction, mapLightColor, mapIntensity, mapAmbient, specularScale, lightingBlend, tintStrength);
            }
        }
        if (driveMapWaterColors)
        {
            ApplyMapWaterColors(t);
        }
    }

    private void ApplyMapWaterColors(float t)
    {
        if (mapWaterRenderers == null || mapWaterRenderers.Length == 0)
            return;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        Color shallow = waterShallowColorByTime.Evaluate(t);
        Color deep = waterDeepColorByTime.Evaluate(t);
        Color foam = waterFoamColorByTime.Evaluate(t);
        Color highlight = waterHighlightColorByTime.Evaluate(t);
        float waterTintStrength = Mathf.Clamp01(mapWaterLightTintStrengthByTime.Evaluate(t));

        for (int i = 0; i < mapWaterRenderers.Length; i++)
        {
            Renderer target = mapWaterRenderers[i];
            if (target == null)
                continue;

            target.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(ShallowColorId, shallow);
            propertyBlock.SetColor(DeepColorId, deep);
            propertyBlock.SetColor(FoamColorId, foam);
            propertyBlock.SetColor(HighlightColorId, highlight);
            propertyBlock.SetFloat(MapWaterLightTintStrengthId, waterTintStrength);
            target.SetPropertyBlock(propertyBlock);
        }
    }

    private static Vector3 GetMapDirectionToLight(Vector3 worldDirectionToLight)
    {
        Vector3 mapDirection = new(
            worldDirectionToLight.x,
            worldDirectionToLight.z,
            Mathf.Max(Mathf.Abs(worldDirectionToLight.y), 0.08f));

        return mapDirection.sqrMagnitude > 0.0001f
            ? mapDirection.normalized
            : new Vector3(-0.45f, 0.35f, 0.8f).normalized;
    }


    private static void WriteMapLighting(MaterialPropertyBlock block, Vector4 direction, Color color, float intensity, float ambient, float specularScale, float blend, float tintStrength)
    {
        block.SetVector(MapLightDirectionId, direction);
        block.SetColor(MapLightColorId, color);
        block.SetFloat(MapLightIntensityId, intensity);
        block.SetFloat(MapAmbientStrengthId, ambient);
        block.SetFloat(MapSpecularIntensityScaleId, specularScale);
        block.SetFloat(MapLightingBlendId, blend);
        block.SetFloat(MapLightTintStrengthId, tintStrength);
    }

    private static void WriteMapLighting(Material material, Vector4 direction, Color color, float intensity, float ambient, float specularScale, float blend, float tintStrength)
    {
        material.SetVector(MapLightDirectionId, direction);
        material.SetColor(MapLightColorId, color);
        material.SetFloat(MapLightIntensityId, intensity);
        material.SetFloat(MapAmbientStrengthId, ambient);
        material.SetFloat(MapSpecularIntensityScaleId, specularScale);
        material.SetFloat(MapLightingBlendId, blend);
        material.SetFloat(MapLightTintStrengthId, tintStrength);
    }

    private void CacheBaseAccentIntensities()
    {
        if (baseAccentLights == null)
        {
            baseAccentInitialIntensities = System.Array.Empty<float>();
            return;
        }

        if (baseAccentInitialIntensities != null && baseAccentInitialIntensities.Length == baseAccentLights.Length)
            return;

        baseAccentInitialIntensities = new float[baseAccentLights.Length];

        for (int i = 0; i < baseAccentLights.Length; i++)
            baseAccentInitialIntensities[i] = baseAccentLights[i] != null ? baseAccentLights[i].intensity : 0f;
    }

    private static Color Desaturate(Color color, float amount)
    {
        amount = Mathf.Clamp01(amount);
        float luminance = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
        color.r = Mathf.Lerp(color.r, luminance, amount);
        color.g = Mathf.Lerp(color.g, luminance, amount);
        color.b = Mathf.Lerp(color.b, luminance, amount);
        return color;
    }

    private static Gradient CreateDefaultSunGradient()
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.32f, 0.42f, 0.75f), 0f),
                new GradientColorKey(new Color(1f, 0.62f, 0.34f), 0.25f),
                new GradientColorKey(new Color(1f, 0.95f, 0.82f), 0.5f),
                new GradientColorKey(new Color(1f, 0.48f, 0.28f), 0.75f),
                new GradientColorKey(new Color(0.32f, 0.42f, 0.75f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return gradient;
    }

    private static Gradient CreateDefaultAmbientGradient()
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.08f, 0.1f, 0.18f), 0f),
                new GradientColorKey(new Color(0.42f, 0.33f, 0.28f), 0.25f),
                new GradientColorKey(new Color(0.62f, 0.68f, 0.75f), 0.5f),
                new GradientColorKey(new Color(0.38f, 0.27f, 0.32f), 0.75f),
                new GradientColorKey(new Color(0.08f, 0.1f, 0.18f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return gradient;
    }

    private static Gradient CreateDefaultMapGradient()
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.46f, 0.56f, 0.8f), 0f),
                new GradientColorKey(new Color(1f, 0.7f, 0.48f), 0.25f),
                new GradientColorKey(new Color(0.9f, 0.98f, 1f), 0.5f),
                new GradientColorKey(new Color(1f, 0.6f, 0.42f), 0.75f),
                new GradientColorKey(new Color(0.46f, 0.56f, 0.8f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return gradient;
    }

    private static Gradient CreateDefaultWaterShallowGradient()
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.30f, 0.48f, 0.55f), 0f),
                new GradientColorKey(new Color(0.48f, 0.76f, 0.78f), 0.25f),
                new GradientColorKey(new Color(0.55f, 0.90f, 0.96f), 0.5f),
                new GradientColorKey(new Color(0.44f, 0.68f, 0.72f), 0.75f),
                new GradientColorKey(new Color(0.30f, 0.48f, 0.55f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return gradient;
    }

    private static Gradient CreateDefaultWaterDeepGradient()
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.06f, 0.13f, 0.22f), 0f),
                new GradientColorKey(new Color(0.08f, 0.24f, 0.36f), 0.25f),
                new GradientColorKey(new Color(0.08f, 0.32f, 0.58f), 0.5f),
                new GradientColorKey(new Color(0.08f, 0.22f, 0.34f), 0.75f),
                new GradientColorKey(new Color(0.06f, 0.13f, 0.22f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return gradient;
    }

    private static Gradient CreateDefaultWaterFoamGradient()
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.48f, 0.62f, 0.68f), 0f),
                new GradientColorKey(new Color(0.78f, 0.88f, 0.86f), 0.25f),
                new GradientColorKey(new Color(0.88f, 1f, 1f), 0.5f),
                new GradientColorKey(new Color(0.74f, 0.82f, 0.84f), 0.75f),
                new GradientColorKey(new Color(0.48f, 0.62f, 0.68f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return gradient;
    }

    private static Gradient CreateDefaultWaterHighlightGradient()
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.42f, 0.56f, 0.72f), 0f),
                new GradientColorKey(new Color(0.78f, 0.88f, 0.92f), 0.25f),
                new GradientColorKey(new Color(0.82f, 1f, 1f), 0.5f),
                new GradientColorKey(new Color(0.72f, 0.82f, 0.88f), 0.75f),
                new GradientColorKey(new Color(0.42f, 0.56f, 0.72f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return gradient;
    }

}
