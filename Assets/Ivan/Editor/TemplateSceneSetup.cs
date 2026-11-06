using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// One-click setup: converts the project to 3D rendering and generates the Template scene
// (dimetric 30°/45° orthographic camera, watercolor ground, tree lines on the far edges, and
// Dongba glyph prefabs: player, trees, river, sun, and ground decals). All content comes from
// the reusable prefabs built by DongbaAssetBuilder, so other scenes can be assembled the same way.
public static class TemplateSceneSetup
{
    private const string k_sceneFolderPath = "Assets/Ivan/Scenes";
    private const string k_scenePath = k_sceneFolderPath + "/Template.unity";
    private const string k_artFolderPath = "Assets/Ivan/Art";
    private const string k_prefabFolderPath = "Assets/Ivan/Prefabs";
    private const string k_groundMaterialPath = "Assets/Ivan/Art/Dongba/M_Ground.mat";
    private const string k_settingsFolderPath = "Assets/Settings";
    private const string k_renderer3DPath = k_settingsFolderPath + "/UniversalRenderer3D.asset";
    private const string k_pipeline3DPath = k_settingsFolderPath + "/UniversalRP_3D.asset";
    private const string k_inputActionsPath = "Assets/InputSystem_Actions.inputactions";

    // 30° pitch + 45° yaw gives the classic dimetric look: flat ground with visible vertical walls
    private const float k_cameraPitchDegrees = 30f;
    private const float k_cameraYawDegrees = 45f;

    // Perspective distance chosen so the vertical span at the map center matches the old ortho
    // size 6 (2 * d * tan(fov/2) ≈ 12 m), keeping the framing while adding near-big/far-small depth
    private const float k_cameraFovDegrees = 40f;
    private const float k_cameraDistanceMeters = 17f;

    // Square play area; must stay in sync with the default clamp bounds in PlayerMov
    private const float k_boundsMinX = -8f;
    private const float k_boundsMaxX = 8f;
    private const float k_boundsMinZ = -8f;
    private const float k_boundsMaxZ = 8f;

    [MenuItem("Tools/Dongba/Setup Template Scene")]
    public static void SetupTemplateScene()
    {
        // Keep the user's current work safe before replacing the active scene
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        Ensure3DProjectSettings();
        EnsureFolders();
        EnsureProjectWideInputActions();

        // Prefabs are the single source of scene content; build them once if they are missing
        DongbaAssetBuilder.EnsureAssetsBuilt();

        Material groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_groundMaterialPath);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Camera camera = CreateCamera();
        CreateDirectionalLight();
        CreateGround(groundMaterial);
        CreateTreeLines();

        PlaceDongbaPrefabs();
        GameObject player = PlacePrefab("Player", Vector3.zero);

        // Sort transparent sprites along the view direction so closer sprites draw on top
        GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
        GraphicsSettings.transparencySortAxis = camera.transform.forward;

        EditorSceneManager.SaveScene(scene, k_scenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = player;

        Debug.Log($"[TemplateSceneSetup] Done. Scene saved to {k_scenePath}. Press Play and move with WASD (8 directions).");
    }

    private static void PlaceDongbaPrefabs()
    {
        PlacePrefab("Tree", new Vector3(-4f, 0f, 3f));
        PlacePrefab("Tree", new Vector3(2.5f, 0f, 5f));
        PlacePrefab("Tree", new Vector3(-6f, 0f, -2f));

        PlacePrefab("River", new Vector3(0f, 0f, 3.5f));

        // The sun hangs above the far corner, higher than the 3m walls so it stays in frame
        PlacePrefab("Sun", new Vector3(5f, 4.5f, 5f));

        // Water glyphs scattered on the floor as quiet accents
        PlacePrefab("GroundDecal", new Vector3(-2f, 0f, -3f));
        PlacePrefab("GroundDecal", new Vector3(3f, 0f, 1f));
        PlacePrefab("GroundDecal", new Vector3(-5.5f, 0f, 5.5f));
    }

