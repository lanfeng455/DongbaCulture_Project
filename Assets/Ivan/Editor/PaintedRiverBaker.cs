using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using Ivan.Diorama;

namespace Ivan.Diorama.EditorTools
{
    // 水墨河流烘焙窗口：把 PaintedRiver 的参数烘焙成场景里的真实物体——
    //   水带 = 一张程序生成的贴图资产（Assets/Ivan/Art/Generated/*.png）+ Ribbon 子物体
    //   水纹 = 用组员 ambient_water 系列的 Sprite 子资产摆成 WaterGlyph 子物体
    // 烘焙结果全部是普通场景物体/资产，运行时不生成任何东西；
    // 参数改动后回来点一次「烘焙」即可重建（旧产物会被替换）。
    public class PaintedRiverBaker : EditorWindow
    {
        private const string k_generatedFolder = "Assets/Ivan/Art/Generated";
        private const string k_glyphAssetFolder = "Assets/DongbaPrototype/Resources/ApprovedArt";
        private const float k_pixelsPerMeter = 40f;
        private const int k_maxTextureEdge = 2048;

        private PaintedRiver m_target;
        private List<Sprite> m_glyphSprites;

        [MenuItem("Tools/Dongba/Diorama/Painted River Baker")]
        public static void Open()
        {
            var window = GetWindow<PaintedRiverBaker>("Painted River Baker");
            window.minSize = new Vector2(320f, 480f);
            window.AutoFollowSelection();
        }

        private void OnEnable()
        {
            // 打开窗口时若场景里正选中了河流，直接跟上
            if (m_target == null) AutoFollowSelection();
        }

        private void OnSelectionChange()
        {
            AutoFollowSelection();
            Repaint();
        }

        private void AutoFollowSelection()
        {
            var candidate = Selection.activeGameObject?.GetComponentInParent<PaintedRiver>();
            if (candidate != null) m_target = candidate;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "选中挂有 Painted River 的物体，调参数后点「烘焙」。\n" +
                "烘焙产物是场景真实物体（Ribbon + WaterGlyph）与贴图资产，运行时不生成；" +
                "改参数后需重新烘焙。横向/倾斜 = Flow Yaw 一个值。",
                MessageType.Info);

            m_target = (PaintedRiver)EditorGUILayout.ObjectField(
                new GUIContent("目标河流"), m_target, typeof(PaintedRiver), true);

            if (m_target == null)
            {
                EditorGUILayout.LabelField("场景里没有选中的 Painted River。");
                return;
            }

            var so = new SerializedObject(m_target);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("河道走向", EditorStyles.boldLabel);
            DrawField(so, "m_flowYawDegrees");
            DrawField(so, "m_length");
            DrawField(so, "m_bankHalfWidth");

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("蜿蜒", EditorStyles.boldLabel);
            DrawField(so, "m_meanderAmplitude");
            DrawField(so, "m_meanderWavelength");

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("外观", EditorStyles.boldLabel);
            DrawField(so, "m_waterColor");
            DrawField(so, "m_deepColor");
            DrawField(so, "m_bankStainColor");
            DrawField(so, "m_waterOpacity");
            DrawField(so, "m_edgeFeather");
            DrawField(so, "m_endFade");

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("水纹", EditorStyles.boldLabel);
            DrawField(so, "m_glyphSpacing");
            DrawField(so, "m_glyphTint");

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("层级与随机", EditorStyles.boldLabel);
            DrawField(so, "m_baseSortingOrder");
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawField(so, "m_seed");
                if (GUILayout.Button("重 Roll", GUILayout.Width(64f)))
                {
                    so.FindProperty("m_seed").intValue = Random.Range(int.MinValue, int.MaxValue);
                    GUI.FocusControl(null);
                }
            }

            so.ApplyModifiedProperties();

