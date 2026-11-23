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
        Text title, instruction, controls;
        DialogueDisplay dialogueDisplay;
        DialogueLibrarySO clueLibrary;
        readonly List<CaveLight> caveLights = new();
        int learnedClues, languageRevision = -1;
        const int HeightClue = 1, GazeClue = 2;
        public float ExitX => m_exitX;
        public bool Locked => director != null && !director.IsCompleted;
        public float HintAlpha => hints == null ? 0 : hints.alpha;
        public string Instruction => instruction == null ? "" : instruction.text;
        public int LearnedClueCount => ((learnedClues & HeightClue) != 0 ? 1 : 0) + ((learnedClues & GazeClue) != 0 ? 1 : 0);

        sealed class CaveLight
        {
            public CaveSlot cave;
            public SpriteRenderer rim;
            public float phase;
        }

        void Awake() { director = GetComponent<Puzzle1Director>(); }
        void OnEnable() { if (!Gates.Contains(this)) Gates.Add(this); }
        void OnDisable() { Gates.Remove(this); }
        void OnDestroy()
        {
            if (director != null) director.PuzzleCompleted -= OpenExit;
            if (dialogueDisplay != null) dialogueDisplay.LibraryLineCompleted -= OnLineCompleted;
        }

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
                if (dialogueDisplay == null) dialogueDisplay = root.GetComponentInChildren<DialogueDisplay>(true);
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
            if (dialogueDisplay != null)
            {
                clueLibrary = dialogueDisplay.Library;
                dialogueDisplay.LibraryLineCompleted += OnLineCompleted;
                RefreshLearnedClues();
            }
            portal = FindFirstObjectByType<ReelPortalHost>();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--puzzle-guidance-check") >= 0)
                gameObject.AddComponent<Puzzle1GuidanceCheck>();
        }

        void OpenExit() { if (exitCollider != null) exitCollider.enabled = false; }

        void OnLineCompleted(DialogueLibrarySO library, int index)
        {
            if (library == clueLibrary && (index == 26 || index == 35)) RefreshLearnedClues();
        }

        void RefreshLearnedClues()
        {
            learnedClues = 0;
            if (dialogueDisplay.HasCompletedLibraryLine(clueLibrary, 26)) learnedClues |= HeightClue;
            if (dialogueDisplay.HasCompletedLibraryLine(clueLibrary, 35)) learnedClues |= GazeClue;
            languageRevision = -1;
        }

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
            var card = DongbaUIStyle.Rect("Clue", root.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -26), new Vector2(374, 124));
            DongbaUIStyle.Plate(card, new Color(1, 1, 1, .78f));
            title = DongbaUIStyle.Label("Remembered words", card, "", new Vector2(18, -12), new Vector2(338, 26), 16);
            title.color = DongbaUIStyle.Muted;
            instruction = DongbaUIStyle.Label("Learned clues", card, "", new Vector2(18, -43), new Vector2(338, 68), 20);
            var strip = DongbaUIStyle.Rect("Controls", root.transform, Vector2.zero, Vector2.zero, new Vector2(30, 24), new Vector2(580, 43));
            DongbaUIStyle.Plate(strip, new Color(1, 1, 1, .91f));
            controls = DongbaUIStyle.Label("Keys", strip, "", new Vector2(16, -8), new Vector2(548, 30), 18);
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
            // A completed range can still have a lingering/fading subtitle on screen.
            bool dialogue = dialogueDisplay != null && (dialogueDisplay.IsPlaying ||
                (dialogueDisplay.SubtitlePanel != null && dialogueDisplay.SubtitlePanel.gameObject.activeInHierarchy));
            bool shown = learnedClues != 0 && !director.IsCompleted && player.transform.position.x >= m_hintStartX &&
                         player.transform.position.x < m_exitX + 6f &&
                         !dialogue && !DongbaInterface.GameplayBlocked &&
                         (portal == null || !portal.PresentationTransitionActive);
            hints.alpha = Mathf.MoveTowards(hints.alpha, shown ? 1 : 0, Time.unscaledDeltaTime * 7);
            if (languageRevision != DongbaLanguage.Revision)
            {
                languageRevision = DongbaLanguage.Revision;
                bool zh = DongbaLanguage.Chinese;
                title.text = zh ? "记下的话" : "REMEMBERED WORDS";
                instruction.text = (learnedClues & HeightClue) != 0 ? (zh ? "白归高处，黑归低处。" : "White above, black below.") : "";
                if ((learnedClues & GazeClue) != 0)
                    instruction.text += (instruction.text.Length > 0 ? "\n" : "") + (zh ? "两蛇始终相望。" : "Neither snake looks away.");
                controls.text = zh ? "WASD / 方向键  移动　·　Enter  对话　·　Esc  菜单"
                    : "WASD / Arrows  Move   ·   Enter  Dialogue   ·   Esc  Menu";
            }
            foreach (var light in caveLights)
            {
                if (light.rim == null) continue;
                float pulse = .5f + .5f * Mathf.Sin(Time.time * 2.1f + light.phase);
                bool active = (learnedClues & HeightClue) != 0 && director.HasSplit && !director.IsCompleted;
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
