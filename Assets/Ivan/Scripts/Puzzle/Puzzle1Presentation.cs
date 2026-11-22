using System.Collections.Generic;
using DongbaPrototype;
using Ivan.Diorama;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ivan.Puzzle
{
    // Scene-owned gate and guidance. Completion remains owned by Puzzle1Director.
    [RequireComponent(typeof(Puzzle1Director))]
    public sealed class Puzzle1Presentation : MonoBehaviour
    {
        [SerializeField] float m_exitX = 95f;
        [SerializeField] float m_hintStartX = 49f;
        static readonly List<Puzzle1Presentation> Gates = new();
        Puzzle1Director director;
        DioramaPlayerController player;
        BoxCollider exitCollider;
        CanvasGroup hints;
        ReelPortalHost portal;
        Text title, instruction, rule, controls;
        readonly List<CaveLight> caveLights = new();
        int lastState = -1, languageRevision = -1;
        public float ExitX => m_exitX;
        public bool Locked => director != null && !director.IsCompleted;
        public float HintAlpha => hints == null ? 0 : hints.alpha;
        public string Instruction => instruction == null ? "" : instruction.text;

        sealed class CaveLight
        {
            public CaveSlot cave;
            public SpriteRenderer rim;
            public float phase;
        }

        void Awake() { director = GetComponent<Puzzle1Director>(); }
        void OnEnable() { if (!Gates.Contains(this)) Gates.Add(this); }
        void OnDisable() { Gates.Remove(this); }
        void OnDestroy() { if (director != null) director.PuzzleCompleted -= OpenExit; }

        public static bool Blocks(Scene scene, Vector3 position, float radius)
        {
            // Also prevents going around the wall or visually jumping over it.
            foreach (var gate in Gates)
                if (gate != null && gate.gameObject.scene == scene && gate.Locked &&
                    position.x > gate.m_exitX - radius) return true;
            return false;
        }

        void Start()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (player == null) player = root.GetComponentInChildren<DioramaPlayerController>();
                foreach (var cave in root.GetComponentsInChildren<CaveSlot>()) AddCaveLight(cave);
            }
            var wall = new GameObject("Puzzle exit - opens only on completion");
            wall.transform.SetParent(transform, false);
            wall.transform.position = new Vector3(m_exitX + .2f, 2f, player == null ? 0 : player.BoundCenter.z);
            exitCollider = wall.AddComponent<BoxCollider>();
            exitCollider.size = new Vector3(.4f, 8f, player == null ? 60f : 2f * (player.HalfDepth + 4f));
            director.PuzzleCompleted += OpenExit;
            if (director.IsCompleted) OpenExit();
            BuildHints();
            portal = FindFirstObjectByType<ReelPortalHost>();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--puzzle-guidance-check") >= 0)
                gameObject.AddComponent<Puzzle1GuidanceCheck>();
        }

        void OpenExit() { if (exitCollider != null) exitCollider.enabled = false; }

        void BuildHints()
        {
            var root = new GameObject("Puzzle guidance", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            hints = root.GetComponent<CanvasGroup>();
            hints.alpha = 0; hints.blocksRaycasts = false; hints.interactable = false;
            var card = DongbaUIStyle.Rect("Clue", root.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -26), new Vector2(458, 182));
            DongbaUIStyle.Plate(card, new Color(1, 1, 1, .94f));
            title = DongbaUIStyle.Label("Stage", card, "", new Vector2(20, -14), new Vector2(418, 32), 22);
            title.color = DongbaUIStyle.Accent;
            instruction = DongbaUIStyle.Label("Current clue", card, "", new Vector2(20, -53), new Vector2(418, 66), 22);
            rule = DongbaUIStyle.Label("White high black low", card, "", new Vector2(20, -133), new Vector2(418, 34), 24);
            rule.fontStyle = FontStyle.Bold;
            var strip = DongbaUIStyle.Rect("Controls", root.transform, Vector2.zero, Vector2.zero, new Vector2(30, 24), new Vector2(890, 43));
            DongbaUIStyle.Plate(strip, new Color(1, 1, 1, .91f));
            controls = DongbaUIStyle.Label("Keys", strip, "", new Vector2(16, -8), new Vector2(858, 30), 19);
        }

        void AddCaveLight(CaveSlot cave)
        {
            SpriteRenderer original = null;
            foreach (var renderer in cave.GetComponentsInChildren<SpriteRenderer>())
                if (renderer.sprite != null && !renderer.name.ToLowerInvariant().Contains("shadow") &&
                    (original == null || renderer.sortingOrder > original.sortingOrder)) original = renderer;
            if (original == null) return;
            var rim = new GameObject("Cave breathing ink edge").AddComponent<SpriteRenderer>();
            rim.transform.SetParent(original.transform, false);
            rim.sprite = original.sprite;
            rim.sharedMaterial = original.sharedMaterial;
            rim.sortingLayerID = original.sortingLayerID;
            rim.sortingOrder = original.sortingOrder - 1;
            rim.color = Color.clear;
            caveLights.Add(new CaveLight { cave = cave, rim = rim, phase = cave.RequiredSide == SnakeSide.White ? 0 : 1.3f });
        }

        void Update()
        {
            if (player == null || hints == null) return;
            bool dialogue = DialogueDisplay.Instance != null && DialogueDisplay.Instance.IsPlaying;
            bool shown = player.transform.position.x >= m_hintStartX &&
                         player.transform.position.x < m_exitX + 6f &&
                         !dialogue && !DongbaInterface.GameplayBlocked &&
                         (portal == null || !portal.PresentationTransitionActive);
            hints.alpha = Mathf.MoveTowards(hints.alpha, shown ? 1 : 0, Time.unscaledDeltaTime * 7);
            int state = director.IsCompleted ? 3 : !director.HasSplit ? 0 : director.Stage == 2 ? 2 : 1;
            if (state != lastState || languageRevision != DongbaLanguage.Revision)
            {
                lastState = state; languageRevision = DongbaLanguage.Revision;
                bool zh = DongbaLanguage.Chinese;
                title.text = zh ? (state == 3 ? "山路已开" : "两蛇相望") : (state == 3 ? "THE WAY IS OPEN" : "THE TWO SNAKES");
                instruction.text = state switch
                {
                    0 => zh ? "靠近两蛇，看看是什么拦住了山路。" : "Approach the snakes blocking the mountain path.",
                    1 => zh ? "他们仍盯着彼此，无法推动。\n试着用红石隔开他们的视线。" : "They cannot be moved while staring.\nPush a red rock between their eyes.",
                    2 => zh ? "趁他们看不见彼此，推向各自的洞。\n移动时也要保持视线被挡住。" : "Push each snake into its own cave.\nKeep their line of sight blocked.",
                    _ => zh ? "两蛇已经归位，可以继续向东了。" : "Both snakes are home. Continue east."
                };
                rule.text = zh ? "白蛇归高处　·　黑蛇归低处" : "WHITE ABOVE  /  BLACK BELOW";
                controls.text = zh ? "WASD / 方向键  移动　·　贴近推动石头与蛇　·　Enter  对话　·　Esc  菜单"
                    : "WASD / Arrows  Move   ·   Walk into rocks / snakes to push   ·   Enter  Dialogue   ·   Esc  Menu";
            }
            foreach (var light in caveLights)
            {
                if (light.rim == null) continue;
                float pulse = .5f + .5f * Mathf.Sin(Time.time * 2.1f + light.phase);
                bool active = director.HasSplit && !director.IsCompleted;
                // A soft edge of the existing pictograph, never a replacement glyph or a beacon.
                light.rim.transform.localScale = Vector3.one * (1.045f + pulse * .035f);
                Color tint = light.cave.RequiredSide == SnakeSide.White
                    ? new Color(.92f, .69f, .28f) : new Color(.39f, .57f, .68f);
                tint.a = active ? .15f + .2f * pulse : 0;
                light.rim.color = tint;
            }
        }
    }
}
