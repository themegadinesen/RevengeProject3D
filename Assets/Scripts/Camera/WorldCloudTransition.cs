using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime-only cloud layer and flythrough effects used by the Map <-> Top camera handoff.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Camera/World Cloud Transition")]
public class WorldCloudTransition : MonoBehaviour
{
    [Header("Map Cloud Cover")]
    [SerializeField] private Shader mapCloudShader;
    [SerializeField, Min(0.01f)] private float mapCloudDistance = 8f;
    [SerializeField, Min(1f)] private float mapCloudCoveragePadding = 1.35f;
    [SerializeField, Range(0f, 1f)] private float mapCloudOpacity = 0.5f;
    [SerializeField, Range(0f, 1f)] private float mapCloudCoverage = 0.64f;
    [SerializeField, Range(0.01f, 0.5f)] private float mapCloudSoftness = 0.26f;
    [SerializeField, Range(0.2f, 12f)] private float mapCloudNoiseScale = 2.8f;
    [SerializeField, Range(0.5f, 8f)] private float mapCloudDetailScale = 3.2f;
    [SerializeField] private Vector2 mapCloudDrift = new Vector2(0.026f, 0.011f);

    [Header("Flythrough Cloud Volume")]
    [SerializeField] private Shader cloudVolumeShader;
    [SerializeField, Min(1f)] private float volumeSize = 180f;
    [SerializeField, Range(0.1f, 6f)] private float density = 2.05f;
    [SerializeField, Range(1f, 16f)] private float noiseScale = 7.5f;
    [SerializeField, Range(0.1f, 10f)] private float diveSpeed = 4.2f;

    [Header("Color")]
    [SerializeField] private Color cloudHighlight = new Color(0.92f, 0.96f, 0.95f, 1f);
    [SerializeField] private Color cloudLowlight = new Color(0.62f, 0.7f, 0.74f, 1f);

    [Header("Plane Flyby")]
    [SerializeField] private GameObject planePrefab;
    [SerializeField, Range(0f, 1f)] private float planeTriggerProgress = 0.32f;
    [SerializeField, Min(0.05f)] private float planeDuration = 1.15f;
    [SerializeField, Min(0.1f)] private float planeDistance = 16f;
    [SerializeField] private Vector2 planeStartViewport = new Vector2(-0.2f, 0.62f);
    [SerializeField] private Vector2 planeEndViewport = new Vector2(1.2f, 0.46f);
    [SerializeField, Min(0.01f)] private float planeScaleMultiplier = 1f;
    [SerializeField] private Vector3 planeRotationOffsetEuler = Vector3.zero;

    [Header("Bird Flocks")]
    [SerializeField] private GameObject[] birdFlockPrefabs;
    [SerializeField, Range(0f, 1f)] private float birdTriggerProgress = 0.68f;
    [SerializeField] private bool birdRequiresBaseView = true;
    [SerializeField, Min(1)] private int birdFlockCount = 2;
    [SerializeField, Min(0.05f)] private float birdDuration = 4f;
    [SerializeField, Min(0.1f)] private float birdDistance = 22f;
    [SerializeField] private float birdHeightOffset;
    [SerializeField] private Vector2 birdStartViewport = new Vector2(1.15f, 0.28f);
    [SerializeField] private Vector2 birdEndViewport = new Vector2(-0.15f, 0.38f);
    [SerializeField, Min(0f)] private float birdViewportScatter = 0.12f;
    [SerializeField, Min(0.01f)] private float birdScaleMultiplier = 1f;
    [SerializeField] private Vector3 birdRotationOffsetEuler = Vector3.zero;
    [SerializeField] private bool birdUseWorldSpace = true;
    [SerializeField] private bool birdAllowLeftToRight = true;
    [SerializeField, Range(0f, 1f)] private float birdLeftToRightChance = 0.5f;
    [SerializeField] private bool spawnPeriodicBirdsInBaseView = true;
    [SerializeField] private Vector2 periodicBirdInterval = new Vector2(18f, 35f);
    [SerializeField] private bool playFlybysWhenReturningToMap;

