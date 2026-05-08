using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class MapWaterCreator
{
    private const string WaterMaterialPath = "Assets/Materials/M_MapWater.mat";
    private const string WaterShaderName = "Custom/URP/MapWaterLit";
    private const string WaterSpriteShapeProfilePath = "Assets/Materials/MapWaterSpriteShapeProfile.asset";
    private const string WaterFillTexturePath = "Assets/Materials/MapWaterFill.png";
    private const string ClosedSpriteShapePrefabPath =
        "Packages/com.unity.2d.spriteshape/Editor/ObjectMenuCreation/DefaultAssets/Sprite Shapes/Closed Sprite Shape.prefab";
    private const string MapLayerName = "MapView";
    private const string SortingLayerName = "Default";
    private const int WaterSortingOrder = -20;

    [MenuItem("GameObject/Map/Create Water Area", false, 10)]
    public static void CreateWaterArea(MenuCommand menuCommand)
    {
        GameObject parent = new GameObject("MapWater");
        GameObjectUtility.SetParentAndAlign(parent, menuCommand.context as GameObject);
        ApplyMapLayer(parent);
        Undo.RegisterCreatedObjectUndo(parent, "Create Map Water");

        GameObject waterObject = CreateSpriteShapeWater(parent.transform);
        if (waterObject == null)
        {
            Undo.DestroyObjectImmediate(parent);
            Debug.LogError(
                "MapWaterCreator: Sprite Shape is not available. Install package 'com.unity.2d.spriteshape' and try again.");
            return;
        }

        Renderer renderer = waterObject.GetComponent<Renderer>();
        Material material = LoadOrCreateMaterial();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;

        MapWaterController controller = waterObject.AddComponent<MapWaterController>();
        controller.ApplySettings();

        Selection.activeGameObject = parent;
        EditorGUIUtility.PingObject(parent);
    }

    [MenuItem("GameObject/Map/Create Water Area (Quad Fallback)", false, 12)]
    public static void CreateWaterAreaQuadFallback(MenuCommand menuCommand)
    {
        GameObject parent = new GameObject("MapWater");
        GameObjectUtility.SetParentAndAlign(parent, menuCommand.context as GameObject);
        ApplyMapLayer(parent);
        Undo.RegisterCreatedObjectUndo(parent, "Create Map Water");

        GameObject waterObject = CreateQuadWater(parent.transform);

        Renderer renderer = waterObject.GetComponent<Renderer>();
        Material material = LoadOrCreateMaterial();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;

        MapWaterController controller = waterObject.AddComponent<MapWaterController>();
        controller.ApplySettings();

        Selection.activeGameObject = parent;
        EditorGUIUtility.PingObject(parent);
    }

    [MenuItem("GameObject/Map/Fix Selected Water Sprite Shape Profile", false, 11)]
    public static void FixSelectedWaterSpriteShapeProfile()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("MapWaterCreator: Select the Water SpriteShape Area object first.");
            return;
        }

        Component spriteShapeController = FindSpriteShapeController(selected);
        if (spriteShapeController == null)
        {
            Debug.LogWarning("MapWaterCreator: No SpriteShapeController found on the selected object, its children, or its parent.");
            return;
        }

        Undo.RecordObject(spriteShapeController, "Assign Map Water Sprite Shape Profile");
        AssignWaterSpriteShapeProfile(spriteShapeController);

        Renderer renderer = spriteShapeController.GetComponent<Renderer>();
        Material material = LoadOrCreateMaterial();
        if (renderer != null && material != null)
        {
            Undo.RecordObject(renderer, "Assign Map Water Material");
            renderer.sharedMaterial = material;
            ConfigureRenderer(renderer);
        }

        MapWaterController waterController = spriteShapeController.GetComponent<MapWaterController>();
        if (waterController == null)
            waterController = spriteShapeController.gameObject.AddComponent<MapWaterController>();

        waterController.ApplySettings();
        EditorUtility.SetDirty(spriteShapeController);
        Debug.Log("MapWaterCreator: Assigned the map water Sprite Shape profile and material.", spriteShapeController);
    }

    private static GameObject CreateSpriteShapeWater(Transform parent)
    {
        Type controllerType = FindType(
            "UnityEngine.U2D.SpriteShapeController, Unity.2D.SpriteShape.Runtime",
            "UnityEngine.U2D.SpriteShapeController, Unity.2D.SpriteShape");
        Type rendererType = FindType(
            "UnityEngine.U2D.SpriteShapeRenderer, Unity.2D.SpriteShape.Runtime",
            "UnityEngine.U2D.SpriteShapeRenderer, Unity.2D.SpriteShape");

        if (controllerType == null || rendererType == null)
            return null;

        GameObject waterObject = CreateDefaultClosedSpriteShape(controllerType);
        if (waterObject == null)
            waterObject = new GameObject("Water SpriteShape Area");

        waterObject.name = "Water SpriteShape Area";
        waterObject.transform.SetParent(parent, false);
        waterObject.transform.localPosition = new Vector3(0f, 0f, 0.1f);
        ApplyMapLayer(waterObject);
        Undo.RegisterCreatedObjectUndo(waterObject, "Create Map Water SpriteShape");

        Component controller = waterObject.GetComponent(controllerType);
        if (controller == null)
            controller = waterObject.AddComponent(controllerType);

        Renderer renderer = waterObject.GetComponent(rendererType) as Renderer;
        ConfigureRenderer(renderer);
        AssignWaterSpriteShapeProfile(controller);
        TryCreateDefaultSpriteShapeSpline(controller);

        return waterObject;
    }

    private static GameObject CreateDefaultClosedSpriteShape(Type controllerType)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ClosedSpriteShapePrefabPath);
        if (prefab == null || prefab.GetComponent(controllerType) == null)
            return null;

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
            return null;

        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        return instance;
    }

    private static GameObject CreateQuadWater(Transform parent)
    {
        GameObject waterObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        waterObject.name = "Water Quad Area";
        waterObject.transform.SetParent(parent, false);
        waterObject.transform.localPosition = new Vector3(0f, 0f, 0.1f);
        waterObject.transform.localScale = new Vector3(12f, 8f, 1f);
        ApplyMapLayer(waterObject);
        Undo.RegisterCreatedObjectUndo(waterObject, "Create Map Water Quad");

        Collider collider = waterObject.GetComponent<Collider>();
        if (collider != null)
            Undo.DestroyObjectImmediate(collider);

        ConfigureRenderer(waterObject.GetComponent<Renderer>());
        return waterObject;
    }

    private static void ConfigureRenderer(Renderer renderer)
    {
        if (renderer == null)
            return;

        renderer.sortingLayerName = SortingLayerName;
        renderer.sortingOrder = WaterSortingOrder;
    }

    private static void ApplyMapLayer(GameObject gameObject)
    {
        int mapLayer = LayerMask.NameToLayer(MapLayerName);
        if (mapLayer >= 0)
            gameObject.layer = mapLayer;
    }

    private static Material LoadOrCreateMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);
        if (material != null)
            return material;

        Shader shader = Shader.Find(WaterShaderName);
        if (shader == null)
        {
            Debug.LogError("MapWaterCreator: Could not find shader " + WaterShaderName + ".");
            return null;
        }

        material = new Material(shader)
        {
            name = "M_MapWater"
        };

        AssetDatabase.CreateAsset(material, WaterMaterialPath);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static Component FindSpriteShapeController(GameObject selected)
    {
        Type controllerType = FindType(
            "UnityEngine.U2D.SpriteShapeController, Unity.2D.SpriteShape.Runtime",
            "UnityEngine.U2D.SpriteShapeController, Unity.2D.SpriteShape");

        if (controllerType == null)
            return null;

        Component controller = selected.GetComponent(controllerType);
        if (controller != null)
            return controller;

        controller = selected.GetComponentInChildren(controllerType);
        if (controller != null)
            return controller;

        return selected.GetComponentInParent(controllerType);
    }

    private static void AssignWaterSpriteShapeProfile(Component spriteShapeController)
    {
        if (spriteShapeController == null)
            return;

        Type spriteShapeType = FindType(
            "UnityEngine.U2D.SpriteShape, Unity.2D.SpriteShape.Runtime",
            "UnityEngine.U2D.SpriteShape, Unity.2D.SpriteShape");

        if (spriteShapeType == null)
            return;

        UnityEngine.Object profile = LoadOrCreateSpriteShapeProfile(spriteShapeType);
        if (profile == null)
            return;

        PropertyInfo spriteShapeProperty = spriteShapeController.GetType().GetProperty("spriteShape", BindingFlags.Instance | BindingFlags.Public);
        spriteShapeProperty?.SetValue(spriteShapeController, profile);

        InvokeIfExists(spriteShapeController, "RefreshSpriteShape");
        object bakeHandle = InvokeIfExists(spriteShapeController, "BakeMesh");
        bakeHandle?.GetType().GetMethod("Complete", BindingFlags.Instance | BindingFlags.Public)?.Invoke(bakeHandle, null);
    }

    private static UnityEngine.Object LoadOrCreateSpriteShapeProfile(Type spriteShapeType)
    {
        UnityEngine.Object profile = AssetDatabase.LoadAssetAtPath(WaterSpriteShapeProfilePath, spriteShapeType);
        Texture2D fillTexture = LoadOrCreateWaterFillTexture();

        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance(spriteShapeType);
            profile.name = "MapWaterSpriteShapeProfile";
            AssetDatabase.CreateAsset(profile, WaterSpriteShapeProfilePath);
        }

        PropertyInfo fillTextureProperty = spriteShapeType.GetProperty("fillTexture", BindingFlags.Instance | BindingFlags.Public);
        if (fillTextureProperty != null && fillTexture != null)
        {
            fillTextureProperty.SetValue(profile, fillTexture);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        return profile;
    }

    private static Texture2D LoadOrCreateWaterFillTexture()
    {
        Texture2D fillTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(WaterFillTexturePath);
        if (fillTexture != null)
            return fillTexture;

        Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(255, 255, 255, 255);

        texture.SetPixels32(pixels);
        texture.Apply();

        byte[] png = texture.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(texture);

        string absolutePath = Path.GetFullPath(WaterFillTexturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
        File.WriteAllBytes(absolutePath, png);
        AssetDatabase.ImportAsset(WaterFillTexturePath);

        TextureImporter importer = AssetImporter.GetAtPath(WaterFillTexturePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Point;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(WaterFillTexturePath);
    }

    private static Type FindType(params string[] assemblyQualifiedNames)
    {
        foreach (string typeName in assemblyQualifiedNames)
        {
            Type type = Type.GetType(typeName);
            if (type != null)
                return type;
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (string typeName in assemblyQualifiedNames)
            {
                string fullName = typeName.Split(',')[0].Trim();
                Type type = assembly.GetType(fullName);
                if (type != null)
                    return type;
            }
        }

        foreach (string typeName in assemblyQualifiedNames)
        {
            string[] parts = typeName.Split(',');
            if (parts.Length < 2)
                continue;

            string fullName = parts[0].Trim();
            string assemblyName = parts[1].Trim();
            try
            {
                Assembly assembly = Assembly.Load(assemblyName);
                Type type = assembly.GetType(fullName);
                if (type != null)
                    return type;
            }
            catch
            {
                // Optional package may not be present. Fall back to the quad water path.
            }
        }

        return null;
    }

    private static void TryCreateDefaultSpriteShapeSpline(Component spriteShapeController)
    {
        if (spriteShapeController == null)
            return;

        PropertyInfo splineProperty = spriteShapeController.GetType().GetProperty("spline", BindingFlags.Instance | BindingFlags.Public);
        object spline = splineProperty != null ? splineProperty.GetValue(spriteShapeController) : null;
        if (spline == null)
            return;

        Type splineType = spline.GetType();
        PropertyInfo isOpenEndedProperty = splineType.GetProperty("isOpenEnded", BindingFlags.Instance | BindingFlags.Public);
        MethodInfo clearMethod = splineType.GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public);
        MethodInfo insertPointAtMethod = splineType.GetMethod("InsertPointAt", BindingFlags.Instance | BindingFlags.Public);
        MethodInfo setTangentModeMethod = splineType.GetMethod("SetTangentMode", BindingFlags.Instance | BindingFlags.Public);
        Type tangentModeType = FindType(
            "UnityEngine.U2D.ShapeTangentMode, Unity.2D.SpriteShape.Runtime",
            "UnityEngine.U2D.ShapeTangentMode, Unity.2D.SpriteShape");

        if (clearMethod == null || insertPointAtMethod == null)
            return;

        try
        {
            isOpenEndedProperty?.SetValue(spline, false);
            clearMethod.Invoke(spline, null);
            insertPointAtMethod.Invoke(spline, new object[] { 0, new Vector3(-6f, -4f, 0f) });
            insertPointAtMethod.Invoke(spline, new object[] { 1, new Vector3(-6f, 4f, 0f) });
            insertPointAtMethod.Invoke(spline, new object[] { 2, new Vector3(6f, 4f, 0f) });
            insertPointAtMethod.Invoke(spline, new object[] { 3, new Vector3(6f, -4f, 0f) });

            if (setTangentModeMethod != null && tangentModeType != null)
            {
                object continuousMode = Enum.Parse(tangentModeType, "Continuous");
                for (int i = 0; i < 4; i++)
                    setTangentModeMethod.Invoke(spline, new[] { (object)i, continuousMode });
            }

            InvokeIfExists(spriteShapeController, "RefreshSpriteShape");
        }
        catch (Exception exception)
        {
            Debug.LogWarning("MapWaterCreator: Created Sprite Shape water, but could not seed a default spline. " + exception.Message);
        }
    }

    private static object InvokeIfExists(Component component, string methodName)
    {
        if (component == null)
            return null;

        MethodInfo method = component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        return method?.Invoke(component, null);
    }
}
