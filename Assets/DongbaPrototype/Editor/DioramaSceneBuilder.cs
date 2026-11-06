using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

// One-click diorama scene builder.
// 完全复用组员 ObliqueForest 的素材 / Shader / 布局。
// 手动按 Start() 里的关键步骤反射调，跳过玩家/HUD/协程等运行时依赖部分。
namespace DongbaPrototype {
public class DioramaSceneBuilder : EditorWindow {
    private GameObject m_sceneRoot;

    [MenuItem("Tools/Dongba/Diorama Scene Builder (Prototype)")]
    public static void ShowWindow() {
        var w = GetWindow<DioramaSceneBuilder>("Diorama Builder");
        w.minSize = new Vector2(300, 160);
    }

    private void OnGUI() {
        EditorGUILayout.LabelField("一键生成东巴 Diorama 场景", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "完全复用 Prototype 的 ObliqueForest 初始化链。\n" +
            "材质 / Shader / 布局 = 组员 Play Mode 版本 100% 一致。\n" +
            "生成后 parent 到 Diorama Scene Root，可自由编辑。",
            MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("首次生成", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope()) {
            if (GUILayout.Button("Generate Prototype Layout", GUILayout.Height(32))) {
                Generate();
            }

            if (GUILayout.Button("Clear", GUILayout.Height(32))) {
                Clear();
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("已生成场景的增量修补（不重建）", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Retrofit：给现有场景补挂缺失的交互组件、解冻相机、按地面重算活动边界。\n" +
            "Convert to Cinemachine：把跟随相机换成 Cinemachine 虚拟相机。",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope()) {
            if (GUILayout.Button("Retrofit 现有场景", GUILayout.Height(28))) {
                RetrofitExisting();
            }

            if (GUILayout.Button("Convert to Cinemachine", GUILayout.Height(28))) {
                ConvertToCinemachine();
            }
        }

        if (GUILayout.Button("创建可行走区域（不规则边界）", GUILayout.Height(28))) {
            CreateWalkableArea();
        }
    }

    // ----------------------------------------------------------------
    // 反射调 Start() 里布局所需的那几行，跳过玩家/HUD/协程
    // ----------------------------------------------------------------

    private void Generate() {
        Clear();

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Diorama Scene Build (Prototype)");

        m_sceneRoot = new GameObject("Diorama Scene Root");
        Undo.RegisterCreatedObjectUndo(m_sceneRoot, "Create Diorama Root");

        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var t = typeof(ObliqueForest);

        // --- Step 1: 创建 Orthographic 相机（组员布局按 orthographicSize=8.5 算） ---
        var forestCamGo = new GameObject("Forest Orthographic Camera");
        forestCamGo.tag = "MainCamera";
        forestCamGo.AddComponent<AudioListener>().enabled = false;
        var forestCam = forestCamGo.AddComponent<Camera>();
        forestCam.orthographic = true;
        forestCam.orthographicSize = 8.5f;
        forestCam.clearFlags = CameraClearFlags.SolidColor;
        forestCam.backgroundColor = new Color(0.90f, 0.85f, 0.73f);
        forestCam.transform.position = new Vector3(0, 17, -20);
        forestCam.transform.LookAt(new Vector3(0, 0, 1));
        // 相机完全可编辑（不再 hideFlags 锁定），方便后续调参数或换成 Cinemachine

        // --- Step 2: 创建 ObliqueForest 实例 ---
        var forestGo = new GameObject("(temp) ObliqueForest Runner");
        forestGo.hideFlags = HideFlags.DontSaveInEditor;
        var forest = forestGo.AddComponent<ObliqueForest>();
        forest.enabled = false; // 禁用 Update

        // --- Step 3: 反射注入 cam / billboard ---
        t.GetField("cam", flags).SetValue(forest, forestCam);
        t.GetField("billboard", flags).SetValue(forest, forestCam.transform.rotation);

        // --- Step 4: 手动初始化材质（组员 Start() Line 73 那行） ---
        // 这步是关键！之前失败就是因为直接反射调 Start() 时被 Get("player_glyph") 之类的 throw 中断，
        // 但 Start() 前两行材质初始化已经被跳过/部分执行，导致后面 AddDictionaryPlant 用的 rootGrowthInk 是 null → NRE
        var rootGrowthShader = Resources.Load<Shader>("RootGrowthInk");
        var plainShader = Shader.Find("Sprites/Default");
        var flowerShader = Resources.Load<Shader>("FlowerInk");

        if (rootGrowthShader != null)
            t.GetField("rootGrowthInk", flags).SetValue(forest, new Material(rootGrowthShader));
        else
            Debug.LogWarning("[DioramaSceneBuilder] Resources.Load<Shader>(\"RootGrowthInk\") 返回 null —— 树会显示异常");

        if (plainShader != null)
            t.GetField("plainTreeInk", flags).SetValue(forest, new Material(plainShader));

        if (flowerShader != null)
            t.GetField("flowerInk", flags).SetValue(forest, new Material(flowerShader));

        // --- Step 5: 布局（自己安全建地面 + 河流，不碰组员 Ground() 的 .mesh getter） ---
        try {
            t.GetMethod("LoadDictionaryArt", flags).Invoke(forest, null);
            t.GetMethod("MakePaper", flags).Invoke(forest, null);

            // 组员 Ground() 拆成两步自己做：
            //   1. SafeBuildGround()    —— 自己建地面 plane，用 sharedMesh，零泄漏
            //   2. SafeBuildRiverMesh()  —— 自己建河流 mesh，组员的 new Mesh() + SetVertices 本来就安全
            SafeBuildGround(forest, flags);
            SafeBuildRiverMesh(forest, flags);

            try { t.GetMethod("SetupFlourish", flags).Invoke(forest, null); } catch (Exception e) {
                Debug.LogWarning($"[DioramaSceneBuilder] SetupFlourish 跳过：{e.Message}");
            }

            t.GetMethod("ComposeForest", flags).Invoke(forest, null);
            t.GetMethod("PlaceForestAnimals", flags).Invoke(forest, null);

            SpawnRiverWaterGlyphs(forest, flags);
        }
        catch (Exception e) {
            Debug.LogError($"[DioramaSceneBuilder] 布局生成失败：{e}");
        }

        // --- Step 5b: 创建玩家 ---
        CreatePlayer(forest, flags);

        // --- Step 5c: 生成 HUD（Editor 静态生成，Play 不再重复生成） ---
        try {
            t.GetMethod("SetupForestHUD", flags).Invoke(forest, null);
        }
        catch (Exception e) {
            Debug.LogWarning($"[DioramaSceneBuilder] SetupForestHUD 跳过：{e.Message}");
        }

        // --- Step 7: 过滤顶层 GameObject ---
        var toKeep = new List<GameObject>();
        var toDelete = new List<GameObject>();

        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>()) {
            if (!go.scene.IsValid()) continue;
            if (go.transform.parent != null) continue;
            if (go == m_sceneRoot) continue;
            if (go == forestGo) continue;
            if (go.hideFlags.HasFlag(HideFlags.DontSaveInEditor)) continue;

            string name = go.name;

            // 保留：组员布局产生的场景对象
            if (name.StartsWith("Dictionary") ||
                name.StartsWith("Peaceful animal") ||
                name.StartsWith("Stationary paper") ||
                name.StartsWith("Stationary river") ||
                name.StartsWith("Water glyph") ||
                name.StartsWith("Soft ground ink") ||
                name.StartsWith("Ground decoration (not a Dongba character)") ||
                name.StartsWith("Small ground strokes")) {
                toKeep.Add(go);
                continue;
            }

            // 保留 forestCamGo（正交俯视角，组员布局按这个比例尺生成）
            if (go == forestCamGo) {
                toKeep.Add(go);
                continue;
            }

            // 保留 HUD（Editor 静态生成的 UI，Play 不再重复生成）
            if (name.StartsWith("Forest HUD") ||
                name.StartsWith("Nearby interaction")) {
                toKeep.Add(go);
                continue;
            }

            // 保留玩家（Play 时由 DioramaPlayerController 驱动）
            if (name.StartsWith("Player foot anchor")) {
                toKeep.Add(go);
                continue;
            }

            // 删除：空池/花迹/落叶/故事等运行时对象（但保留 Player/Axe/Hand）
            if (name.StartsWith("Father") ||
                name.StartsWith("Stationary story") ||
                name.StartsWith("Walk flower") ||
                name.StartsWith("Falling original")) {
                toDelete.Add(go);
                continue;
            }
        }

        // Parent 到 Scene Root（保持世界坐标）
        foreach (var go in toKeep) {
            Undo.SetTransformParent(go.transform, m_sceneRoot.transform, "Parent to Scene Root");
        }

        foreach (var go in toDelete) {
            DestroyImmediate(go);
        }

        // 关键：销毁 ObliqueForest 组件和它的宿主 GameObject。
        // 否则 Play 时 Unity 会自动调 Start()，把所有实体（玩家/动物/树/HUD）再生成一遍。
        DestroyImmediate(forestGo);

        // --- Step 8: 挂运行时交互组件（Ivan.Diorama 三件套，输入映射复刻组员） ---
        int treeCount, animalCount;
        bool hasPlayer;
        AttachRuntimeComponents(m_sceneRoot.transform, forestCam, out treeCount, out animalCount, out hasPlayer);

        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        Selection.activeGameObject = m_sceneRoot;

        Debug.Log(
            $"[DioramaSceneBuilder] 生成完成：保留 {toKeep.Count} 个顶层 GameObject，" +
            $"删除 {toDelete.Count} 个运行时对象。\n" +
            $"ObliqueForest 组件已销毁 —— Play 不会再重复生成。\n" +
            $"已挂交互组件：DioramaTree × {treeCount}、DioramaAnimal × {animalCount}、" +
            $"玩家控制器 {(hasPlayer ? "已挂" : "未找到 Player foot anchor")}。\n" +
            $"Play 后操作：WASD/方向键移动、Space 跳跃、J/Enter 挥斧、E 喂食/补种。");
    }

    // ----------------------------------------------------------------
    // 给生成的场景对象挂运行时交互组件
    // 树 → DioramaTree，动物 → DioramaAnimal，玩家 → DioramaPlayerController
    // ----------------------------------------------------------------

    private static void AttachRuntimeComponents(
        Transform root, Camera cam,
        out int treeCount, out int animalCount, out bool hasPlayer) {
        treeCount = 0;
        animalCount = 0;
        hasPlayer = false;

        foreach (Transform child in root) {
            string name = child.name;

            if (name.StartsWith("Dictionary tree") && child.GetComponent<Ivan.Diorama.DioramaTree>() == null) {
                child.gameObject.AddComponent<Ivan.Diorama.DioramaTree>();
                SetStaticSortingOrder(child);
                treeCount++;
            }
            else if (name.StartsWith("Peaceful animal") && child.GetComponent<Ivan.Diorama.DioramaAnimal>() == null) {
                child.gameObject.AddComponent<Ivan.Diorama.DioramaAnimal>();
                SetStaticSortingOrder(child);
                animalCount++;
            }
            else if (name.StartsWith("Player foot anchor")) {
                // 已挂过就不重复挂（Retrofit 可能被多次点击）
                var pc = child.GetComponent<Ivan.Diorama.DioramaPlayerController>();
                if (pc != null) {
                    hasPlayer = true;
                    continue;
                }
                pc = child.gameObject.AddComponent<Ivan.Diorama.DioramaPlayerController>();
                var body = child.Find("Player glyph");
                var axe = body != null ? body.Find("Axe") : null;

                // 序列化字段赋值
                var so = new SerializedObject(pc);
                so.FindProperty("m_body").objectReferenceValue = body;
                so.FindProperty("m_axe").objectReferenceValue = axe;
                so.FindProperty("m_camera").objectReferenceValue = cam;
                so.ApplyModifiedPropertiesWithoutUndo();
                hasPlayer = true;
            }
        }
    }

    // Editor 生成时静态设一次层级（组员公式 100 - z*10），
    // Play 后由 DioramaTree/DioramaAnimal/DioramaPlayerController 每帧动态维护
    private static void SetStaticSortingOrder(Transform root) {
        int order = Ivan.Diorama.DioramaRiver.SortingOrderFor(root.position);
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true)) {
            sr.sortingOrder = order;
        }
    }