    [Header("Debug")]
    [SerializeField] private bool showRuntimeObjectsInHierarchy;

    private static readonly int ProgressID = Shader.PropertyToID("_TransitionProgress");
    private static readonly int StrengthID = Shader.PropertyToID("_TransitionStrength");
    private static readonly int DirectionID = Shader.PropertyToID("_TransitionDirection");
    private static readonly int DensityID = Shader.PropertyToID("_CloudDensity");
    private static readonly int NoiseScaleID = Shader.PropertyToID("_NoiseScale");
    private static readonly int DiveSpeedID = Shader.PropertyToID("_DiveSpeed");
    private static readonly int HighlightID = Shader.PropertyToID("_CloudHighlight");
    private static readonly int LowlightID = Shader.PropertyToID("_CloudLowlight");
    private static readonly int MapOpacityID = Shader.PropertyToID("_Opacity");
    private static readonly int MapCoverageID = Shader.PropertyToID("_Coverage");
    private static readonly int MapSoftnessID = Shader.PropertyToID("_Softness");
    private static readonly int MapNoiseScaleID = Shader.PropertyToID("_NoiseScale");
    private static readonly int MapDetailScaleID = Shader.PropertyToID("_DetailScale");
    private static readonly int MapDriftID = Shader.PropertyToID("_Drift");
    private static readonly int MapHighlightID = Shader.PropertyToID("_CloudTint");
    private static readonly int MapLowlightID = Shader.PropertyToID("_ShadowTint");

    private Camera targetCamera;
    private GameObject mapCloudObject;
    private Material mapCloudMaterial;
    private GameObject volumeObject;
    private Material volumeMaterial;
    private MeshRenderer volumeRenderer;
    private readonly List<ActiveFlyby> activeFlybys = new List<ActiveFlyby>();
    private bool mapCloudsRequestedVisible;
    private bool transitionActive;
    private bool planePlayed;
    private bool birdsPlayed;
    private bool baseViewReadyForBirds;
    private float nextPeriodicBirdSpawnTime = float.PositiveInfinity;

    private class ActiveFlyby
    {
        public Transform Transform;
        public Vector3 StartPosition;
        public Vector3 EndPosition;
        public bool UseLocalSpace;
        public float Duration;
        public float Elapsed;
    }

    public void Initialize(Camera cameraSource)
    {
        targetCamera = cameraSource;
        EnsureResources();
        ApplyStaticSettings();
        RefreshMapCloudTransform();
        SetTransition(0f, 0f, 1f);
    }

    private void Reset()
    {
        targetCamera = GetComponent<Camera>();
        mapCloudShader = Shader.Find("Custom/ProceduralMapCloudCover");
        cloudVolumeShader = Shader.Find("Custom/CloudDiveVolume");
    }

    public void SetMapCloudsVisible(bool visible)
    {
        mapCloudsRequestedVisible = visible;
        RefreshMapCloudVisibility();
    }

    public void SetBaseViewReadyForBirds(bool ready)
    {
        if (baseViewReadyForBirds == ready)
            return;

        baseViewReadyForBirds = ready;

        if (baseViewReadyForBirds)
            ScheduleNextPeriodicBirdSpawn();
        else
            nextPeriodicBirdSpawnTime = float.PositiveInfinity;
    }

    public void SetTransition(float progress, float strength, float direction)
    {
        EnsureResources();

        if (volumeObject == null || volumeMaterial == null)
            return;

        bool wasTransitionActive = transitionActive;
        float clampedStrength = Mathf.Clamp01(strength);
        bool visible = clampedStrength > 0.001f && progress > 0.001f;
        transitionActive = visible;
        volumeObject.SetActive(visible);
        RefreshMapCloudVisibility();

        if (!visible)
        {
            if (wasTransitionActive)
            {
                planePlayed = false;
                birdsPlayed = false;
            }

            return;
        }

        if (!wasTransitionActive)
        {
            ClearActiveFlybys();
            planePlayed = false;
            birdsPlayed = false;
        }

        volumeObject.transform.localPosition = Vector3.zero;
        volumeObject.transform.localRotation = Quaternion.identity;
        volumeObject.transform.localScale = Vector3.one * volumeSize;

        volumeMaterial.SetFloat(ProgressID, Mathf.Clamp01(progress));
        volumeMaterial.SetFloat(StrengthID, clampedStrength);
        volumeMaterial.SetFloat(DirectionID, direction >= 0f ? 1f : -1f);

        UpdateFlybyTriggers(Mathf.Clamp01(progress), direction);
    }

