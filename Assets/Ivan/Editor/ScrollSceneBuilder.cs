using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using Ivan.Diorama;
using Ivan.Scroll;

namespace Ivan.Scroll.EditorTools
{
    // 卷轴剧情段生成器：
    //   1) 扫场景里所有 StoryTripwire + DialogueDisplay 配置 → 估算对话总时长
    //   2) 卷长 = 移动速度 × 总时长（可加缓冲/手动覆盖）
    //   3) 沿卷长做梯度带生成：左端树多 → 末端草和石头主导（山脚 → 山顶）
    //   4) 避让 ScrollReservedZone（手摆河流等）、河流本体、出生点
    //   5) 可选扩展 Stationary paper ground，玩家活动边界自动跟随
    // 生成物全部收在「Scroll Scenery Root」下，重生成 = 整体替换，手动摆的东西不受影响。
    public class ScrollSceneBuilder : EditorWindow
    {
        private const string k_rootName = "Scroll Scenery Root";
        private const string k_undoLabel = "Build Scroll Scenery";

        [Header("素材（留空会用 Ivan/Prefabs 下的默认项）")]
        [SerializeField] private List<GameObject> m_treePrefabs = new();
        [SerializeField] private List<GameObject> m_grassPrefabs = new();
        [SerializeField] private List<GameObject> m_stonePrefabs = new();

        [Header("节奏")]
        [Tooltip("自动行走的移动速度（米/秒），应与 ScrollPlayDirector / 玩家一致")]
        [SerializeField] private float m_walkSpeed = 4f;

        [Tooltip("卷长缓冲系数：>1 留白多一点（推荐 1.15），1 = 正好讲完")]
        [SerializeField] private float m_lengthScale = 1.15f;

        [Tooltip("手动覆盖卷长（米），0 = 自动按对话时长推算")]
        [SerializeField] private float m_lengthOverride = 0f;

        [Header("范围")]
        [Tooltip("卷轴右端（谜题区左边缘）的 X 坐标")]
        [SerializeField] private float m_puzzleEdgeX = 40f;

        [Tooltip("生成的 Z 范围（可行走带两侧）")]
        [SerializeField] private float m_zMin = -14f;
        [SerializeField] private float m_zMax = 14f;

        [Header("通道")]
        [Tooltip("中央通行带的半宽（米）。±此范围不生成任何装饰，让玩家能走过去")]
        [SerializeField] private float m_walkwayHalfWidth = 1.8f;

        [Header("密度")]
        [Tooltip("采样格边长（米），越小越密")]
        [SerializeField] private float m_cellSize = 1.6f;

        [Tooltip("树基准密度（0~1，乘以梯度权重后落地）")]
        [SerializeField] private float m_treeDensity = 0.3f;

        [Tooltip("草基准密度")]
        [SerializeField] private float m_grassDensity = 0.28f;

        [Tooltip("石头基准密度（降一点，末端点缀就好）")]
        [SerializeField] private float m_stoneDensity = 0.12f;

        [Tooltip("石头从哪段进度开始出现（0~1，山腰以上才有石头）")]
        [SerializeField] private float m_stoneStartT = 0.55f;

        [Header("其它")]
        [Tooltip("同时避让 DioramaRiver 的水面（InRiver 判定）")]
        [SerializeField] private bool m_avoidRiver = true;

        [Tooltip("草/石的随机缩放幅度（0 = 统一大小）")]
        [SerializeField] private float m_scaleJitter = 0.12f;

        [Tooltip("随机种子。重 roll 换一个")]
        [SerializeField] private int m_seed = 12345;

        [Header("地面")]
        [Tooltip("生成时同时向左扩展 Stationary paper ground（玩家活动边界自动跟随）")]
        [SerializeField] private bool m_stretchGround = true;

