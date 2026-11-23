using System;
using System.Collections;
using Ivan.Diorama;
using Ivan.Scroll;
using Unity.Cinemachine;
using UnityEngine;

namespace Ivan.Puzzle
{
    // Solving the snakes opens a route. Leaving remains a separate, player-driven action.
    [DisallowMultipleComponent, RequireComponent(typeof(Puzzle1Director))]
    public sealed class Puzzle1ExitJourney : MonoBehaviour
    {
        [SerializeField] GameObject m_upperStone;
        [SerializeField] GameObject m_lowerStone;
        [SerializeField] InkRevealCurtain m_curtain;
        [SerializeField] float m_exitX = 101f;
        [SerializeField] float m_pathCenterZ = 1.45f;
        [SerializeField] float m_pathHalfWidth = 1.95f;
        [SerializeField] float m_panDuration = 1.25f;
        [SerializeField] float m_stoneMoveDuration = 1.8f;
        [SerializeField] float m_returnDuration = 1.15f;

        Puzzle1Director director;
        DioramaPlayerController player;
        ScrollPlayDirector scroll;
        Camera view;
        CinemachineBrain brain;
        bool brainWasEnabled, cameraOwned, playerCameraOwned, movementOwned, priorCameraHandoff, started, crossedFromInside;
        Vector3 returnCameraPosition;
        Quaternion returnCameraRotation;
        float returnCameraZoom;
        Vector3 upperStart, lowerStart;
        Coroutine opening;

        public event Action ExitReached;
        public bool ExitReady { get; private set; }
        public bool HasExited { get; private set; }
        public bool IsRevealing => started && !ExitReady;
        public float ExitX => m_exitX;
        public Vector3 ExitPosition => new Vector3(m_exitX, 0f, m_pathCenterZ);
        public float StoneOpeningProgress { get; private set; }
        public float UpperStoneTravel => m_upperStone == null ? 0 : Vector3.Distance(upperStart, m_upperStone.transform.position);
        public float LowerStoneTravel => m_lowerStone == null ? 0 : Vector3.Distance(lowerStart, m_lowerStone.transform.position);

        void Awake()
        {
            director = GetComponent<Puzzle1Director>();
            director.PuzzleCompleted += BeginOpening;
        }

        void Start()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (player == null) player = root.GetComponentInChildren<DioramaPlayerController>();
                if (scroll == null) scroll = root.GetComponentInChildren<ScrollPlayDirector>();
                if (view == null) view = root.GetComponentInChildren<Camera>();
                if (m_curtain == null) m_curtain = root.GetComponentInChildren<InkRevealCurtain>();
            }
            if (scroll != null) scroll.DisableAutomaticCompletionWalk();
            if (director.IsCompleted) BeginOpening();
        }

        void BeginOpening()
        {
            if (started) return;
            started = true;
            opening = StartCoroutine(OpenRoute());
        }

        IEnumerator OpenRoute()
        {
            // Wait for the final push/dialogue to finish before borrowing its movement lock.
            yield return null;
            while (DialogueDisplay.Instance != null && DialogueDisplay.Instance.IsPlaying) yield return null;
            if (scroll != null) scroll.SetMode(ScrollPlayDirector.PlayMode.Manual);
            if (player != null)
            {
                player.ClearAutoMoveInput();
                player.SetStageMovementLock(true);
                movementOwned = true;
                priorCameraHandoff = player.CameraHandoff;
                player.CameraHandoff = true;
                playerCameraOwned = true;
            }
            if (view != null)
            {
                returnCameraPosition = view.transform.position;
                returnCameraRotation = view.transform.rotation;
                returnCameraZoom = view.orthographicSize;
                brain = view.GetComponent<CinemachineBrain>();
                brainWasEnabled = brain != null && brain.enabled;
                if (brain != null) brain.enabled = false;
                cameraOwned = true;

                // Keep the actual camera's pitch and zoom; slide its ground focus to the gate.
                var ray = view.ViewportPointToRay(new Vector3(.5f, .5f, 0));
                var plane = new Plane(Vector3.up, Vector3.zero);
                Vector3 focus = player != null ? player.transform.position : new Vector3(70, 0, 0);
                if (plane.Raycast(ray, out float distance)) focus = ray.GetPoint(distance);
                float gateX = m_upperStone != null && m_lowerStone != null
                    ? (m_upperStone.transform.position.x + m_lowerStone.transform.position.x) * .5f : m_exitX - 4.7f;
                var gate = new Vector3(gateX, focus.y, m_pathCenterZ);
                yield return Pan(returnCameraPosition, returnCameraPosition + gate - focus, m_panDuration);
            }

            upperStart = m_upperStone == null ? Vector3.zero : m_upperStone.transform.position;
            lowerStart = m_lowerStone == null ? Vector3.zero : m_lowerStone.transform.position;
            if (m_curtain != null) m_curtain.Reveal();
            float age = 0;
            while (age < m_stoneMoveDuration)
            {
                age += Time.deltaTime;
                float t = Smooth(age / m_stoneMoveDuration);
                StoneOpeningProgress = t;
                // Move the original glyphs, including their obstacle footprints, to the sides.
                if (m_upperStone != null) m_upperStone.transform.position = upperStart + new Vector3(.5f, 0, 5f) * t;
                if (m_lowerStone != null) m_lowerStone.transform.position = lowerStart + new Vector3(.5f, 0, -5f) * t;
                yield return null;
            }
            StoneOpeningProgress = 1;
            while (m_curtain != null && m_curtain.IsCovering) yield return null;
            yield return new WaitForSeconds(.35f);
            if (view != null) yield return Pan(view.transform.position, returnCameraPosition, m_returnDuration);
            RestoreCamera();
            ReleaseMovement();
            ExitReady = true;
            crossedFromInside = player != null && player.transform.position.x < m_exitX;
            opening = null;
        }

        IEnumerator Pan(Vector3 from, Vector3 to, float duration)
        {
            for (float age = 0; age < duration; age += Time.deltaTime)
            {
                if (view != null) view.transform.position = Vector3.LerpUnclamped(from, to, Smooth(age / duration));
                yield return null;
            }
            if (view != null) view.transform.position = to;
        }

        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * t * (t * (t * 6 - 15) + 10); }

        void Update()
        {
            if (!ExitReady || HasExited || player == null || !director.IsCompleted) return;
            Vector3 feet = player.transform.position;
            if (feet.x < m_exitX) crossedFromInside = true;
            // Do not auto-leave on solve, from a debug teleport outside, or by skirting the road.
            if (!crossedFromInside || feet.x < m_exitX || Mathf.Abs(feet.z - m_pathCenterZ) > m_pathHalfWidth) return;
            HasExited = true;
            ExitReached?.Invoke();
        }

        void RestoreCamera()
        {
            if (cameraOwned && view != null)
            {
                view.transform.SetPositionAndRotation(returnCameraPosition, returnCameraRotation);
                view.orthographicSize = returnCameraZoom;
            }
            if (cameraOwned && brain != null) brain.enabled = brainWasEnabled;
            if (playerCameraOwned && player != null) player.CameraHandoff = priorCameraHandoff;
            cameraOwned = false;
            playerCameraOwned = false;
        }

        void ReleaseMovement() { if (movementOwned && player != null) player.SetStageMovementLock(false); movementOwned = false; }

        void OnDisable()
        {
            if (opening != null) { StopCoroutine(opening); opening = null; }
            RestoreCamera();
            ReleaseMovement();
        }

        void OnDestroy() { if (director != null) director.PuzzleCompleted -= BeginOpening; }
    }
}
