#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
namespace DongbaPrototype {public static class CharacterPresentationVerification {
 public static void Run(){EditorSceneManager.OpenScene("Assets/DongbaPrototype/ObliqueForest.unity");EditorApplication.isPlaying=true;}
}}
#endif