        private Vector2 m_scroll;
        private float m_parallelSeconds;    // 边走边说型对话时长（与走路并行，决定卷长下限）
        private float m_pausedSeconds;      // 停走型对话时长（额外停留，不产生位移）
        private float m_resolvedStartX;     // 本次生效的生成区间起点（Build/Pick 共用）
        private float m_resolvedLength;     // 本次生效的卷长

        [MenuItem("Tools/Dongba/Scroll/Scroll Scene Builder")]
        public static void Open()
        {
            var window = GetWindow<ScrollSceneBuilder>("Scroll Scene Builder");
            window.minSize = new Vector2(400f, 560f);
        }

        private void OnEnable()
        {
            // 不再自动填素材，用户自己在 Inspector 里拖 List
        }

        private void OnGUI()
        {
            m_scroll = EditorGUILayout.BeginScrollView(m_scroll);

            EditorGUILayout.HelpBox(
                "按场景里所有 Story Tripwire 的对话区间估算时长 → 卷长 = 速度 × 时长 × 缓冲。\n" +
                "沿卷长做梯度：左端树主导 → 末端草和石头主导（山脚 → 山顶）。\n" +
                "中央 ±walkwayHalfWidth 范围留作通道，Scroll Reserved Zone / 河流 / 出生点也不生成。",
                MessageType.Info);

            var so = new SerializedObject(this);
            EditorGUILayout.PropertyField(so.FindProperty(nameof(m_treePrefabs)), new GUIContent("树 Prefabs"), true);
            EditorGUILayout.PropertyField(so.FindProperty(nameof(m_grassPrefabs)), new GUIContent("草 Prefabs"), true);
            EditorGUILayout.PropertyField(so.FindProperty(nameof(m_stonePrefabs)), new GUIContent("石头 Prefabs"), true);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("节奏", EditorStyles.boldLabel);
            DrawField(so, nameof(m_walkSpeed));
            DrawField(so, nameof(m_lengthScale));
            DrawField(so, nameof(m_lengthOverride));

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("范围", EditorStyles.boldLabel);
            DrawField(so, nameof(m_puzzleEdgeX));
            DrawField(so, nameof(m_zMin));
            DrawField(so, nameof(m_zMax));

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("通道", EditorStyles.boldLabel);
            DrawField(so, nameof(m_walkwayHalfWidth));
            EditorGUILayout.HelpBox(
                $"中央 ±{m_walkwayHalfWidth:F1}m 范围内不生成任何装饰。" +
                $"当前可用通道宽度 {m_walkwayHalfWidth * 2f:F1}m。",
                MessageType.None);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("密度", EditorStyles.boldLabel);
            DrawField(so, nameof(m_cellSize));
            DrawField(so, nameof(m_treeDensity));
            DrawField(so, nameof(m_grassDensity));
            DrawField(so, nameof(m_stoneDensity));
            DrawField(so, nameof(m_stoneStartT));

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("其它", EditorStyles.boldLabel);
            DrawField(so, nameof(m_avoidRiver));
            DrawField(so, nameof(m_scaleJitter));

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawField(so, nameof(m_seed));
                if (GUILayout.Button("重 Roll", GUILayout.Width(64f)))
                {
                    m_seed = Random.Range(int.MinValue, int.MaxValue);
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.Space(4f);
            DrawField(so, nameof(m_stretchGround));
            so.ApplyModifiedProperties();

            // ---- 时长 / 卷长估算（只读展示）----
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("估算", EditorStyles.boldLabel);
            ResolveRange(out float startX, out float length);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    "对话总时长", $"{m_parallelSeconds + m_pausedSeconds:F1} 秒（停走 {m_pausedSeconds:F1}）");
                EditorGUILayout.LabelField("卷长", $"{length:F1} 米");
            }
            EditorGUILayout.LabelField("生成区间", $"X: {startX:F1} → {m_puzzleEdgeX:F1}（右端为谜题区）");

            var tripwires = FindObjectsByType<StoryTripwire>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (tripwires.Length == 0)
            {
                EditorGUILayout.HelpBox("场景里还没有 Story Tripwire——先摆几条绊线、填好对话区间，卷长才有依据。", MessageType.Warning);
            }

