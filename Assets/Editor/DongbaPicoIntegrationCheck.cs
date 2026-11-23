using UnityEditor;
using UnityEditor.SceneManagement;
namespace DongbaXR.Editor {
    public static class DongbaPicoIntegrationCheck {
        public static void Run(){
            var scenes=EditorBuildSettings.scenes;
            DongbaPicoSetup.ConfigureLoader();
            if(scenes.Length!=EditorBuildSettings.scenes.Length)throw new System.Exception("PICO changed desktop scene list");
            EditorSceneManager.OpenScene(DongbaPicoSetup.ScenePath);
            EditorApplication.isPlaying=true;
        }
    }
}