            EditorGUILayout.Space(10f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("烘焙（重建视觉）", GUILayout.Height(32f)))
                {
                    Bake();
                }
                if (GUILayout.Button("清除视觉", GUILayout.Height(32f), GUILayout.Width(96f)))
                {
                    ClearVisuals();
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                "提示：烘焙后可单独选中 WaterGlyph 微调位置；水带是生成的 PNG 资产，" +
                "想手改也可以直接编辑贴图。", EditorStyles.wordWrappedMiniLabel);
        }

        // ---- 烘焙主流程 ----

        private void Bake()
        {
            if (m_target == null) return;
            Transform root = m_target.transform;
            var so = new SerializedObject(m_target);
            so.Update();

            // 平贴地面（Euler 90）+ 世界朝向：横向/纵向/倾斜全由这一个角度决定
            float yaw = so.FindProperty("m_flowYawDegrees").floatValue;
            Undo.RecordObject(root, "Bake Painted River");
            root.rotation = Quaternion.Euler(90f, yaw, 0f);

            DestroyOldVisuals(root);
            BakeRibbon(so, root);
            BakeGlyphs(so, root);

            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Debug.Log($"[PaintedRiverBaker] 已烘焙 {m_target.name}。", m_target);
        }

        private void ClearVisuals()
        {
            if (m_target == null) return;
            DestroyOldVisuals(m_target.transform);
            EditorSceneManager.MarkSceneDirty(m_target.gameObject.scene);
        }

        private static void DestroyOldVisuals(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                // PaintedRiverVisuals 是旧版（运行时生成）留下的容器名，一并清掉
                if (child.name == PaintedRiver.RibbonChildName ||
                    child.name == "PaintedRiverVisuals" ||
                    child.name.StartsWith(PaintedRiver.GlyphChildPrefix))
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }
        }

        // ---- 水带贴图（与运行时 CenterlineLocalY 同一套公式）----

        private void BakeRibbon(SerializedObject so, Transform root)
        {
            float length = so.FindProperty("m_length").floatValue;
            float bank = so.FindProperty("m_bankHalfWidth").floatValue;
            float amplitude = so.FindProperty("m_meanderAmplitude").floatValue;
            float wavelength = so.FindProperty("m_meanderWavelength").floatValue;
            Color waterColor = so.FindProperty("m_waterColor").colorValue;
            Color deepColor = so.FindProperty("m_deepColor").colorValue;
            Color stainColor = so.FindProperty("m_bankStainColor").colorValue;
            float waterOpacity = so.FindProperty("m_waterOpacity").floatValue;
            float edgeFeather = so.FindProperty("m_edgeFeather").floatValue;
            float endFade = so.FindProperty("m_endFade").floatValue;
            int seed = so.FindProperty("m_seed").intValue;
            int sortingOrder = so.FindProperty("m_baseSortingOrder").intValue;

            float halfHeight = amplitude + bank + edgeFeather + 1.0f;
            int texW = Mathf.Clamp(Mathf.RoundToInt(length * k_pixelsPerMeter), 16, k_maxTextureEdge);
            int texH = Mathf.Clamp(Mathf.RoundToInt(halfHeight * 2f * k_pixelsPerMeter), 16, k_maxTextureEdge);
            float pixelsPerUnit = texW / Mathf.Max(0.01f, length);

            var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
            var pixels = new Color32[texW * texH];
            float safeWavelength = Mathf.Max(0.01f, wavelength);
            float phase = seed * 0.17f;    // 与 PaintedRiver.CenterlineLocalY 保持一致

            for (int y = 0; y < texH; y++)
            {
                float wy = (float)y / (texH - 1) * 2f * halfHeight - halfHeight;
                for (int x = 0; x < texW; x++)
                {
                    float wx = (float)x / (texW - 1) * length - length * 0.5f;
                    float center = Mathf.Sin(wx * Mathf.PI * 2f / safeWavelength + phase) * amplitude;

                    // 岸线沿流向呼吸（宽窄变化）+ 逐像素细颗粒
                    float bankHere = bank
                        + (Mathf.PerlinNoise(wx * 0.45f + seed * 0.31f, seed * 0.77f) - 0.5f) * 0.5f;
                    float d = Mathf.Abs(wy - center)
                        + (Mathf.PerlinNoise(wx * 2.2f + seed * 0.13f, wy * 2.2f + seed * 0.57f) - 0.5f) * 0.18f;

                    // 水面：河心到岸边羽化
                    float waterA = (1f - SmoothStep(bankHere * 0.55f, bankHere + edgeFeather, d)) * waterOpacity;

                    // 岸边深墨湿边（窄高斯带）
                    float edge = Mathf.Exp(-Mathf.Pow((d - bankHere * 0.92f) / 0.13f, 2f)) * 0.30f;

                    // 岸上棕色水渍（中心在岸线外 0.15m）
                    float endFadeFactor = EndFadeFactor(wx, length, endFade);
                    float stain = Mathf.Exp(-Mathf.Pow((d - bankHere - 0.15f) / 0.38f, 2f)) * 0.16f;
                    stain *= Mathf.Clamp01(1f - waterA * 2f);       // 水内不画棕渍
                    stain *= endFadeFactor;

                    // 流向拉长的条纹 + 细颗粒（顺着流向的明暗，读作流速）
                    float lum = 1f
                        + (Mathf.PerlinNoise(wx * 0.35f + seed * 0.91f, wy * 3.5f + seed * 0.41f) - 0.5f) * 0.10f
                        + (Mathf.PerlinNoise(wx * 6f + seed * 0.29f, wy * 6f + seed * 0.67f) - 0.5f) * 0.05f;

                    Color water = new Color(waterColor.r * lum, waterColor.g * lum, waterColor.b * lum);
                    Color deep = Color.Lerp(water, deepColor, Mathf.Clamp01(edge / 0.30f));

                    // 标准叠合：棕色水渍在底、水带盖面（alpha 覆盖并集）；
                    // 用 Color→Color32 隐式转换（自带 clamp），亮度超界不会回绕成暗色伪影
                    float waterAlpha = Mathf.Clamp01(waterA + edge * 0.5f) * endFadeFactor;
                    float alpha = Mathf.Clamp01(stain + waterAlpha * (1f - stain));
                    Color color = alpha < 1e-4f ? deep :
                        (stainColor * stain + deep * waterAlpha * (1f - stain)) / alpha;

                    pixels[y * texW + x] = new Color(color.r, color.g, color.b, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            // 存成贴图资产：场景里的 Ribbon 引用的是真资产，重开工程不丢
            EnsureGeneratedFolder();
            string assetPath = $"{k_generatedFolder}/PaintedRiver_{SanitizeFileName(root.name)}.png";
            File.WriteAllBytes(Path.GetFullPath(assetPath), tex.EncodeToPNG());
            DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            // 水平方向 1 像素 = 1/ppu 米，让精灵的世界宽度正好等于河道长度
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;     // 贴图本身已被钳在 2048，不会被二次缩小
            importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

            var ribbonObject = new GameObject(PaintedRiver.RibbonChildName);
            ribbonObject.transform.SetParent(root, false);
            var renderer = ribbonObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            // 本地 +z 映到世界 -y（向下），抬高用负 z：略高于地面防 z-fighting
            ribbonObject.transform.localPosition = new Vector3(0f, 0f, -0.02f);

            // 竖向被 2048 钳制时按目标高度补偿缩放
            float boundsY = texH / pixelsPerUnit;
            if (!Mathf.Approximately(boundsY, halfHeight * 2f))
            {
                ribbonObject.transform.localScale = new Vector3(1f, halfHeight * 2f / boundsY, 1f);
            }

            Undo.RegisterCreatedObjectUndo(ribbonObject, "Bake Painted River");
        }

        // ---- 水纹 glyph ----

        private void BakeGlyphs(SerializedObject so, Transform root)
        {
            var sprites = LoadGlyphSprites();
            if (sprites.Count == 0)
            {
                Debug.LogWarning("[PaintedRiverBaker] 找不到 ambient_water 水纹贴图，跳过水纹。", m_target);
                return;
            }

            float length = so.FindProperty("m_length").floatValue;
            float bank = so.FindProperty("m_bankHalfWidth").floatValue;
            float spacing = Mathf.Max(0.05f, so.FindProperty("m_glyphSpacing").floatValue);
            Color tint = so.FindProperty("m_glyphTint").colorValue;
            int sortingOrder = so.FindProperty("m_baseSortingOrder").intValue;
            int seed = so.FindProperty("m_seed").intValue;

            var rng = new System.Random(seed);
            int count = Mathf.Max(1, Mathf.FloorToInt(length / spacing));

            for (int i = 0; i < count; i++)
            {
                float x = -length * 0.5f + i * spacing + ((float)rng.NextDouble() - 0.5f) * spacing;
                float lateral = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Max(0f, bank - 0.25f);
                float yaw = (float)rng.NextDouble() * 16f - 8f;
                Sprite sprite = sprites[i % sprites.Count];

                var glyphObject = new GameObject($"{PaintedRiver.GlyphChildPrefix} {i}");
                glyphObject.transform.SetParent(root, false);
                var renderer = glyphObject.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = tint;
                renderer.sortingOrder = sortingOrder + 10;

                // 尺寸归一：世界宽度随机 0.85~1.6 米（组员原版配方）
                float worldWidth = 0.85f + (float)rng.NextDouble() * 0.75f;
                float scale = worldWidth / Mathf.Max(0.001f, sprite.bounds.size.x);
                glyphObject.transform.localScale = Vector3.one * scale;
                // 抬高一点压在水带上方（本地 +z 向下，取负）
                glyphObject.transform.localPosition = new Vector3(
                    x, m_target.CenterlineLocalY(x) + lateral, -0.035f);
                // 平面内旋转（父物体已平贴，local z 转即世界俯视旋转）
                glyphObject.transform.localRotation = Quaternion.Euler(0f, 0f, yaw);

                Undo.RegisterCreatedObjectUndo(glyphObject, "Bake Painted River");
            }
        }

        // 水纹精灵：组员 ambient_water_00~09 已按 Sprite(Multiple) 导入，直接取子精灵
        private List<Sprite> LoadGlyphSprites()
        {
            if (m_glyphSprites != null && m_glyphSprites.Count > 0) return m_glyphSprites;

            m_glyphSprites = new List<Sprite>();
            for (int i = 0; i < 10; i++)
            {
                string path = $"{k_glyphAssetFolder}/ambient_water_{i:00}.png";
                if (!File.Exists(Path.GetFullPath(path))) continue;
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<Sprite>()
                    .OrderBy(s => s.name)
                    .ToList();
                m_glyphSprites.AddRange(sprites);
            }
            return m_glyphSprites;
        }

        // ---- 工具 ----

        private static void DrawField(SerializedObject so, string propertyName)
        {
            var property = so.FindProperty(propertyName);
            if (property == null)
            {
                // 字段改名忘了同步窗口时的显式报错，而不是静默丢参数
                EditorGUILayout.LabelField($"缺失字段：{propertyName}");
                return;
            }
            EditorGUILayout.PropertyField(property, true);
        }

        private static float EndFadeFactor(float localX, float length, float endFade)
        {
            float half = length * 0.5f;
            float fade = Mathf.Max(0.05f, endFade);
            float fromStart = Mathf.Clamp01((localX + half) / fade);
            float toEnd = Mathf.Clamp01((half - localX) / fade);
            return SmoothStep(0f, 1f, fromStart) * SmoothStep(0f, 1f, toEnd);
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / Mathf.Max(1e-5f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.Select(c => invalid.Contains(c) ? '_' : c);
            return new string(chars.ToArray());
        }

        private static void EnsureGeneratedFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Ivan/Art"))
            {
                AssetDatabase.CreateFolder("Assets/Ivan", "Art");
            }
            if (!AssetDatabase.IsValidFolder(k_generatedFolder))
            {
                AssetDatabase.CreateFolder("Assets/Ivan/Art", "Generated");
            }
        }
    }
}
