using UnityEditor;
using UnityEditor.SceneManagement;
namespace DongbaXR.Editor
{
    public static class DongbaFirstAxeCheck
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/PicoMR/DongbaPicoMR.unity");
            EditorApplication.isPlaying = true;
        }
    }
}