    // ----------------------------------------------------------------
    // 创建玩家（反射调 Start() Line 75-79）
    // 每个 Get() 独立 try-catch，缺贴图就跳过，至少玩家骨架能立起来
    // ----------------------------------------------------------------

    private static void CreatePlayer(object forest, BindingFlags flags) {
        var t = typeof(ObliqueForest);
        var getSprite = t.GetMethod("Get", flags);
        // 明确指定参数类型避免反射歧义
        var billboard = t.GetMethod("Billboard", flags, null,
            new[] { typeof(string), typeof(Transform), typeof(Sprite), typeof(float), typeof(bool) },
            null);
        var fieldPlayer = t.GetField("player", flags);
        var fieldBody = t.GetField("body", flags);
        var fieldPlayerImage = t.GetField("playerImage", flags);
        var fieldLastDry = t.GetField("lastDryPosition", flags);

        // player = new GameObject("Player foot anchor").transform;
        var playerGo = new GameObject("Player foot anchor");
        var player = playerGo.transform;
        player.position = new Vector3(0, 0, 0);
        fieldPlayer.SetValue(forest, player);
        fieldLastDry.SetValue(forest, player.position);

        // Get("player_glyph") —— 可能找不到，try-catch
        Sprite playerSprite = null;
        try { playerSprite = (Sprite)getSprite.Invoke(forest, new object[] { "player_glyph" }); } catch (Exception e) {
            Debug.LogWarning($"[DioramaSceneBuilder] player_glyph 缺失：{e.Message} —— 玩家用占位符");
        }
        if (playerSprite == null) {
            try { playerSprite = (Sprite)getSprite.Invoke(forest, new object[] { "ambient_water_00" }); } catch {}
        }

        // Billboard("Player glyph", player, sprite, 1.8f, false);
        Transform body = null;
        if (billboard != null) {
            try {
                body = (Transform)billboard.Invoke(forest,
                    new object[] { "Player glyph", player, playerSprite, 1.8f, false });
            }
            catch (Exception e) {
                Debug.LogWarning($"[DioramaSceneBuilder] Billboard(player) 失败：{e.Message}");
            }
        }
        fieldBody.SetValue(forest, body);
        if (body != null) {
            var sr = body.GetComponent<SpriteRenderer>();
            fieldPlayerImage.SetValue(forest, sr);
        }

        // Axe（Start() Line 79 简化版 —— 组员原版有完整动画，这里只放静态 sprite）
        Sprite axeSprite = null;
        try { axeSprite = (Sprite)getSprite.Invoke(forest, new object[] { "axe_glyph" }); } catch (Exception e) {
            Debug.LogWarning($"[DioramaSceneBuilder] axe_glyph 缺失：{e.Message}");
        }
        if (axeSprite == null) {
            try { axeSprite = (Sprite)getSprite.Invoke(forest, new object[] { "ambient_water_01" }); } catch {}
        }

        if (axeSprite != null && body != null) {
            var axeGo = new GameObject("Axe");
            axeGo.transform.SetParent(body, false);
            axeGo.transform.localPosition = new Vector3(0.3f, 0.3f, 0);
            var axeSR = axeGo.AddComponent<SpriteRenderer>();
            axeSR.sprite = axeSprite;
            axeSR.sortingOrder = 1;
            axeGo.transform.localScale = Vector3.one * 0.6f;
        }
    }

