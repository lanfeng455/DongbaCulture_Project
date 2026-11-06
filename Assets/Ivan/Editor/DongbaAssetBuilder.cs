using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// One-click build for all reusable Dongba art assets.
// Reads the raw glyph images from Art/Dongba/Raw, cuts the white background to alpha,
// crops to the glyph bounding box, configures sprites/materials, and saves prefabs so that
// any scene can simply drag prefabs in instead of rebuilding content by hand.
// Safe to re-run: processed textures are rebuilt and prefabs are updated in place.
public static class DongbaAssetBuilder
{
    private const string k_rawFolderPath = "Assets/Ivan/Art/Dongba/Raw";
    private const string k_dongbaArtFolderPath = "Assets/Ivan/Art/Dongba";
    private const string k_prefabFolderPath = "Assets/Ivan/Prefabs";

    // White-background removal: a pixel counts as paper only when its darkest channel is high.
    // Watercolor washes lean towards a color (one channel clearly lower), so they survive the cut.
    private const float k_whiteFullyTransparent = 0.96f;
    private const float k_whiteFullyOpaque = 0.90f;

    // Sprites are scaled by deriving pixels-per-unit from the cropped glyph height,
    // so a change in the raw image resolution never changes the in-world size.
    private const float k_renHeightMeters = 1.7f;
    private const float k_muHeightMeters = 2.6f;
    // The sun glyph is much wider than tall, so keep its height modest or it dominates the sky
    private const float k_riHeightMeters = 2f;
    private const float k_fuHeightMeters = 0.6f;
    private const float k_gunHeightMeters = 0.8f;

    // Ground watercolor paper repeats every 8 meters; tighter tiling made the wash pattern visibly repeat
    private const float k_groundTilingRepeatMeters = 8f;
    private const float k_groundSizeMeters = 24f;

    // Glyph ink colors. Textures are stored as white alpha masks, so the renderer or material
    // color becomes the visible ink color and each glyph type gets its own palette tone.
    private static readonly Color k_renColor = new Color(0.2f, 0.16f, 0.12f);
    private static readonly Color k_muColor = new Color(0.28f, 0.52f, 0.3f);
    private static readonly Color k_riColor = new Color(0.85f, 0.32f, 0.16f);
    private static readonly Color k_shuiColor = new Color(0.3f, 0.56f, 0.78f);
    private static readonly Color k_fuColor = new Color(0.25f, 0.2f, 0.16f);
    private static readonly Color k_gunColor = new Color(0.45f, 0.3f, 0.17f);

    [MenuItem("Tools/Dongba/Build Dongba Assets")]
    public static void BuildAll()
    {
        // Pick up the raw folder in case it was created outside the editor and never imported
        AssetDatabase.Refresh();
        EnsureFolders();

        Sprite renSprite = BuildGlyphSprite("dongba_ren", k_renHeightMeters);
        Sprite muSprite = BuildGlyphSprite("dongba_mu", k_muHeightMeters);
        Sprite riSprite = BuildGlyphSprite("dongba_ri", k_riHeightMeters);
        // Held glyphs pivot at the handle end: the anchor sits at the hand and the item
        // extends away from the body. A center pivot left half of the axe over the player.
        Sprite fuSprite = BuildGlyphSprite("dongba_fu", k_fuHeightMeters, new Vector2(0f, 0.5f));
        Sprite gunSprite = BuildGlyphSprite("dongba_gun", k_gunHeightMeters, new Vector2(0f, 0f));
        Sprite bubbleSprite = CreateBubbleSprite();

        // The water glyph is only used as a ground decal texture, never as a standing sprite
        Texture2D shuiTexture = BuildGlyphTexture("dongba_shui");
        Texture2D groundTexture = EnsureGroundTexture();

        Material groundMaterial = EnsureLitMaterial("M_Ground", groundTexture, Color.white,
            new Vector2(k_groundSizeMeters / k_groundTilingRepeatMeters, k_groundSizeMeters / k_groundTilingRepeatMeters), false);
        Material riverMaterial = EnsureLitMaterial("M_River", groundTexture, new Color(0.6f, 0.78f, 0.88f),
            new Vector2(2f, 1f), false);
        Material decalMaterial = EnsureLitMaterial("M_DecalShui", shuiTexture, k_shuiColor, Vector2.one, true);

        // Order matters: axe and stick exist before the player (hand) and tree (drops) reference them
        GameObject axePrefab = EnsureAxePrefab(fuSprite);
        GameObject stickPrefab = EnsureStickPrefab(gunSprite);
        EnsureTreePrefab(muSprite, stickPrefab);
        EnsureBillboardPrefab("Sun", riSprite, k_riColor);
        EnsurePlayerPrefab(renSprite, axePrefab, stickPrefab, fuSprite, gunSprite, bubbleSprite);
        EnsureRiverPrefab(riverMaterial, decalMaterial, shuiTexture);
        EnsureGroundDecalPrefab(decalMaterial, shuiTexture);

        AssetDatabase.SaveAssets();
        Debug.Log("[DongbaAssetBuilder] Dongba assets are ready. Prefabs live in Assets/Ivan/Prefabs.");
    }

