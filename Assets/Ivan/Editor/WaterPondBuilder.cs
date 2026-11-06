using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Creates the pond building blocks: a unit disc mesh (radius 0.5, vertex alpha fading
// to 0 at the rim so the edge stays soft) and the reusable 水面 prefab using
// M_DioramaWater. Placement is manual on purpose: drag the prefab into a room, then
// move/scale it (non-uniform X/Z is fine) until the waterline sits right — the
// shader's depth-based shoreline blends it into the basin slopes automatically.
// Safe to re-run: mesh and prefab are updated in place, existing scene instances
// keep their position and just pick up the rebuilt mesh.
public static class WaterPondBuilder
{
    private const string k_meshAssetPath = "Assets/Ivan/Model/WaterPond.asset";
    private const string k_prefabPath = "Assets/Ivan/Prefabs/水面.prefab";
    private const string k_waterMaterialPath = "Assets/Ivan/Art/M_DioramaWater.mat";
    private const string k_pondName = "水面";

    private const int k_segments = 48;
    // Inner rings stay opaque; the outermost ring carries alpha 0 for the soft rim
    private static readonly float[] k_ringFractions = { 0.55f, 0.85f, 1.0f };

    [MenuItem("Tools/Dongba/Build Water Pond")]
    public static void BuildWaterPond()
    {
        Mesh mesh = BuildOrUpdateDiscMesh();

        Material waterMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_waterMaterialPath);
        if (waterMaterial == null)
        {
            Debug.LogError($"[WaterPondBuilder] Missing material at {k_waterMaterialPath}.");
            return;
        }

        GameObject prefab = BuildOrUpdatePrefab(mesh, waterMaterial);
        EnsureSceneInstance(prefab);

        AssetDatabase.SaveAssets();
        Debug.Log("[WaterPondBuilder] Pond mesh and prefab are ready. Drag 水面 into a room " +
            "and move/scale it to fit the basin; the shoreline blends itself.");
    }

    private static Mesh BuildOrUpdateDiscMesh()
    {
        // Update in place so existing prefab/scene references survive the rebuild
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(k_meshAssetPath);
        bool isNew = mesh == null;
        if (isNew)
        {
            mesh = new Mesh { name = "WaterPond" };
        }

        var verts = new System.Collections.Generic.List<Vector3> { Vector3.zero };
        var colors = new System.Collections.Generic.List<Color32> { new Color32(255, 255, 255, 255) };
        int ringCount = k_ringFractions.Length;

        for (int ring = 0; ring < ringCount; ring++)
        {
            byte alpha = ring == ringCount - 1 ? (byte)0 : (byte)255;
            float radius = 0.5f * k_ringFractions[ring];
            for (int seg = 0; seg < k_segments; seg++)
            {
                float angle = seg * Mathf.PI * 2f / k_segments;
                verts.Add(new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
                colors.Add(new Color32(255, 255, 255, alpha));
            }
        }

        var triangles = new System.Collections.Generic.List<int>();
        for (int seg = 0; seg < k_segments; seg++)
        {
            int next = (seg + 1) % k_segments;
            // Wound so the disc faces +Y; getting this wrong leaves the normals
            // pointing down and the surface renders black
            triangles.Add(0);
            triangles.Add(1 + next);
            triangles.Add(1 + seg);

            for (int ring = 0; ring < ringCount - 1; ring++)
            {
                int inner = 1 + ring * k_segments;
                int outer = 1 + (ring + 1) * k_segments;
                triangles.Add(inner + seg);
                triangles.Add(outer + next);
                triangles.Add(outer + seg);
                triangles.Add(inner + seg);
                triangles.Add(inner + next);
                triangles.Add(outer + next);
            }
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        if (isNew)
        {
            AssetDatabase.CreateAsset(mesh, k_meshAssetPath);
        }
        else
        {
            EditorUtility.SetDirty(mesh);
        }
        return mesh;
    }

    private static GameObject BuildOrUpdatePrefab(Mesh mesh, Material waterMaterial)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(k_prefabPath);
        if (root == null)
        {
            root = new GameObject(k_pondName);
        }

        MeshFilter filter = root.GetComponent<MeshFilter>();
        if (filter == null)
        {
            filter = root.AddComponent<MeshFilter>();
        }
        filter.sharedMesh = mesh;

        MeshRenderer renderer = root.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = root.AddComponent<MeshRenderer>();
        }
        renderer.sharedMaterial = waterMaterial;
        // Water receives shadows from sticks and rocks but never casts its own
        renderer.shadowCastingMode = ShadowCastingMode.Off;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, k_prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        return prefab;
    }

    private static void EnsureSceneInstance(GameObject prefab)
    {
        // A hand-placed instance belongs to the user: only drop one in when none exists
        GameObject existing = GameObject.Find(k_pondName);
        if (existing != null)
        {
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        GameObject room = GameObject.Find("Room_A");
        if (room != null)
        {
            instance.transform.SetParent(room.transform, false);
            instance.transform.localPosition = Vector3.zero;
        }
        Undo.RegisterCreatedObjectUndo(instance, "Build Water Pond");
        Selection.activeGameObject = instance;
    }
}
