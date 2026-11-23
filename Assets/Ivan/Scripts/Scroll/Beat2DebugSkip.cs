using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ivan.Diorama;

namespace Ivan.Scroll
{
    // Beat_2 内部剧情跳过调试：F6 触发。
    // 效果：屏幕快速渐暗 → 卷轴导演以更快速度自动向右走到终点 → 途中所有绊线/Sequence 的 Event 步照常触发
    // （对话全部跳过）→ 到终点后渐亮显形。
    //
    // 工作流程：
    //   1. Beat2DebugSkipTrigger.Update() 按 F6 → StartSkip()
    //   2. StartSkip() 盖全屏黑色遮罩（0.3s 渐暗）
    //   3. 遮罩盖满后：强制 ScrollPlayDirector 切 Auto 模式 + 更快速度
    //   4. 绊线/Sequence 读到 Beat2DebugSkip.IsSkipping=true → Fire() 但不等对话/Wait，Event 步照常 Invoke
    //   5. ScrollPlayDirector.HasFinished 变 true → 渐亮 0.4s → 销毁遮罩
    public static class Beat2DebugSkip
    {
        public static bool IsSkipping { get; private set; }
        private static Coroutine s_activeRoutine;

        private const float k_fadeInDuration = 0.3f;   // 渐暗遮罩盖满的时长
        private const float k_fadeOutDuration = 0.4f;  // 走到终点后渐亮显形的时长
        private const float k_skipWalkSpeed = 18f;     // 跳过时自动行走速度（比正常 4 m/s 快 4.5 倍）

        public static void Start()
        {
            if (IsSkipping) return;
            IsSkipping = true;
            Debug.Log("[Beat2DebugSkip] 剧情跳过已开启 — 快速自动走到终点，事件照常触发，对话全部跳过。");

            var runner = SkipRunner.Instance;
            if (runner != null) s_activeRoutine = runner.StartCoroutine(CoSkip());
        }

        public static void Stop()
        {
            if (!IsSkipping) return;
            IsSkipping = false;
            if (s_activeRoutine != null)
            {
                var runner = SkipRunner.Instance;
                if (runner != null) runner.StopCoroutine(s_activeRoutine);
                s_activeRoutine = null;
            }
            // 如果遮罩还在，立即销毁
            if (SkipRunner.Instance != null) SkipRunner.Instance.RemoveFadeOverlay();
            Debug.Log("[Beat2DebugSkip] 剧情跳过已关闭。");
        }