            EditorGUILayout.Space(12f);
            using (new EditorGUI.DisabledScope(m_treePrefabs.Count == 0 && m_grassPrefabs.Count == 0 && m_stonePrefabs.Count == 0))
            {
                if (GUILayout.Button("生成卷轴场景（整体重建）", GUILayout.Height(32f)))
                {
                    Build();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("把玩家移到卷轴起点"))
                {
                    MovePlayerToStart(startX);
                }
                if (GUILayout.Button("只扩展地面"))
                {
                    StretchGround(startX);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawField(SerializedObject so, string name)
        {
            EditorGUILayout.PropertyField(so.FindProperty(name));
        }

        // ------------------------------------------------------------------
        // 时长估算
        // ------------------------------------------------------------------

        // 每条绊线一段：Σ(字符数/打字速度 + 句间停顿) + 段尾停留。
        // 边走边说型的时长与走路并行，决定卷长；停走型（PauseWalking）是纯额外停留
        private void EstimateDialogueSeconds(out float parallelSeconds, out float pausedSeconds)
        {
            parallelSeconds = 0f;
            pausedSeconds = 0f;

            var display = FindObjectsByType<DialogueDisplay>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (display.Length == 0) return;

            var displaySo = new SerializedObject(display[0]);
            var libraryRef = displaySo.FindProperty("m_dialogueLibrary").objectReferenceValue as DialogueLibrarySO;
            if (libraryRef == null) return;

            float cps = displaySo.FindProperty("m_charactersPerSecond").floatValue;
            float lineDelay = displaySo.FindProperty("m_delayBetweenLinesInSeconds").floatValue;
            float hideDelay = displaySo.FindProperty("m_autoHideDelayInSeconds").floatValue;
            if (cps <= 0f) cps = 18f;

            var entries = libraryRef.Entries;
            float total = 0f;

            var tripwires = FindObjectsByType<StoryTripwire>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var wire in tripwires)
            {
                int from = Mathf.Clamp(wire.FromIndex, 0, entries.Count - 1);
                int to = Mathf.Clamp(wire.ToIndex, from, entries.Count - 1);

                for (int i = from; i <= to; i++)
                {
                    var entry = entries[i];

                    // 指令行：@等待N秒 计入段时长；转身/移动/隐藏瞬时完成不计时
                    if (StageCommandParser.TryParse(entry.content, out StageCommand command))
                    {
                        if (command.Type == StageCommandType.Wait)
                        {
                            total += command.Value;
                        }
                        continue;
                    }

                    int characters = (entry.speaker?.Length ?? 0) + (entry.content?.Length ?? 0) + 2;
                    total += characters / cps + lineDelay;
                }
                total += hideDelay;

                if (wire.PauseWalking)
                {
                    pausedSeconds += total;
                }
                parallelSeconds += total;
                total = 0f;
            }
            parallelSeconds -= pausedSeconds;   // 停走段不算并行时长
        }

        private float ResolveLength(float seconds)
        {
            if (m_lengthOverride > 0f) return m_lengthOverride;
            return m_walkSpeed * seconds * Mathf.Max(0.5f, m_lengthScale);
        }

        // 生成区间取两者的较大值：
        //   1) 对话时长推算的卷长（节奏对齐）
        //   2) 覆盖最左绊线所需的长度（绊线位置是手工指定的空间布局，必须盖住），
        //      左端再留 6 米给玩家出生点
        private void ResolveRange(out float startX, out float length)
        {
            EstimateDialogueSeconds(out m_parallelSeconds, out m_pausedSeconds);
            float dialogueLength = ResolveLength(m_parallelSeconds);

            float leftmost = float.MaxValue;
            var tripwires = FindObjectsByType<StoryTripwire>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var wire in tripwires)
            {
                leftmost = Mathf.Min(leftmost, wire.transform.position.x);
            }
            float minLength = leftmost < float.MaxValue ? m_puzzleEdgeX - leftmost + 6f : 0f;

            length = Mathf.Max(dialogueLength, minLength);
            startX = m_puzzleEdgeX - length;

            // 缓存给 PickPrefab / IsBlocked 用，保证与显示、生成完全一致
            m_resolvedStartX = startX;
            m_resolvedLength = length;
        }

