using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 一次性诊断探针：菜单启动 → 自动进 Play → 对「一棵树」做逐层隔离渲染。
// 输出 Diagnostics/layers：
//   all.png          = 该树全部图层（树干 + 6 层枝叶）
//   trunkonly.png    = 只留树干
//   only_leaf_i.png  = 只留第 i 层枝叶（其余叶层关掉）
//   same_texture.png = 6 层全部改用同一个 sprite（验证是否为纹理绑定问题）
// 定位后整文件删除。
namespace DongbaPrototype.EditorDiagnostics
{
    public static class ObliqueForestTreeProbe
    {
        private const string k_sessionKey = "ObliqueForestTreeProbe_Armed";
        private const string k_reportPath = "Diagnostics/obliqueforest-trees.txt";
        private const string k_folder = "Diagnostics/layers";
        private const int k_width = 900;
        private const int k_height = 700;
        private static double s_playStartTime;
        private static bool s_reported;

        [MenuItem("Tools/Dongba/Diagnostics/Probe ObliqueForest Trees")]
        public static void ArmAndPlay()
        {
            SessionState.SetBool(k_sessionKey, true);
            s_reported = false;
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = true;
            }
        }

        [InitializeOnLoadMethod]
        private static void Install()
        {
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
            EditorApplication.update += Poll;
        }

