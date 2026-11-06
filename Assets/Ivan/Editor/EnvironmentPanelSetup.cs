using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// One-click creation of the PanelSettings asset required by EnvironmentPanel's UIDocument.
// PanelSettings must be created through the editor so its internal shader references
// are wired up correctly — a hand-written .asset file would miss them.
public static class EnvironmentPanelSetup
{
    private const string k_panelSettingsPath = "Assets/Ivan/UI/EnvironmentPanelSettings.asset";

    [MenuItem("Tools/Environment/Create Panel Settings")]
    public static void CreatePanelSettings()
    {
        PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(k_panelSettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            AssetDatabase.CreateAsset(settings, k_panelSettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[EnvironmentPanelSetup] PanelSettings created at {k_panelSettingsPath}. Assign it to the UIDocument's Panel Settings field.");
        }

        Selection.activeObject = settings;
    }
}