    private void Update()
    {
        RefreshRuntimeObjectLayers();
        RefreshMapCloudTransform();
        UpdateActiveFlybys();
        UpdatePeriodicBirdFlocks();
    }

    private void EnsureResources()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (targetCamera == null)
            return;

        EnsureMapCloudResources();
        EnsureVolumeResources();
    }

    private void EnsureMapCloudResources()
    {
        if (targetCamera == null)
            return;

        if (mapCloudShader == null)
            mapCloudShader = Shader.Find("Custom/ProceduralMapCloudCover");

        if (mapCloudShader == null)
            return;

        if (mapCloudMaterial == null)
        {
            mapCloudMaterial = new Material(mapCloudShader)
            {
                name = "Runtime Procedural Map Cloud Cover",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        if (mapCloudObject != null)
            return;

        mapCloudObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        mapCloudObject.name = "Runtime Procedural Map Cloud Cover";
        mapCloudObject.hideFlags = GetRuntimeObjectHideFlags();
        mapCloudObject.layer = GetCameraVisibleLayer();
        mapCloudObject.transform.SetParent(targetCamera.transform, false);
        mapCloudObject.SetActive(false);

        Collider cloudCollider = mapCloudObject.GetComponent<Collider>();
        if (cloudCollider != null)
            Destroy(cloudCollider);

        MeshRenderer mapRenderer = mapCloudObject.GetComponent<MeshRenderer>();
        if (mapRenderer != null)
        {
            mapRenderer.sharedMaterial = mapCloudMaterial;
            mapRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mapRenderer.receiveShadows = false;
            mapRenderer.allowOcclusionWhenDynamic = false;
            ConfigureOverlayRenderer(mapRenderer);
        }
    }

    private void EnsureVolumeResources()
    {
        if (targetCamera == null)
            return;

        if (cloudVolumeShader == null)
            cloudVolumeShader = Shader.Find("Custom/CloudDiveVolume");

        if (cloudVolumeShader == null)
            return;

        if (volumeMaterial == null)
        {
            volumeMaterial = new Material(cloudVolumeShader)
            {
                name = "Runtime Cloud Dive Volume",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        if (volumeObject != null)
            return;

        volumeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        volumeObject.name = "Runtime Cloud Dive Volume";
        volumeObject.hideFlags = GetRuntimeObjectHideFlags();
        volumeObject.layer = GetCameraVisibleLayer();
        volumeObject.transform.SetParent(targetCamera.transform, false);
        volumeObject.transform.localPosition = Vector3.zero;
        volumeObject.transform.localRotation = Quaternion.identity;
        volumeObject.transform.localScale = Vector3.one * volumeSize;
        volumeObject.SetActive(false);

        Collider volumeCollider = volumeObject.GetComponent<Collider>();
        if (volumeCollider != null)
            Destroy(volumeCollider);

        volumeRenderer = volumeObject.GetComponent<MeshRenderer>();
        if (volumeRenderer != null)
        {
            volumeRenderer.sharedMaterial = volumeMaterial;
            volumeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            volumeRenderer.receiveShadows = false;
            volumeRenderer.allowOcclusionWhenDynamic = false;
            ConfigureOverlayRenderer(volumeRenderer);
        }
    }

    private static void ConfigureOverlayRenderer(Renderer targetRenderer)
    {
        if (targetRenderer == null)
            return;

        SortingLayer[] sortingLayers = SortingLayer.layers;
        if (sortingLayers != null && sortingLayers.Length > 0)
            targetRenderer.sortingLayerID = sortingLayers[sortingLayers.Length - 1].id;

        targetRenderer.sortingOrder = short.MaxValue;
    }

    private void ApplyStaticSettings()
    {
        if (mapCloudMaterial != null)
        {
            mapCloudMaterial.SetFloat(MapOpacityID, mapCloudOpacity);
            mapCloudMaterial.SetFloat(MapCoverageID, mapCloudCoverage);
            mapCloudMaterial.SetFloat(MapSoftnessID, mapCloudSoftness);
            mapCloudMaterial.SetFloat(MapNoiseScaleID, mapCloudNoiseScale);
            mapCloudMaterial.SetFloat(MapDetailScaleID, mapCloudDetailScale);
            mapCloudMaterial.SetVector(MapDriftID, new Vector4(mapCloudDrift.x, mapCloudDrift.y, 0f, 0f));
            mapCloudMaterial.SetColor(MapHighlightID, cloudHighlight);
            mapCloudMaterial.SetColor(MapLowlightID, cloudLowlight);
        }

        if (volumeMaterial == null)
            return;

        volumeMaterial.SetFloat(DensityID, density);
        volumeMaterial.SetFloat(NoiseScaleID, noiseScale);
        volumeMaterial.SetFloat(DiveSpeedID, diveSpeed);
        volumeMaterial.SetColor(HighlightID, cloudHighlight);
        volumeMaterial.SetColor(LowlightID, cloudLowlight);
    }

    private void RefreshMapCloudTransform()
    {
        if (mapCloudObject == null || targetCamera == null)
            return;

        float distance = Mathf.Max(0.01f, mapCloudDistance);
        float viewHeight = targetCamera.orthographic
            ? targetCamera.orthographicSize * 2f
            : 2f * distance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float viewWidth = viewHeight * targetCamera.aspect;

        mapCloudObject.transform.localPosition = Vector3.forward * distance;
        mapCloudObject.transform.localRotation = Quaternion.identity;
        mapCloudObject.transform.localScale = new Vector3(
            viewWidth * mapCloudCoveragePadding,
            viewHeight * mapCloudCoveragePadding,
            1f
        );
    }

    private void RefreshMapCloudVisibility()
    {
        if (mapCloudObject == null)
            return;

        bool visible = mapCloudsRequestedVisible && mapCloudOpacity > 0.001f;
        mapCloudObject.SetActive(visible);
    }

    private void UpdateFlybyTriggers(float progress, float direction)
    {
        bool allowFlybys = direction > 0f || playFlybysWhenReturningToMap;
        if (!allowFlybys)
            return;

        if (!planePlayed && progress >= planeTriggerProgress)
        {
            planePlayed = true;
            SpawnFlyby(
                planePrefab,
                planeStartViewport,
                planeEndViewport,
                planeDistance,
                planeDuration,
                planeScaleMultiplier,
                planeRotationOffsetEuler,
                0f,
                useWorldSpace: false
            );
        }

        bool birdsAllowed = !birdRequiresBaseView || baseViewReadyForBirds;
        if (birdsAllowed && !birdsPlayed && progress >= birdTriggerProgress)
        {
            birdsPlayed = true;
            SpawnBirdFlocks();
        }
    }

    private void SpawnBirdFlocks()
    {
        if (birdFlockPrefabs == null || birdFlockPrefabs.Length == 0)
            return;

        bool leftToRight = birdAllowLeftToRight && Random.value < birdLeftToRightChance;
        int count = Mathf.Max(1, birdFlockCount);
        for (int i = 0; i < count; i++)
        {
            GameObject prefab = birdFlockPrefabs[Random.Range(0, birdFlockPrefabs.Length)];
            if (prefab == null)
                continue;

            Vector2 scatter = Random.insideUnitCircle * birdViewportScatter;
            float stagger = count > 1 ? i / (float)(count - 1) : 0.5f;
            Vector2 configuredStart = birdStartViewport + scatter + Vector2.up * Mathf.Lerp(-0.08f, 0.08f, stagger);
            Vector2 configuredEnd = birdEndViewport + scatter + Vector2.up * Mathf.Lerp(0.08f, -0.08f, stagger);
            Vector2 start = leftToRight ? configuredEnd : configuredStart;
            Vector2 end = leftToRight ? configuredStart : configuredEnd;

            SpawnFlyby(
                prefab,
                start,
                end,
                birdDistance + i * 0.8f,
                birdDuration,
                birdScaleMultiplier,
                birdRotationOffsetEuler,
                birdHeightOffset,
                birdUseWorldSpace
            );
        }
    }

    private void UpdatePeriodicBirdFlocks()
    {
        if (!spawnPeriodicBirdsInBaseView || !baseViewReadyForBirds || transitionActive)
            return;

        if (birdFlockPrefabs == null || birdFlockPrefabs.Length == 0)
            return;

        if (Time.time < nextPeriodicBirdSpawnTime)
            return;

        SpawnBirdFlocks();
        ScheduleNextPeriodicBirdSpawn();
    }

    private void ScheduleNextPeriodicBirdSpawn()
    {
        float minInterval = Mathf.Max(0.1f, Mathf.Min(periodicBirdInterval.x, periodicBirdInterval.y));
        float maxInterval = Mathf.Max(minInterval, Mathf.Max(periodicBirdInterval.x, periodicBirdInterval.y));
        nextPeriodicBirdSpawnTime = Time.time + Random.Range(minInterval, maxInterval);
    }

    private void SpawnFlyby(
        GameObject prefab,
        Vector2 startViewport,
        Vector2 endViewport,
        float distance,
        float duration,
        float scaleMultiplier,
        Vector3 rotationOffsetEuler,
        float heightOffset,
        bool useWorldSpace)
    {
        if (prefab == null || targetCamera == null)
            return;

        Object spawnedObject = Instantiate((Object)prefab);
        GameObject instance = spawnedObject as GameObject;
        if (instance == null)
        {
            if (spawnedObject != null)
                Destroy(spawnedObject);

            Debug.LogWarning($"WorldCloudTransition: Flyby prefab '{prefab.name}' did not instantiate as a GameObject.", this);
            return;
        }

        instance.name = prefab.name + " Runtime Flyby";
        if (!useWorldSpace)
            instance.transform.SetParent(targetCamera.transform, false);

        Vector3 start = useWorldSpace
            ? ViewportToWorld(startViewport, distance)
            : ViewportToCameraLocal(startViewport, distance);
        Vector3 end = useWorldSpace
            ? ViewportToWorld(endViewport, distance)
            : ViewportToCameraLocal(endViewport, distance);
        Vector3 offset = Vector3.up * heightOffset;
        start += offset;
        end += offset;
        Vector3 direction = end - start;

        if (useWorldSpace)
            instance.transform.position = start;
        else
            instance.transform.localPosition = start;

        Quaternion rotation = direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(rotationOffsetEuler)
            : Quaternion.Euler(rotationOffsetEuler);

        if (useWorldSpace)
            instance.transform.rotation = rotation;
        else
            instance.transform.localRotation = rotation;

        instance.transform.localScale = prefab.transform.localScale * Mathf.Max(0.01f, scaleMultiplier);

        activeFlybys.Add(new ActiveFlyby
        {
            Transform = instance.transform,
            StartPosition = start,
            EndPosition = end,
            UseLocalSpace = !useWorldSpace,
            Duration = Mathf.Max(0.05f, duration),
            Elapsed = 0f
        });
    }

    private HideFlags GetRuntimeObjectHideFlags()
    {
        return showRuntimeObjectsInHierarchy ? HideFlags.DontSave : HideFlags.HideAndDontSave;
    }

    private void RefreshRuntimeObjectHideFlags()
    {
        HideFlags runtimeFlags = GetRuntimeObjectHideFlags();

        if (mapCloudObject != null)
            mapCloudObject.hideFlags = runtimeFlags;

        if (volumeObject != null)
            volumeObject.hideFlags = runtimeFlags;
    }

    private void RefreshRuntimeObjectLayers()
    {
        int visibleLayer = GetCameraVisibleLayer();

        if (mapCloudObject != null)
            mapCloudObject.layer = visibleLayer;

        if (volumeObject != null)
            volumeObject.layer = visibleLayer;
    }

    private int GetCameraVisibleLayer()
    {
        if (targetCamera == null)
            return gameObject.layer;

        int cullingMask = targetCamera.cullingMask;
        for (int layer = 0; layer < 32; layer++)
        {
            if ((cullingMask & (1 << layer)) != 0)
                return layer;
        }

        return gameObject.layer;
    }

    private Vector3 ViewportToCameraLocal(Vector2 viewportPosition, float distance)
    {
        if (targetCamera == null)
            return Vector3.forward * distance;

        float safeDistance = Mathf.Max(0.1f, distance);
        float viewHeight = targetCamera.orthographic
            ? targetCamera.orthographicSize * 2f
            : 2f * safeDistance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float viewWidth = viewHeight * targetCamera.aspect;

        return new Vector3(
            (viewportPosition.x - 0.5f) * viewWidth,
            (viewportPosition.y - 0.5f) * viewHeight,
            safeDistance
        );
    }

    private Vector3 ViewportToWorld(Vector2 viewportPosition, float distance)
    {
        if (targetCamera == null)
            return Vector3.forward * Mathf.Max(0.1f, distance);

        return targetCamera.ViewportToWorldPoint(new Vector3(
            viewportPosition.x,
            viewportPosition.y,
            Mathf.Max(0.1f, distance)
        ));
    }

    private void UpdateActiveFlybys()
    {
        for (int i = activeFlybys.Count - 1; i >= 0; i--)
        {
            ActiveFlyby flyby = activeFlybys[i];
            if (flyby.Transform == null)
            {
                activeFlybys.RemoveAt(i);
                continue;
            }

            flyby.Elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(flyby.Elapsed / flyby.Duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            Vector3 position = Vector3.LerpUnclamped(
                flyby.StartPosition,
                flyby.EndPosition,
                eased
            );

            if (flyby.UseLocalSpace)
                flyby.Transform.localPosition = position;
            else
                flyby.Transform.position = position;

            if (t >= 1f)
            {
                Destroy(flyby.Transform.gameObject);
                activeFlybys.RemoveAt(i);
            }
        }
    }

    private void ClearActiveFlybys()
    {
        for (int i = activeFlybys.Count - 1; i >= 0; i--)
        {
            if (activeFlybys[i].Transform != null)
                Destroy(activeFlybys[i].Transform.gameObject);
        }

        activeFlybys.Clear();
    }

    private void OnValidate()
    {
        mapCloudDistance = Mathf.Max(0.01f, mapCloudDistance);
        mapCloudCoveragePadding = Mathf.Max(1f, mapCloudCoveragePadding);
        volumeSize = Mathf.Max(1f, volumeSize);
        planeDuration = Mathf.Max(0.05f, planeDuration);
        birdFlockCount = Mathf.Max(1, birdFlockCount);
        birdDuration = Mathf.Max(0.05f, birdDuration);
        periodicBirdInterval.x = Mathf.Max(0.1f, periodicBirdInterval.x);
        periodicBirdInterval.y = Mathf.Max(0.1f, periodicBirdInterval.y);
        RefreshRuntimeObjectHideFlags();
        ApplyStaticSettings();
    }

    private void OnDisable()
    {
        if (mapCloudObject != null)
            mapCloudObject.SetActive(false);

        if (volumeObject != null)
            volumeObject.SetActive(false);

        ClearActiveFlybys();
        planePlayed = false;
        birdsPlayed = false;
        baseViewReadyForBirds = false;
        nextPeriodicBirdSpawnTime = float.PositiveInfinity;
    }

    private void OnDestroy()
    {
        ClearActiveFlybys();

        if (mapCloudObject != null)
            Destroy(mapCloudObject);

        if (mapCloudMaterial != null)
            Destroy(mapCloudMaterial);

        if (volumeObject != null)
            Destroy(volumeObject);

        if (volumeMaterial != null)
            Destroy(volumeMaterial);
    }
}