    // ----------------------------------------------------------------
    // 河流 161 个 Water glyph + Wash 墨迹
    // 组员 Start() Line 85-91 是 inline 代码，这里反射拿辅助方法复制
    // ----------------------------------------------------------------

    private static void SpawnRiverWaterGlyphs(object forest, BindingFlags flags) {
        var t = typeof(ObliqueForest);
        var riverX = t.GetMethod("RiverX", flags);
        var getSprite = t.GetMethod("Get", flags);
        var wash = t.GetMethod("Wash", flags);

        if (riverX == null || getSprite == null || wash == null) {
            Debug.LogWarning("[DioramaSceneBuilder] 反射找不到河流辅助方法，跳过水纹");
            return;
        }

        var layout = new System.Random(148);
        for (int i = 0; i < 161; i++) {
            float z = -20f + i * 0.25f + ((float)layout.NextDouble() - 0.5f) * 0.24f;
            float offset = ((float)layout.NextDouble() - 0.5f) * 1.55f;
            var go = new GameObject("Water glyph " + i);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = -80;
            sr.sprite = (Sprite)getSprite.Invoke(forest, new object[] { "ambient_water_0" + (i % 10) });
            if (sr.sprite == null) { DestroyImmediate(go); continue; }
            sr.color = new Color(0.48f, 0.66f, 0.73f, 0.86f);
            float riverXVal = (float)riverX.Invoke(forest, new object[] { z });
            go.transform.rotation = Quaternion.Euler(90f, (float)layout.NextDouble() * 16f - 8f, 0f);
            go.transform.position = new Vector3(riverXVal + offset, 0.04f, z);
            float scale = (0.85f + (float)layout.NextDouble() * 0.75f) / sr.sprite.bounds.size.x;
            go.transform.localScale = Vector3.one * scale;

            if (i % 3 == 0) {
                wash.Invoke(forest, new object[] {
                    new Vector3(riverXVal, 0, z), 2.8f, new Color(0.32f, 0.54f, 0.62f, 0.25f)
                });
                wash.Invoke(forest, new object[] {
                    new Vector3(riverXVal - 1.4f, 0, z), 1.1f, new Color(0.49f, 0.37f, 0.2f, 0.15f)
                });
                wash.Invoke(forest, new object[] {
                    new Vector3(riverXVal + 1.4f, 0, z), 1.1f, new Color(0.49f, 0.37f, 0.2f, 0.15f)
                });
            }
        }
    }