    private static GameObject PlacePrefab(string prefabName, Vector3 position, float heightScale = 1f)
    {
        string prefabPath = $"{k_prefabFolderPath}/{prefabName}.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[TemplateSceneSetup] Missing prefab: {prefabPath}. Run Tools > Dongba > Build Dongba Assets first.");
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = position;
        // Only stretch vertically: tall trees get longer trunks without growing wider
        instance.transform.localScale = new Vector3(1f, heightScale, 1f);
        return instance;
    }

    private static void Ensure3DProjectSettings()
    {
        // Switch the editor to 3D mode so new scenes and imports default to 3D
        EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode3D;

        UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(k_renderer3DPath);
        if (rendererData == null)
        {
            rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, k_renderer3DPath);
        }

        UniversalRenderPipelineAsset pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(k_pipeline3DPath);
        if (pipelineAsset == null)
        {
            pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(pipelineAsset, k_pipeline3DPath);
        }

        GraphicsSettings.defaultRenderPipeline = pipelineAsset;

        // Quality levels can override the default pipeline, so assign the 3D asset on every level
        int previousLevel = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = pipelineAsset;
        }
        QualitySettings.SetQualityLevel(previousLevel, false);
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(k_sceneFolderPath))
        {
            AssetDatabase.CreateFolder("Assets/Ivan", "Scenes");
        }

        if (!AssetDatabase.IsValidFolder(k_artFolderPath))
        {
            AssetDatabase.CreateFolder("Assets/Ivan", "Art");
        }
    }

    private static void EnsureProjectWideInputActions()
    {
        // PlayerMov reads InputSystem.actions, so make sure the asset is assigned as project-wide
        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(k_inputActionsPath);
        if (actions != null && InputSystem.actions != actions)
        {
            InputSystem.actions = actions;
        }
    }

    private static Camera CreateCamera()
    {
        GameObject cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";

        Quaternion rotation = Quaternion.Euler(k_cameraPitchDegrees, k_cameraYawDegrees, 0f);
        cameraGo.transform.rotation = rotation;
        cameraGo.transform.position = rotation * Vector3.back * k_cameraDistanceMeters;

        Camera camera = cameraGo.AddComponent<Camera>();
        // Perspective over ortho: near-big/far-small depth makes the square map read as a diorama.
        // Billboards stay camera-facing, so sprites only scale naturally with distance.
        camera.fieldOfView = k_cameraFovDegrees;
        camera.farClipPlane = 100f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f);

        cameraGo.AddComponent<AudioListener>();

        // Force URP to attach its additional camera data component
        camera.GetUniversalAdditionalCameraData();
        return camera;
    }

    private static void CreateDirectionalLight()
    {
        GameObject lightGo = new GameObject("Directional Light");
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
    }

    private static void CreateTreeLines()
    {
        // Rows of tall trees along the far (+X / +Z) edges take the role of backdrop walls
        const float treeLineDistance = 8.6f;
        const float spacing = 1.9f;

        int index = 0;
        for (float x = k_boundsMinX; x <= k_boundsMaxX + 0.01f; x += spacing)
        {
            PlacePrefab("Tree", new Vector3(x + TreeJitter(index), 0f, treeLineDistance + TreeJitter(index + 1)), TreeScale(index));
            index++;
        }

        // The east line stops short of the corner so the two rows do not double up there
        for (float z = k_boundsMinZ; z <= k_boundsMaxZ - spacing; z += spacing)
        {
            PlacePrefab("Tree", new Vector3(treeLineDistance + TreeJitter(index), 0f, z + TreeJitter(index + 1)), TreeScale(index));
            index++;
        }
    }

    private static float TreeScale(int index)
    {
        // Deterministic variation (0.9x - 1.6x) so re-running setup grows the same forest
        return 0.9f + 0.7f * Mathf.Abs(Mathf.Sin(index * 1.7f));
    }

    private static float TreeJitter(int seed)
    {
        return 0.35f * Mathf.Sin(seed * 2.3f);
    }

    private static void CreateGround(Material material)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        // A plane is 10x10 meters at scale 1, so this gives a square 24 x 24 meters of floor
        ground.transform.localScale = new Vector3(2.4f, 1f, 2.4f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = material;
    }
}