    // Called by scene setup so missing assets are built automatically without an extra menu click
    public static void EnsureAssetsBuilt()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>($"{k_prefabFolderPath}/Player.prefab") == null)
        {
            BuildAll();
        }
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(k_prefabFolderPath))
        {
            AssetDatabase.CreateFolder("Assets/Ivan", "Prefabs");
        }
    }

    private static Sprite BuildGlyphSprite(string rawName, float targetHeightMeters)
    {
        return BuildGlyphSprite(rawName, targetHeightMeters, new Vector2(0.5f, 0f));
    }

    private static Sprite BuildGlyphSprite(string rawName, float targetHeightMeters, Vector2 pivot)
    {
        string spriteAssetPath = $"{k_dongbaArtFolderPath}/{rawName}.png";
        Texture2D cropped = ExtractGlyphPixels(rawName, out int contentWidth, out int contentHeight);

        File.WriteAllBytes(Path.GetFullPath(spriteAssetPath), cropped.EncodeToPNG());
        Object.DestroyImmediate(cropped);

        AssetDatabase.ImportAsset(spriteAssetPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(spriteAssetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        // Derive ppu from the cropped height so the glyph renders at a stable world size
        importer.spritePixelsPerUnit = contentHeight / targetHeightMeters;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

        // The pivot is ignored unless alignment is Custom (a centered pivot is why the tilted
        // billboards sank into the ground); in Unity 6 alignment lives in TextureImporterSettings.
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(spriteAssetPath);
    }

    private static Texture2D BuildGlyphTexture(string rawName)
    {
        string textureAssetPath = $"{k_dongbaArtFolderPath}/{rawName}.png";
        Texture2D cropped = ExtractGlyphPixels(rawName, out _, out _);

        File.WriteAllBytes(Path.GetFullPath(textureAssetPath), cropped.EncodeToPNG());
        Object.DestroyImmediate(cropped);

        AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);

        // Decal textures are sampled through a material, so keep mipmaps for a smooth tilted view
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(textureAssetPath);
        importer.textureType = TextureImporterType.Default;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
    }

    private static Texture2D EnsureGroundTexture()
    {
        string rawPath = FindRawImagePath("ground_watercolor");
        string textureAssetPath = $"{k_dongbaArtFolderPath}/ground_watercolor.jpg";

        // The ground needs no alpha cut; copying keeps the Raw folder as the single source of originals
        if (!File.Exists(Path.GetFullPath(textureAssetPath)))
        {
            File.Copy(Path.GetFullPath(rawPath), Path.GetFullPath(textureAssetPath));
        }

        AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(textureAssetPath);
        importer.textureType = TextureImporterType.Default;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
    }

    private static Sprite CreateBubbleSprite()
    {
        string spriteAssetPath = $"{k_dongbaArtFolderPath}/ui_bubble.png";

        const int size = 128;
        const float radius = size * 0.5f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - radius + 0.5f;
                float dy = y - radius + 0.5f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy) / radius;

                // Soft paper disc: opaque core, gentle alpha falloff, faint ink rim
                float alpha = Mathf.InverseLerp(1f, 0.82f, distance);
                float shade = distance > 0.78f && distance < 0.9f ? 0.82f : 1f;
                texture.SetPixel(x, y, new Color(shade, shade, shade, alpha));
            }
        }
        texture.Apply();

        File.WriteAllBytes(Path.GetFullPath(spriteAssetPath), texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(spriteAssetPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(spriteAssetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        // 128 pixels per unit: exactly 1 meter across at scale 1, so the UI sizes bubbles in meters
        importer.spritePixelsPerUnit = size;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(spriteAssetPath);
    }

    // Internal so DongbaGlyphSpriteBuilder can reuse the white-cut extraction
    // without duplicating it
    internal static Texture2D ExtractGlyphPixels(string rawName, out int contentWidth, out int contentHeight)
    {
        string rawPath = FindRawImagePath(rawName);
        byte[] bytes = File.ReadAllBytes(Path.GetFullPath(rawPath));

        // LoadImage fills the texture from encoded bytes, bypassing the "readable" import flag
        Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        ImageConversion.LoadImage(source, bytes);

        int width = source.width;
        int height = source.height;
        Color[] pixels = source.GetPixels();

        for (int i = 0; i < pixels.Length; i++)
        {
            Color pixel = pixels[i];
            float minChannel = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
            float alpha = Mathf.InverseLerp(k_whiteFullyTransparent, k_whiteFullyOpaque, minChannel);
            // Store a white alpha mask so tinting can recolor the ink; black pixels would stay black
            pixels[i] = new Color(1f, 1f, 1f, alpha);
        }

        // Crop to the ink bounding box so the sprite pivot and size describe the glyph, not the canvas
        FindContentBounds(pixels, width, height, out int minX, out int minY, out int maxX, out int maxY);

        contentWidth = maxX - minX + 1;
        contentHeight = maxY - minY + 1;

        Color[] croppedPixels = new Color[contentWidth * contentHeight];
        for (int y = 0; y < contentHeight; y++)
        {
            for (int x = 0; x < contentWidth; x++)
            {
                croppedPixels[y * contentWidth + x] = pixels[(minY + y) * width + (minX + x)];
            }
        }

        Texture2D cropped = new Texture2D(contentWidth, contentHeight, TextureFormat.RGBA32, false);
        cropped.SetPixels(croppedPixels);
        cropped.Apply();

        Object.DestroyImmediate(source);
        return cropped;
    }

    private static void FindContentBounds(Color[] pixels, int width, int height,
        out int minX, out int minY, out int maxX, out int maxY)
    {
        const int padding = 4;
        minX = width;
        minY = height;
        maxX = -1;
        maxY = -1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (pixels[y * width + x].a <= 0.02f)
                {
                    continue;
                }

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        // Fall back to the full canvas if the image somehow ended up empty
        if (maxX < 0)
        {
            minX = 0;
            minY = 0;
            maxX = width - 1;
            maxY = height - 1;
        }

        minX = Mathf.Max(0, minX - padding);
        minY = Mathf.Max(0, minY - padding);
        maxX = Mathf.Min(width - 1, maxX + padding);
        maxY = Mathf.Min(height - 1, maxY + padding);
    }

    private static string FindRawImagePath(string rawName)
    {
        string[] extensions = { ".jpg", ".png" };
        foreach (string extension in extensions)
        {
            string candidate = $"{k_rawFolderPath}/{rawName}{extension}";
            if (File.Exists(Path.GetFullPath(candidate)))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"[DongbaAssetBuilder] No raw image named '{rawName}' (.jpg/.png) in {k_rawFolderPath}.");
    }

    private static Material EnsureLitMaterial(string materialName, Texture2D baseMap, Color tint, Vector2 tiling, bool transparent)
    {
        string assetPath = $"{k_dongbaArtFolderPath}/{materialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, assetPath);
        }

        material.SetTexture("_BaseMap", baseMap);
        material.SetColor("_BaseColor", tint);
        material.SetTextureScale("_BaseMap", tiling);
        // Watercolor should stay matte; specular highlights would break the paper look
        material.SetFloat("_Smoothness", 0f);

        if (transparent)
        {
            material.SetFloat("_Surface", 1f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            // Decals sit almost coplanar with the ground, so depth writes would cause z-fighting
            material.SetInt("_ZWrite", 0);
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    // Re-creates or updates a prefab in place, so re-running the build upgrades existing prefabs
    // (new components, new colors) instead of silently skipping them.
    private static void UpdatePrefab(string prefabPath, string rootName, System.Action<GameObject> configure)
    {
        GameObject root;
        bool loadedFromDisk = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
        if (loadedFromDisk)
        {
            root = PrefabUtility.LoadPrefabContents(prefabPath);
        }
        else
        {
            root = new GameObject(rootName);
        }

        configure(root);

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

        if (loadedFromDisk)
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        else
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void EnsurePlayerPrefab(Sprite renSprite, GameObject axePrefab, GameObject stickPrefab,
        Sprite fuSprite, Sprite gunSprite, Sprite bubbleSprite)
    {
        UpdatePrefab($"{k_prefabFolderPath}/Player.prefab", "Player",
            player => ConfigurePlayer(player, renSprite, axePrefab, stickPrefab, fuSprite, gunSprite, bubbleSprite));
    }

    private static void ConfigurePlayer(GameObject player, Sprite renSprite, GameObject axePrefab,
        GameObject stickPrefab, Sprite fuSprite, Sprite gunSprite, Sprite bubbleSprite)
    {
        CharacterController controller = EnsureComponent<CharacterController>(player);
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.stepOffset = 0.2f;

        EnsureComponent<PlayerMov>(player);
        EnsureComponent<PlayerInventory>(player);

        ToolEquip toolEquip = EnsureComponent<ToolEquip>(player);
        SetReference(toolEquip, "m_axePrefab", axePrefab);
        SetReference(toolEquip, "m_stickPrefab", stickPrefab);

        BackpackUI backpackUI = EnsureComponent<BackpackUI>(player);
        SetReference(backpackUI, "m_bubbleSprite", bubbleSprite);
        SetReference(backpackUI, "m_axeSprite", fuSprite);
        SetReference(backpackUI, "m_stickSprite", gunSprite);

        // The sprite lives on a child so billboard rotation and breathing never touch the collider capsule
        GameObject visual = FindOrCreateChild(player.transform, "Visual");
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = Vector3.one;

        SpriteRenderer spriteRenderer = EnsureComponent<SpriteRenderer>(visual);
        spriteRenderer.sprite = renSprite;
        spriteRenderer.color = k_renColor;
        EnsureComponent<Billboard>(visual);
        EnsureComponent<Breathing>(visual);

        // Hand anchor for the equipped tool; ToolEquip repositions it onto the facing side every frame
        GameObject toolAnchor = FindOrCreateChild(player.transform, "ToolAnchor");
        toolAnchor.transform.localPosition = Vector3.zero;
    }

    private static void EnsureBillboardPrefab(string prefabName, Sprite sprite, Color tint)
    {
        UpdatePrefab($"{k_prefabFolderPath}/{prefabName}.prefab", prefabName,
            root => ConfigureBillboardProp(root, sprite, tint));
    }

    private static void ConfigureBillboardProp(GameObject prop, Sprite sprite, Color tint)
    {
        SpriteRenderer spriteRenderer = EnsureComponent<SpriteRenderer>(prop);
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = tint;
        EnsureComponent<Billboard>(prop);
    }

    private static GameObject EnsureAxePrefab(Sprite fuSprite)
    {
        string prefabPath = $"{k_prefabFolderPath}/Axe.prefab";
        UpdatePrefab(prefabPath, "Axe", root =>
        {
            EnsureComponent<Billboard>(root);

            // The blade rotates around its own origin during a swing; the sprite's bottom-center
            // pivot makes that origin the grip, so the axe chops around the handle
            GameObject blade = FindOrCreateChild(root.transform, "Blade");
            blade.transform.localPosition = Vector3.zero;
            blade.transform.localRotation = Quaternion.identity;
            blade.transform.localScale = Vector3.one;

            SpriteRenderer spriteRenderer = EnsureComponent<SpriteRenderer>(blade);
            spriteRenderer.sprite = fuSprite;
            spriteRenderer.color = k_fuColor;
        });
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    private static GameObject EnsureStickPrefab(Sprite gunSprite)
    {
        string prefabPath = $"{k_prefabFolderPath}/Stick.prefab";
        UpdatePrefab(prefabPath, "Stick", root =>
        {
            SpriteRenderer spriteRenderer = EnsureComponent<SpriteRenderer>(root);
            spriteRenderer.sprite = gunSprite;
            spriteRenderer.color = k_gunColor;
            EnsureComponent<Billboard>(root);

            // Generous trigger so simply walking close is enough to pick the stick up
            SphereCollider pickupCollider = EnsureComponent<SphereCollider>(root);
            pickupCollider.isTrigger = true;
            pickupCollider.radius = 0.6f;
            pickupCollider.center = new Vector3(0f, 0.2f, 0f);

            EnsureComponent<StickPickup>(root);
        });
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    private static void EnsureTreePrefab(Sprite muSprite, GameObject stickPrefab)
    {
        UpdatePrefab($"{k_prefabFolderPath}/Tree.prefab", "Tree", root => ConfigureTree(root, muSprite, stickPrefab));
    }

    private static void ConfigureTree(GameObject root, Sprite muSprite, GameObject stickPrefab)
    {
        // Older trees carried the sprite on the root; visuals now live on a child so hit
        // feedback (punch) and idle motion (breathing) never fight over the same transform
        RemoveComponent<SpriteRenderer>(root);
        RemoveComponent<Billboard>(root);

        GameObject visual = FindOrCreateChild(root.transform, "Visual");
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        SpriteRenderer spriteRenderer = EnsureComponent<SpriteRenderer>(visual);
        spriteRenderer.sprite = muSprite;
        spriteRenderer.color = k_muColor;
        EnsureComponent<Billboard>(visual);
        EnsureComponent<Breathing>(root);

        // A trigger keeps the player walk-through friendly while letting axe swings find the trunk
        SphereCollider trunkCollider = EnsureComponent<SphereCollider>(root);
        trunkCollider.isTrigger = true;
        trunkCollider.radius = 0.5f;
        trunkCollider.center = new Vector3(0f, 1f, 0f);

        TreeHealth health = EnsureComponent<TreeHealth>(root);
        SetReference(health, "m_stickPrefab", stickPrefab);
    }

    private static void EnsureRiverPrefab(Material riverMaterial, Material decalMaterial, Texture2D shuiTexture)
    {
        string prefabPath = $"{k_prefabFolderPath}/River.prefab";
        // Fully derived from current assets (decal aspect changes with new glyphs), so rebuild from scratch
        AssetDatabase.DeleteAsset(prefabPath);

        UpdatePrefab(prefabPath, "River", river =>
        {
            // A stretched ground-colored quad tinted blue reads as water without a dedicated water texture
            CreateGroundQuad("Water", riverMaterial, new Vector3(0f, 0.01f, 0f), new Vector3(6f, 2f, 1f), 0f)
                .transform.SetParent(river.transform, false);

            GameObject glyphs = new GameObject("Glyphs");
            glyphs.transform.SetParent(river.transform, false);

            // Scatter a few water glyphs along the riverbed; yaw variation keeps them from looking stamped
            CreateGroundQuad("Glyph_Shui", decalMaterial, new Vector3(-1.6f, 0.02f, 0f), GlyphQuadScale(shuiTexture, 1.5f), 15f)
                .transform.SetParent(glyphs.transform, false);
            CreateGroundQuad("Glyph_Shui", decalMaterial, new Vector3(0.2f, 0.02f, 0.2f), GlyphQuadScale(shuiTexture, 1.5f), -10f)
                .transform.SetParent(glyphs.transform, false);
            CreateGroundQuad("Glyph_Shui", decalMaterial, new Vector3(1.8f, 0.02f, -0.15f), GlyphQuadScale(shuiTexture, 1.5f), 25f)
                .transform.SetParent(glyphs.transform, false);
        });
    }

    private static void EnsureGroundDecalPrefab(Material decalMaterial, Texture2D shuiTexture)
    {
        string prefabPath = $"{k_prefabFolderPath}/GroundDecal.prefab";
        AssetDatabase.DeleteAsset(prefabPath);

        UpdatePrefab(prefabPath, "GroundDecal", decal =>
        {
            // The quad lives on a child so the root is free to carry positioning only
            CreateGroundQuad("Quad", decalMaterial, new Vector3(0f, 0.02f, 0f), GlyphQuadScale(shuiTexture, 1.5f), 0f)
                .transform.SetParent(decal.transform, false);
        });
    }

    private static GameObject CreateGroundQuad(string name, Material material, Vector3 localPosition, Vector3 localScale, float yawDegrees)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.localPosition = localPosition;
        // Rotating 90 degrees on X turns the quad's front face upwards so it lies flat on the ground
        quad.transform.localEulerAngles = new Vector3(90f, yawDegrees, 0f);
        quad.transform.localScale = localScale;
        quad.GetComponent<MeshRenderer>().sharedMaterial = material;

        // Flat decals and water never need collision
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        return quad;
    }

    private static Vector3 GlyphQuadScale(Texture2D glyphTexture, float targetWidthMeters)
    {
        // Keep the glyph's native aspect ratio so the wide water glyph is not stretched on a square quad
        float aspect = (float)glyphTexture.width / glyphTexture.height;
        return new Vector3(targetWidthMeters, targetWidthMeters / aspect, 1f);
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
        {
            component = target.AddComponent<T>();
        }
        return component;
    }

    private static void RemoveComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
        {
            Object.DestroyImmediate(component);
        }
    }

    private static GameObject FindOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
        {
            return child.gameObject;
        }

        GameObject created = new GameObject(name);
        created.transform.SetParent(parent, false);
        return created;
    }

    // Serialized fields stay private, so cross-prefab references are wired through SerializedObject
    private static void SetReference(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