        // ------------------------------------------------------------------
        // 生成
        // ------------------------------------------------------------------

        private void Build()
        {
            ResolveRange(out float startX, out float length);

            // 地面上表面高度——贴地用，参考 SnapToGroundTool
            float groundY = 0f;
            var ground = GameObject.Find("Stationary paper ground");
            if (ground != null)
            {
                var gr = ground.GetComponent<Renderer>();
                if (gr != null) groundY = gr.bounds.max.y;
            }

            var zones = FindObjectsByType<ScrollReservedZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var zoneBounds = new List<Bounds>(zones.Length);
            foreach (var zone in zones)
            {
                zoneBounds.Add(zone.WorldBounds);
            }

            var root = GameObject.Find(k_rootName);
            if (root != null)
            {
                // 卷轴装饰是 PCG 产物：整体重建，不搞增量
                bool ok = EditorUtility.DisplayDialog("Scroll Builder",
                    $"「{k_rootName}」已存在（{root.transform.childCount} 个物体）。\n重建会先清空它的全部子物体，继续？", "重建", "取消");
                if (!ok) return;

                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    Undo.DestroyObjectImmediate(root.transform.GetChild(i).gameObject);
                }
            }
            else
            {
                root = new GameObject(k_rootName);
                Undo.RegisterCreatedObjectUndo(root, k_undoLabel);
            }

            var random = new System.Random(m_seed);
            int placedTrees = 0, placedGrass = 0, placedStones = 0;
            var occupied = new List<Vector2>();   // 已占用点（XZ），用于最小间距

            for (float x = startX + 1.5f; x < m_puzzleEdgeX - 1f; x += m_cellSize)
            {
                for (float z = m_zMin; z < m_zMax; z += m_cellSize)
                {
                    // 网格内抖动，去掉机械感
                    float px = x + (float)(random.NextDouble() - 0.5) * m_cellSize;
                    float pz = z + (float)(random.NextDouble() - 0.5) * m_cellSize;

                    GameObject prefab = PickPrefab(px, random);
                    if (prefab == null) continue;

                    if (IsBlocked(px, pz, zoneBounds, occupied)) continue;

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    instance.transform.SetParent(root.transform, false);
                    instance.transform.position = new Vector3(px, groundY, pz);
                    instance.name = prefab.name;

                    if (m_scaleJitter > 0f && !IsTree(prefab))
                    {
                        float scale = 1f + (float)(random.NextDouble() * 2 - 1) * m_scaleJitter;
                        instance.transform.localScale = Vector3.one * Mathf.Max(0.5f, scale);
                    }

                    // 贴地：渲染包围盒底边对齐地面上表面（参考 SnapToGroundTool）
                    SnapInstanceToGround(instance, groundY);

                    // 层级：按项目公式 100 - z*10（DioramaRiver.SortingOrderFor）。
                    // Default 层里 z 越小 sortingOrder 越高，前景挡背景。
                    // 同 z 时按物体类型次级排序：树 > 石头 > 草（树干不能被地面装饰盖）
                    int order = DioramaRiver.SortingOrderFor(instance.transform.position);
                    int typeBias;
                    if (IsTree(prefab)) typeBias = 2;
                    else if (IsStone(prefab)) typeBias = 0;
                    else typeBias = -2;   // 草/花最低，被树和石头覆盖
                    order += typeBias;

                    foreach (var sr in instance.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        sr.sortingOrder = order;
                    }

                    occupied.Add(new Vector2(px, pz));
                    if (IsTree(prefab)) placedTrees++;
                    else if (IsStone(prefab)) placedStones++;
                    else placedGrass++;
                }
            }