    // ----------------------------------------------------------------
    // 修 Ground() 的 MeshFilter.mesh 泄漏
    // ----------------------------------------------------------------

    // ----------------------------------------------------------------
    // SafeBuildGround —— 复刻组员 Ground() 的地面 plane，但用 sharedMesh 零泄漏
    // 组员原版：g.GetComponent<MeshFilter>().mesh  ← getter 触发 warning + Instantiate
    // 我们版：sharedMesh 赋值，mesh 用 Instantiate，彻底不碰 getter
    // ----------------------------------------------------------------

    private static void SafeBuildGround(object forest, BindingFlags flags) {
        var t = typeof(ObliqueForest);
        var paperTex = (Texture2D)t.GetField("paper", flags).GetValue(forest);

        var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
        g.name = "Stationary paper ground";
        g.transform.localScale = new Vector3(9.2f, 1f, 4.2f);

        // 用 sharedMesh（Unity 内置 Plane 的原生 mesh，Instantiate 一份改 UV）
        var filter = g.GetComponent<MeshFilter>();
        var safeMesh = Instantiate(filter.sharedMesh);  // Instantiate 不是 getter！
        var uvs = safeMesh.uv;
        for (int i = 0; i < uvs.Length; i++) uvs[i] *= 2f;
        safeMesh.uv = uvs;
        safeMesh.name = "Plane (UV×2, safe)";
        filter.sharedMesh = safeMesh;  // 直接赋 sharedMesh，零 getter 调用

        // 材质 —— 组员也是 new Material + mainTexture = paper
        var shader = Resources.Load<Shader>("EcologyPaper");
        var groundMat = new Material(shader);
        groundMat.mainTexture = paperTex;
        g.GetComponent<Renderer>().sharedMaterial = groundMat;
        g.GetComponent<Renderer>().sortingOrder = -100;
        DestroyImmediate(g.GetComponent<Collider>());

        // 反射 set 组员的 groundMaterial 字段 —— 它在 Update/其他方法里可能引用到
        t.GetField("groundMaterial", flags).SetValue(forest, groundMat);
    }

