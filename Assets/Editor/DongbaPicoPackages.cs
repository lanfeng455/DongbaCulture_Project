using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace DongbaXR.Editor
{
    public static class DongbaPicoPackages
    {
        static AddRequest request;
        static double deadline;
        static AddAndRemoveRequest migration;

        public static void PrepareRokidRemoval()
        {
            migration = Client.AddAndRemove(new[] { "com.unity.xr.arfoundation@6.6.2", "com.unity.xr.legacyinputhelpers@3.0.1" });
            deadline = EditorApplication.timeSinceStartup + 600;
            EditorApplication.update += FinishMigration;
        }
        static void FinishMigration()
        {
            if (!migration.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= FinishMigration;
            bool ok = migration.IsCompleted && migration.Status == StatusCode.Success;
            if (ok) Debug.Log("[DongbaPICO] Shared dependencies preserved; embedded Rokid packages can be removed.");
            else Debug.LogError(migration.Error?.message ?? "Package migration timed out");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        [MenuItem("Dongba/PICO Ultra/Install downloaded PICO SDK")]
        public static void Install()
        {
            request = Client.Add("https://github.com/Pico-Developer/PICO-Unity-Integration-SDK.git#c93a59bab88d337fe0d54140499712897cedbe52");
            deadline = EditorApplication.timeSinceStartup + 600;
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (!request.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= Poll;
            bool success = request.IsCompleted && request.Status == StatusCode.Success;
            if (success) Debug.Log("[DongbaPICO] Installed " + request.Result.name + " " + request.Result.version);
            else Debug.LogError("[DongbaPICO] SDK installation failed: " + (request.Error?.message ?? "timeout"));
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