            if (m_stretchGround)
            {
                StretchGround(startX);
            }

            EditorUtility.SetDirty(root);
            Selection.activeGameObject = root;
            Debug.Log($"[ScrollSceneBuilder] 生成完成：树 {placedTrees}、草 {placedGrass}、石头 {placedStones}，" +
                      $"区间 [{startX:F1}, {m_puzzleEdgeX:F1}]，地面 y={groundY:F3}，种子 {m_seed}。", root);
        }

        // 把实例贴到 groundY。InstantiatePrefab 刚创建的对象，Renderer.bounds
        // 可能还没应用 transform matrix（Unity 渲染循环才更新），所以改用
        // Sprite.pivot + sprite.rect + pixelsPerUnit 手动算 sprite 底边的
        // world y，直接推导出正确的 instance.position.y。
        private static void SnapInstanceToGround(GameObject instance, float groundY)
        {
            var spriteRenderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
            if (spriteRenderers.Length == 0) return;

            // 找第一个 sprite 非空的 SpriteRenderer
            SpriteRenderer sr = null;
            foreach (var s in spriteRenderers)
            {
                if (s.sprite != null) { sr = s; break; }
            }
            if (sr == null) return;

            Sprite sprite = sr.sprite;

            // Sprite.pivot 是 pixel 坐标 relative to sprite.rect，
            // pivot.y = 0 表示精灵底部，= spriteHeight 表示顶部。
            // 这里转成 normalized (0~1) 便于算 world 偏移。
            Vector2 pivotPx = sprite.pivot;
            float h = sprite.rect.height;
            if (h <= 0f) return;

            float pivotYNorm = Mathf.Clamp01(pivotPx.y / h);

            // sprite 在 world 里的高度
            float worldHeight = h / sprite.pixelsPerUnit;

            // pivot 相对于 sprite 底边的 world y 偏移量（向上为正）
            // pivot 在底部(0) → offset = 0
            // pivot 在中心(0.5) → offset = worldHeight / 2
            // pivot 在顶部(1) → offset = worldHeight
            float localOffsetFromBottom = pivotYNorm * worldHeight;

            // 把 sr.transform 往上的 localPosition.y 全部累加，
            // 得到 sr 相对于 instance 的 y 偏移
            float accumulatedChildY = 0f;
            Transform cur = sr.transform;
            while (cur != null && cur != instance.transform)
            {
                accumulatedChildY += cur.localPosition.y;
                cur = cur.parent;
            }

            // 目标：sprite 底边 world y = groundY
            // instance.position.y + accumulatedChildY + localOffsetFromBottom = groundY
            Vector3 pos = instance.transform.position;
            pos.y = groundY - accumulatedChildY - localOffsetFromBottom;
            instance.transform.position = pos;
        }

        // 梯度选类：t=0 树海 → t=1 草原+碎石（山脚 → 山顶）
        private GameObject PickPrefab(float x, System.Random random)
        {
            float t = Mathf.Clamp01((x - m_resolvedStartX) / Mathf.Max(1f, m_resolvedLength));

            float wTree = Mathf.Max(0f, 1f - t * 1.35f) * m_treeDensity;
            float wGrass = (0.2f + 0.8f * t) * m_grassDensity;
            float wStone = SmoothStep(m_stoneStartT, 1f, t) * m_stoneDensity;

            float roll = (float)random.NextDouble() * (wTree + wGrass + wStone);
            if (roll < wTree) return Pick(m_treePrefabs, random);
            roll -= wTree;
            if (roll < wGrass) return Pick(m_grassPrefabs, random);
            roll -= wGrass;
            if (roll < wStone) return Pick(m_stonePrefabs, random);
            return null;
        }