    // ----------------------------------------------------------------
    // SafeBuildRiverMesh —— 完全复制组员 Ground() Line 121-123 的河流 mesh 代码
    // 组员原版用 new Mesh() + SetVertices/Triangles，是 setter，本来就安全
    // 只是组员 Line 123 用 .mesh = mesh 赋值（setter，OK）+ .material 赋值，
    // 我们全部用 sharedXxx 替代，彻底杜绝实例化
    // ----------------------------------------------------------------

    private static void SafeBuildRiverMesh(object forest, BindingFlags flags) {
        var riverXMethod = typeof(ObliqueForest).GetMethod("RiverX", flags);
        if (riverXMethod == null) return;

        const int segments = 160, rows = 7;
        var verts = new Vector3[(segments + 1) * rows];
        var colors = new Color[verts.Length];
        var tris = new int[segments * 6 * (rows - 1)];
        int k = 0;

        for (int i = 0; i <= segments; i++) {
            float z = -20f + i * 0.25f;
            for (int j = 0; j < rows; j++) {
                float u = (j - 3) / 3f;
                float riverX = (float)riverXMethod.Invoke(forest, new object[] { z });
                verts[i * rows + j] = new Vector3(riverX + u * 1.35f, 0.015f, z);
                colors[i * rows + j] = new Color(.39f, .59f, .64f, (1 - Mathf.Abs(u)) * .28f);
                if (i < segments && j < rows - 1) {
                    int a = i * rows + j;
                    tris[k++] = a; tris[k++] = a + rows; tris[k++] = a + 1;
                    tris[k++] = a + 1; tris[k++] = a + rows; tris[k++] = a + rows + 1;
                }
            }
        }

        var mesh = new Mesh();
        mesh.vertices = verts;
        mesh.colors = colors;
        mesh.triangles = tris;
        mesh.RecalculateNormals();

        var river = new GameObject("Stationary river wash / full map");
        var filter = river.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;  // 用 sharedMesh 赋值（setter，安全）

        var renderer = river.AddComponent<MeshRenderer>();
        var riverMat = new Material(Shader.Find("Sprites/Default"));
        riverMat.color = Color.white;
        renderer.sharedMaterial = riverMat;  // 用 sharedMaterial（安全）
        renderer.sortingOrder = -90;

        // 河面碰撞体：玩家物理阻挡；挂 DioramaWater 注册，跳跃瞬间统一临时禁用
        var collider = river.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        river.AddComponent<Ivan.Diorama.DioramaWater>();
    }