        private static IEnumerator CoSkip()
        {
            // —— 1. 渐暗 0.3s ——
            var overlay = SkipRunner.Instance.CreateFadeOverlay();
            float age = 0f;
            while (age < k_fadeInDuration)
            {
                age += Time.unscaledDeltaTime;
                overlay.SetAlpha(Mathf.Clamp01(age / k_fadeInDuration));
                yield return null;
            }

            // —— 2. 计算玩家起点和卷轴终点 ——
            var director = Object.FindFirstObjectByType<ScrollPlayDirector>();
            var player = Object.FindFirstObjectByType<DioramaPlayerController>();
            float playerX = player != null ? player.transform.position.x : 0f;
            float endX = director != null ? director.EndX : playerX + 40f;

            // —— 3. 按区间手动触发事件 ——
            TriggerAllStoryEvents(playerX, endX);

            // 给 Event 步协程尾帧时间跑完
            yield return null; yield return null; yield return null;

            // —— 4. 瞬移到终点（清所有移动锁）——
            TeleportToScrollEnd();
            yield return null;

            // 终点在墨纸覆盖区内：先把纸揭开再渐亮，否则玩家会站在被纸盖住的黑区里
            var curtain = Object.FindFirstObjectByType<InkRevealCurtain>();
            if (curtain != null && curtain.IsCovering)
            {
                curtain.Reveal();
                float wait = 0f;
                while (curtain != null && curtain.IsCovering && wait < 6f)
                {
                    wait += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            // —— 5. 渐亮 0.4s ——
            age = 0f;
            while (age < k_fadeOutDuration)
            {
                age += Time.unscaledDeltaTime;
                overlay.SetAlpha(1f - Mathf.Clamp01(age / k_fadeOutDuration));
                yield return null;
            }

            SkipRunner.Instance.RemoveFadeOverlay();
            IsSkipping = false;
            s_activeRoutine = null;
        }

        // 按玩家当前位置 → 卷轴终点顺序，手动触发区间内所有未触发的 Tripwire + Sequence。
        // Beat2DebugSkip.IsSkipping=true 让它们正常 Fire() 但跳过对话/Wait 步，Event 步照常 Invoke。
        // 玩家已走过的（x<=playerX 或 HasFired=true）和终点之后的（x>endX）都不触发。
        private static void TriggerAllStoryEvents(float playerX, float endX)
        {
            var director = Object.FindFirstObjectByType<ScrollPlayDirector>();

            // 收集所有带位置的事件：Tripwire 用 transform.position.x；Sequence CrossX 用 transform.x，
            // EnterTrigger 用 BoxCollider.bounds.center.x（更准）
            var events = new List<(float x, System.Action fire)>();

            foreach (var t in Object.FindObjectsByType<StoryTripwire>(FindObjectsSortMode.None))
            {
                if (t.HasFired) continue;
                events.Add((t.transform.position.x, () => t.Fire()));
            }

            foreach (var s in Object.FindObjectsByType<StorySequencePlayer>(FindObjectsSortMode.None))
            {
                if (s.HasFired) continue;
                float sx = s.transform.position.x;
                if (s.Trigger == StorySequencePlayer.TriggerKind.EnterTrigger)
                {
                    var col = s.GetComponent<Collider>();
                    if (col != null) sx = col.bounds.center.x;
                }
                events.Add((sx, () => s.Fire()));
            }

            // 按 x 升序，只触发 [playerX, endX] 区间内的
            events.Sort((a, b) => a.x.CompareTo(b.x));
            int triggered = 0;
            foreach (var ev in events)
            {
                if (ev.x <= playerX) continue;   // 玩家已走过
                if (ev.x > endX) break;          // 终点之后的事件不提前触发
                ev.fire();
                triggered++;
            }

            Debug.Log($"[Beat2DebugSkip] 触发了 {triggered}/{events.Count} 个区间内事件 (playerX={playerX:F2}, endX={endX:F2})");
        }

        // 把玩家瞬移到卷轴终点（ScrollPlayDirector.m_endX 位置），
        // 解锁任何演出/谜题期间可能残留的移动锁。
        private static void TeleportToScrollEnd()
        {
            var director = Object.FindFirstObjectByType<ScrollPlayDirector>();
            var player = Object.FindFirstObjectByType<DioramaPlayerController>();
            if (player == null) return;

            // 清掉任何演出锁定（SetStageMovementLock / SetAutoMoveInput 都清）
            player.SetStageMovementLock(false);
            player.ClearAutoMoveInput();

            // 瞬移：z 保持当前 z（卷轴玩法只沿 x 卷动），y 保持当前 y
            Vector3 pos = player.transform.position;
            float endX = director != null ? director.EndX : pos.x + 40f;  // 没导演就默认右移 40
            pos.x = Mathf.Max(pos.x, endX);   // 已经在终点右边就不动
            player.transform.position = pos;

            // 顺便让 ScrollPlayDirector 也知道"已经到终点了"
            if (director != null) director.MarkFinished();
        }
    }

    // 极简 Coroutine 宿主 + 渐暗遮罩管理器。
    // 不依赖任何现有组件（避免生命周期竞态），DontDestroyOnLoad 防止场景切换时中断协程。
    internal class SkipRunner : MonoBehaviour
    {
        private static SkipRunner s_instance;
        public static SkipRunner Instance
        {
            get
            {
                if (s_instance == null)
                {
                    var go = new GameObject("Beat2DebugSkipRunner");
                    DontDestroyOnLoad(go);
                    s_instance = go.AddComponent<SkipRunner>();
                }
                return s_instance;
            }
        }

        private Canvas m_fadeCanvas;
        private UnityEngine.UI.Image m_fadeImage;

        // 盖一个全屏黑色 Overlay Canvas（sortingOrder = 99999，最上层）
        public FadeOverlayHandle CreateFadeOverlay()
        {
            var canvasGo = new GameObject("Beat2SkipFadeOverlay");
            DontDestroyOnLoad(canvasGo);
            m_fadeCanvas = canvasGo.AddComponent<Canvas>();
            m_fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            m_fadeCanvas.sortingOrder = 99999;

            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(3840f, 2160f);
            scaler.matchWidthOrHeight = 0.5f;

            var imageGo = new GameObject("Fade", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            imageGo.transform.SetParent(canvasGo.transform, false);
            var rect = (RectTransform)imageGo.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            m_fadeImage = imageGo.GetComponent<UnityEngine.UI.Image>();
            m_fadeImage.color = Color.black;
            m_fadeImage.raycastTarget = true;   // 吃掉所有点击，防止跳过期间误触

            return new FadeOverlayHandle(this);
        }

        public void RemoveFadeOverlay()
        {
            if (m_fadeCanvas != null)
            {
                Destroy(m_fadeCanvas.gameObject);
                m_fadeCanvas = null;
                m_fadeImage = null;
            }
        }

        internal void SetOverlayAlpha(float alpha)
        {
            if (m_fadeImage != null)
            {
                var c = m_fadeImage.color;
                c.a = alpha;
                m_fadeImage.color = c;
            }
        }
    }

    // 给协程用的轻量句柄，避免直接暴露 SkipRunner 内部状态
    internal readonly struct FadeOverlayHandle
    {
        private readonly SkipRunner m_runner;
        public FadeOverlayHandle(SkipRunner runner) => m_runner = runner;
        public void SetAlpha(float a) => m_runner?.SetOverlayAlpha(a);
    }
}