        private static GameObject Pick(List<GameObject> list, System.Random random)
        {
            if (list == null || list.Count == 0) return null;
            return list[random.Next(list.Count)];
        }

        private bool IsTree(GameObject prefab) => m_treePrefabs.Contains(prefab);
        private bool IsStone(GameObject prefab) => m_stonePrefabs.Contains(prefab);

        // 保留区 / 河流 / 出生点 / 中央通道 / 最小间距
        private bool IsBlocked(float x, float z, List<Bounds> zones, List<Vector2> occupied)
        {
            // 出生点附近留一块空地
            if (x < m_resolvedStartX + 3f && Mathf.Abs(z) < 3f) return true;

            // 中央通行带：±m_walkwayHalfWidth 范围不生成任何装饰，让玩家能走过去
            if (Mathf.Abs(z) < m_walkwayHalfWidth) return true;

            if (m_avoidRiver && DioramaRiver.InRiver(new Vector3(x, 0f, z))) return true;

            foreach (Bounds zone in zones)
            {
                if (x >= zone.min.x && x <= zone.max.x && z >= zone.min.z && z <= zone.max.z)
                {
                    return true;
                }
            }

            foreach (Vector2 point in occupied)
            {
                float dx = point.x - x;
                float dz = point.y - z;
                if (dx * dx + dz * dz < m_cellSize * m_cellSize * 0.5f) return true;
            }
            return false;
        }

        private static float SmoothStep(float from, float to, float value)
        {
            float t = Mathf.Clamp01((value - from) / Mathf.Max(0.0001f, to - from));
            return t * t * (3f - 2f * t);
        }

        // ------------------------------------------------------------------
        // 地面与玩家
        // ------------------------------------------------------------------

        // 把 Stationary paper ground 向左扩到覆盖卷轴起点，玩家活动边界（按地面推导）自动跟随
        private static void StretchGround(float startX)
        {
            var ground = GameObject.Find("Stationary paper ground");
            if (ground == null)
            {
                Debug.LogWarning("[ScrollSceneBuilder] 找不到 Stationary paper ground，跳过地面扩展。");
                return;
            }

            var rend = ground.GetComponent<Renderer>();
            if (rend == null) return;

            Bounds b = rend.bounds;
            float targetMinX = startX - 3f;
            if (b.min.x <= targetMinX)
            {
                Debug.Log("[ScrollSceneBuilder] 地面已覆盖卷轴范围，无需扩展。");
                return;
            }

            float newWidth = b.max.x - targetMinX;
            var t = ground.transform;

            Undo.RecordObject(t, k_undoLabel);
            float scaleX = t.localScale.x * (newWidth / b.size.x);
            Vector3 pos = t.position;
            pos.x = (b.max.x + targetMinX) * 0.5f;
            t.position = pos;
            Vector3 scale = t.localScale;
            scale.x = scaleX;
            t.localScale = scale;

            // 边界按地面推导：主动触发一次重算
            var player = FindObjectsByType<DioramaPlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var controller in player)
            {
                controller.DeriveBoundsFromGround();
            }

            EditorUtility.SetDirty(ground);
            Debug.Log($"[ScrollSceneBuilder] 地面已向左扩展至 X={targetMinX:F1}，玩家边界已重算。");
        }

        private static void MovePlayerToStart(float startX)
        {
            var player = FindObjectsByType<DioramaPlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (player.Length == 0)
            {
                Debug.LogWarning("[ScrollSceneBuilder] 场景里没有 DioramaPlayerController。");
                return;
            }

            float groundY = 0f;
            var ground = GameObject.Find("Stationary paper ground");
            if (ground != null)
            {
                var gr = ground.GetComponent<Renderer>();
                if (gr != null) groundY = gr.bounds.max.y;
            }

            var body = player[0].transform;
            Undo.RecordObject(body, k_undoLabel);
            body.position = new Vector3(startX + 1.5f, groundY, 0f);
            EditorUtility.SetDirty(body.gameObject);
        }
    }
}