    // ----------------------------------------------------------------
    // Clear
    // ----------------------------------------------------------------

    private void Clear() {
        if (m_sceneRoot != null) {
            Undo.DestroyObjectImmediate(m_sceneRoot);
            m_sceneRoot = null;
        }
        else {
            var old = GameObject.Find("Diorama Scene Root");
            if (old != null) Undo.DestroyObjectImmediate(old);
        }
    }

    // ----------------------------------------------------------------
    // 增量修补（不重建、不丢用户改动）
    // ----------------------------------------------------------------

    // 创建（或复用）不规则可行走区域：初始为当前地面的矩形，
    // 之后在 Scene 视图选中它拖点即可改成任意形状，玩家自动引用。
    [MenuItem("Tools/Dongba/Create Walkable Area")]
    public static void CreateWalkableArea()
    {
        var area = UnityEngine.Object.FindFirstObjectByType<Ivan.Diorama.WalkableArea>();
        if (area == null) {
            var go = new GameObject("Walkable Area");
            Undo.RegisterCreatedObjectUndo(go, "Create Walkable Area");
            area = go.AddComponent<Ivan.Diorama.WalkableArea>();
        }

        // 初始顶点取当前地面的矩形四角；已有顶点则不覆盖用户手改的边界
        if (area.PointCount < 3) {
            var ground = GameObject.Find("Stationary paper ground");
            var renderer = ground != null ? ground.GetComponent<Renderer>() : null;
            if (renderer != null) {
                Bounds b = renderer.bounds;
                Undo.RecordObject(area, "Init Walkable Area");
                area.SetPoints(new[] {
                    new Vector2(b.min.x, b.min.z),
                    new Vector2(b.max.x, b.min.z),
                    new Vector2(b.max.x, b.max.z),
                    new Vector2(b.min.x, b.max.z),
                });
                EditorUtility.SetDirty(area);
            }
        }

        // 自动接到玩家身上，避免手动拖拽引用
        var player = GameObject.Find("Player foot anchor");
        var pc = player != null ? player.GetComponent<Ivan.Diorama.DioramaPlayerController>() : null;
        if (pc != null) {
            var so = new SerializedObject(pc);
            var prop = so.FindProperty("m_walkableArea");
            if (prop != null && prop.objectReferenceValue == null) {
                prop.objectReferenceValue = area;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        Selection.activeGameObject = area.gameObject;
        Debug.Log("[DioramaSceneBuilder] Walkable Area 就绪：选中它即可在 Scene 视图拖点编辑边界。" +
                  "点数不够时点 Inspector 里的「加密边界」。");
    }

    // Retrofit 现有场景：
    //   1. 给现有树/动物/玩家补挂缺失的运行时组件
    //   2. 解冻被 hideFlags 锁死的相机
    //   3. 按地面实际尺寸重算玩家活动边界
    [MenuItem("Tools/Dongba/Retrofit Existing Diorama Scene")]
    public static void RetrofitExisting() {
        var root = GameObject.Find("Diorama Scene Root");
        if (root == null) {
            Debug.LogError("[DioramaSceneBuilder] 场景里没有 Diorama Scene Root，请先生成场景。");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Diorama Scene Retrofit");

        // 1) 解冻相机（旧版本生成时加了 NotEditable）
        var cam = Camera.main;
        if (cam != null && (cam.gameObject.hideFlags & HideFlags.NotEditable) != 0) {
            cam.gameObject.hideFlags = HideFlags.None;
            Debug.Log("[DioramaSceneBuilder] 相机已解冻，可在 Inspector 编辑。");
        }

        // 2) 补挂运行时组件
        int trees, animals;
        bool hasPlayer;
        AttachRuntimeComponents(root.transform, cam, out trees, out animals, out hasPlayer);

        // 3) 重算玩家活动边界（Awake 不会在 Edit Mode 跑，这里显式调用）
        var pc = root.GetComponentInChildren<Ivan.Diorama.DioramaPlayerController>();
        if (pc != null) {
            pc.DeriveBoundsFromGround();
        }

        // 4) 老场景补河面碰撞体（新生成的场景在 SafeBuildRiverMesh 里已带）
        var river = GameObject.Find("Stationary river wash / full map");
        if (river != null && river.GetComponent<Collider>() == null) {
            var mf = river.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) {
                var mc = Undo.AddComponent<MeshCollider>(river);
                mc.sharedMesh = mf.sharedMesh;
                Undo.AddComponent<Ivan.Diorama.DioramaWater>(river);
                Debug.Log("[DioramaSceneBuilder] 已为河面补挂 MeshCollider + DioramaWater。");
            }
        }

        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());

        Debug.Log(
            $"[DioramaSceneBuilder] Retrofit 完成（未重建场景）：\n" +
            $"  新挂 / 已存在的树组件 {trees} 个、动物组件 {animals} 个、玩家控制器 {(hasPlayer ? "已就位" : "未找到 Player foot anchor")}。\n" +
            $"  活动边界已按地面重算（见上方日志）。");
    }

    // ----------------------------------------------------------------
    // Convert to Cinemachine：把自建跟随相机换成 Cinemachine 虚拟相机
    // 保持原正交 8.5 / 偏移 (0,17,-20) / 俯视角度，但可在 Cinemachine 面板调阻尼、FOV 等
    // ----------------------------------------------------------------

    [MenuItem("Tools/Dongba/Convert Camera to Cinemachine")]
    public static void ConvertToCinemachine() {
        var mainCam = Camera.main;
        if (mainCam == null) {
            Debug.LogError("[DioramaSceneBuilder] 找不到 MainCamera，无法转换。");
            return;
        }

        var player = GameObject.Find("Player foot anchor");
        if (player == null) {
            Debug.LogError("[DioramaSceneBuilder] 找不到 Player foot anchor，无法设置跟随目标。");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Convert to Cinemachine");

        // 1) 主相机挂 CinemachineBrain（Cinemachine 的输出终端）
        if (mainCam.GetComponent<CinemachineBrain>() == null) {
            Undo.AddComponent<CinemachineBrain>(mainCam.gameObject);
            Debug.Log("[DioramaSceneBuilder] 已给 MainCamera 挂 CinemachineBrain。");
        }

        // 2) 创建虚拟相机（已存在则复用，避免重复点击堆出多个）
        var existing = GameObject.Find("Diorama Cinemachine Camera");
        GameObject vcamGo;
        if (existing != null) {
            vcamGo = existing;
            Debug.Log("[DioramaSceneBuilder] 复用已存在的 Diorama Cinemachine Camera。");
        }
        else {
            vcamGo = new GameObject("Diorama Cinemachine Camera");
            Undo.RegisterCreatedObjectUndo(vcamGo, "Create Cinemachine Camera");
        }
        vcamGo.transform.position = player.transform.position + new Vector3(0f, 17f, -20f);
        vcamGo.transform.LookAt(player.transform.position + Vector3.forward);

        var vcam = vcamGo.GetComponent<CinemachineCamera>();
        if (vcam == null) {
            vcam = Undo.AddComponent<CinemachineCamera>(vcamGo);
        }

        // 3) 跟随目标 + 正交镜头
        vcam.Follow = player.transform;
        vcam.Priority.Value = 100;   // 高于默认的 0，确保生效

        var lens = vcam.Lens;
        lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        lens.OrthographicSize = mainCam.orthographic ? mainCam.orthographicSize : 8.5f;
        lens.NearClipPlane = mainCam.nearClipPlane;
        lens.FarClipPlane = mainCam.farClipPlane;
        vcam.Lens = lens;

        // 4) Body 组件：世界空间固定偏移，位置带阻尼
        var follow = vcamGo.GetComponent<CinemachineFollow>();
        if (follow == null) {
            follow = vcamGo.AddComponent<CinemachineFollow>();
        }
        follow.FollowOffset = new Vector3(0f, 17f, -20f);
        follow.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
        follow.TrackerSettings.PositionDamping = new Vector3(0.25f, 0.25f, 0.25f);

        // 5) 断开玩家控制器自己的相机跟随，避免两套逻辑打架
        var pc = player.GetComponent<Ivan.Diorama.DioramaPlayerController>();
        if (pc != null) {
            var so = new SerializedObject(pc);
            var camProp = so.FindProperty("m_camera");
            if (camProp != null) {
                camProp.objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            Debug.Log("[DioramaSceneBuilder] 已把 PlayerController 的相机引用置空 —— 改由 Cinemachine 接管跟随。");
        }

        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        Selection.activeGameObject = vcamGo;

        Debug.Log(
            "[DioramaSceneBuilder] Cinemachine 转换完成：\n" +
            "  主相机已挂 CinemachineBrain，新建 Diorama Cinemachine Camera（正交 8.5、偏移 0/17/-20）。\n" +
            "  选中该物体可在 Inspector 调 Damping / 镜头参数，或再加 CinemachineConfiner2D 限制相机范围。");
    }
}
}