        private static void HandlePlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(k_sessionKey, false))
            {
                s_playStartTime = EditorApplication.timeSinceStartup;
                s_reported = false;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(k_sessionKey, false);
            }
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(k_sessionKey, false) || s_reported) return;
            if (!EditorApplication.isPlaying) return;

            // 进 Play 后等 3 秒，让 ObliqueForest.Start() 把森林全部生成完
            if (EditorApplication.timeSinceStartup - s_playStartTime < 3.0) return;

            var forest = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .FirstOrDefault(c => c != null && c.GetType().Name == "ObliqueForest");
            if (forest == null)
            {
                WriteReport(new StringBuilder("场景里没找到 ObliqueForest 组件。"));
                return;
            }

            s_reported = true;
            Directory.CreateDirectory("Diagnostics");
            var report = BuildReport(forest);

            var tree = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t != null && t.name.StartsWith("Dictionary tree /"));
            var camera = Camera.main;
            if (tree == null || camera == null)
            {
                WriteReport(report);
                EditorApplication.isPlaying = false;
                return;
            }

            try
            {
                CaptureLayers(tree, camera, forest, report);
            }
            catch (Exception ex)
            {
                Debug.LogError("[ObliqueForestTreeProbe] 逐层截图失败: " + ex);
            }
            finally
            {
                WriteReport(report);
                EditorApplication.isPlaying = false;
            }
        }

        // 静态渲染 + ReadPixels，不依赖帧循环，因此可以在 Editor update 里同步跑完。
        private static void CaptureLayers(Transform tree, Camera camera, Component forest, StringBuilder report)
        {
            Directory.CreateDirectory(k_folder);
            var capture = forest.GetType().GetMethod(
                "CaptureSceneToTexture",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null, new[] { typeof(string), typeof(int), typeof(int) }, null);
            if (capture == null)
            {
                report.AppendLine("找不到 CaptureSceneToTexture(string,int,int)");
                return;
            }

            Vector3 savedPosition = camera.transform.position;
            Quaternion savedRotation = camera.transform.rotation;
            float savedOrthoSize = camera.orthographicSize;

            // 相机放在树的「前方」（视线反方向），否则会落到地面以下只能拍到纸背景。
            Vector3 forward = savedRotation * Vector3.forward;
            Vector3 up = savedRotation * Vector3.up;
            camera.orthographicSize = 3f;
            camera.transform.position = tree.position - forward * 20f + up * 1.2f;

            File.WriteAllText(k_folder + "/tree.txt",
                $"{tree.name}\nroot={tree.position}\nforward={forward}\ncamera={camera.transform.position}");

            var renderers = tree.GetComponentsInChildren<SpriteRenderer>(true);
            var leaves = renderers.Where(r => r.transform.name.StartsWith("Original tree foliage stroke")).ToArray();
            var trunk = renderers.FirstOrDefault(r => r.transform.name == "Glyph");

            // 逐层隔离前先记录每一层真实的纹理信息，判断 sprite 到底指向哪张图。
            report.AppendLine();
            report.AppendLine($"== 逐层取证：{tree.name} ==");
            foreach (var r in leaves)
            {
                var sprite = r.sprite;
                var texture = sprite != null ? sprite.texture : null;
                report.AppendLine(string.Format(
                    "  {0}: sprite={1} rect={2} bounds={3} texture={4} {5}x{6} {7} readable={8} worldBounds={9} lossyScale={10} order={11} enabled={12} colorA={13}",
                    r.transform.name,
                    sprite != null ? sprite.name : "<null>",
                    sprite != null ? sprite.rect.ToString() : "-",
                    sprite != null ? sprite.bounds.size.ToString() : "-",
                    texture != null ? texture.name : "<null>",
                    texture != null ? texture.width : 0,
                    texture != null ? texture.height : 0,
                    texture != null ? texture.format.ToString() : "-",
                    texture != null ? texture.isReadable.ToString() : "-",
                    r.bounds.size.ToString(),
                    r.transform.lossyScale.ToString(),
                    r.sortingOrder,
                    r.enabled,
                    r.color.a));
            }
            if (trunk != null)
            {
                var ts = trunk.sprite;
                var tt = ts != null ? ts.texture : null;
                report.AppendLine($"  Glyph(trunk): sprite={(ts != null ? ts.name : "<null>")} rect={(ts != null ? ts.rect.ToString() : "-")} " +
                    $"texture={(tt != null ? tt.name : "<null>")} {(tt != null ? tt.width : 0)}x{(tt != null ? tt.height : 0)}");
            }

            // 1) 全开
            capture.Invoke(forest, new object[] { $"{k_folder}/all.png", k_width, k_height });

            // 2) 只留树干
            bool[] wasEnabled = leaves.Select(r => r.enabled).ToArray();
            foreach (var r in leaves) r.enabled = false;
            capture.Invoke(forest, new object[] { $"{k_folder}/trunkonly.png", k_width, k_height });
            for (int i = 0; i < leaves.Length; i++) leaves[i].enabled = wasEnabled[i];

            // 3) 每层单独开（其余全关）
            for (int i = 0; i < leaves.Length; i++)
            {
                foreach (var r in leaves) r.enabled = false;
                leaves[i].enabled = true;
                capture.Invoke(forest, new object[] { $"{k_folder}/only_leaf_{i:00}.png", k_width, k_height });
                foreach (var r in leaves) r.enabled = false;
            }
            for (int i = 0; i < leaves.Length; i++) leaves[i].enabled = wasEnabled[i];

            // 4) 6 层全部改用同一个 sprite：若此时出现 6 份重叠的同一笔触，说明共享材质的 _MainTex 绑定有问题
            var sprites = leaves.Select(r => r.sprite).ToArray();
            if (leaves.Length > 0)
            {
                Sprite probeSprite = sprites.Length > 3 ? sprites[3] : sprites[0];
                foreach (var r in leaves) r.sprite = probeSprite;
                capture.Invoke(forest, new object[] { $"{k_folder}/same_texture.png", k_width, k_height });
                for (int i = 0; i < leaves.Length; i++) leaves[i].sprite = sprites[i];
            }

            // 5) 绕开 RootGrowthInk 自定义着色器，叶层换回 URP 默认 sprite 材质
            var previousMaterials = leaves.Select(r => r.sharedMaterial).ToArray();
            var plain = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            foreach (var r in leaves) r.sharedMaterial = plain;
            capture.Invoke(forest, new object[] { $"{k_folder}/default_material.png", k_width, k_height });
            for (int i = 0; i < leaves.Length; i++) leaves[i].sharedMaterial = previousMaterials[i];

            // 6) 叶层改用「导入自动生成的 Sprite」（与树干同一来源），对比运行时 Sprite.Create 的结果
            var autoSprites = new Sprite[leaves.Length];
            string key = sprites.Length > 0 && sprites[0] != null
                ? sprites[0].name.Substring(0, Mathf.Max(0, sprites[0].name.LastIndexOf("_leaf_", StringComparison.Ordinal)))
                : "plant_00";
            for (int i = 0; i < leaves.Length && i < 6; i++)
            {
                autoSprites[i] = Resources.Load<Sprite>("EcologyTreeV2/" + key + "_leaf_" + i);
            }
            for (int i = 0; i < leaves.Length; i++) leaves[i].sprite = autoSprites[i] ?? sprites[i];
            capture.Invoke(forest, new object[] { $"{k_folder}/auto_sprite.png", k_width, k_height });
            for (int i = 0; i < leaves.Length; i++) leaves[i].sprite = sprites[i];

            // 7) 每层各用一份材质实例（共享材质可能导致六层合批后共用一张 _MainTex）
            var beforeMats = leaves.Select(r => r.sharedMaterial).ToArray();
            var perLeafMats = leaves.Select(r => new Material(r.sharedMaterial)).ToArray();
            for (int i = 0; i < leaves.Length; i++) leaves[i].sharedMaterial = perLeafMats[i];
            capture.Invoke(forest, new object[] { $"{k_folder}/fix_permat.png", k_width, k_height });
            for (int i = 0; i < leaves.Length; i++) leaves[i].sharedMaterial = beforeMats[i];
            foreach (var m in perLeafMats) UnityEngine.Object.Destroy(m);

            // 8) 每层显式写 MaterialPropertyBlock 的 _MainTex
            var blocks = new MaterialPropertyBlock[leaves.Length];
            for (int i = 0; i < leaves.Length; i++)
            {
                blocks[i] = new MaterialPropertyBlock();
                if (leaves[i].sprite != null) blocks[i].SetTexture("_MainTex", leaves[i].sprite.texture);
                leaves[i].SetPropertyBlock(blocks[i]);
            }
            capture.Invoke(forest, new object[] { $"{k_folder}/fix_mpb.png", k_width, k_height });
            for (int i = 0; i < leaves.Length; i++) leaves[i].SetPropertyBlock(null);

            camera.transform.position = savedPosition;
            camera.transform.rotation = savedRotation;
            camera.orthographicSize = savedOrthoSize;
            Debug.Log($"[ObliqueForestTreeProbe] 逐层截图完成，见 {k_folder}");
        }

        private static StringBuilder BuildReport(Component forest)
        {
            var log = new StringBuilder();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

            log.AppendLine("== ObliqueForest 全局状态 ==");
            foreach (string field in new[] { "greenHeight", "greenVelocity", "ecologyVisual", "Balance", "FlourishThreshold", "initialPlantCount", "plants" })
            {
                var member = forest.GetType().GetField(field, flags) ?? (MemberInfo)forest.GetType().GetProperty(field, flags);
                if (member == null)
                {
                    log.AppendLine($"  {field}: <字段不存在>");
                    continue;
                }
                object value = member is FieldInfo f ? f.GetValue(forest)
                    : ((PropertyInfo)member).GetValue(forest);
                if (value is System.Collections.ICollection collection)
                {
                    log.AppendLine($"  {field}: 集合，数量 {collection.Count}");
                }
                else
                {
                    log.AppendLine($"  {field}: {value}");
                }
            }

            var inkField = forest.GetType().GetField("rootGrowthInk", flags);
            if (inkField?.GetValue(forest) is Material ink)
            {
                log.AppendLine($"  rootGrowthInk: {ink.name} shader={ink.shader?.name} _Growth={ink.GetFloat("_Growth")}");
            }
            else
            {
                log.AppendLine("  rootGrowthInk: <null 或不是材质>");
            }

            if (Camera.main != null)
            {
                log.AppendLine($"  Camera.main: pos={Camera.main.transform.position} rot={Camera.main.transform.rotation.eulerAngles} " +
                    $"ortho={Camera.main.orthographic} size={Camera.main.orthographicSize} pixelSize={Camera.main.pixelWidth}x{Camera.main.pixelHeight}");
            }
            return log;
        }

        private static void WriteReport(StringBuilder log)
        {
            Directory.CreateDirectory("Diagnostics");
            File.WriteAllText(k_reportPath, log.ToString());
            Debug.Log($"[ObliqueForestTreeProbe] 报告写入 {k_reportPath}");
        }
    }
}
