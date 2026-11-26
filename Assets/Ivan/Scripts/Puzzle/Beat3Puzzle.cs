using System;
using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;
using TMPro;

using Ivan.Diorama;

namespace Ivan.Puzzle
{
    // Beat_3《狐狸》：中英文题目 Canvas + 黑白两块可推字牌。
    //   1. 题目 Canvas 淡入，显示中英文题目；解释（白 / 黑两个字）先隐藏；
    //   2. 场景里的 Dictionary 白 / Dictionary 黑 可被玩家推走；
    //   3. 白推进左边的判断区、黑推进右边的判断区后谜题完成：
    //      两个解释慢慢淡入、英文字幕改成完整句，随后由 m_onCompleted 接后续剧情。
    //
    // 判定不读 Canvas 文字：解释文字（白 / 黑）本身就摆在题目两个空格上，
    // 运行时直接以它们的世界坐标在地面高度生成两个球形触发区，
    // 复用 Beat4 的 Beat4TriggerZone，谁推进来按刚体身份判定。
    //
    // 入位后不锁定字牌，玩家可以继续推。每帧检查两块是否同时在各自槽内，
    // 同时成立才触发完成演出。
    //
    // 当前阶段：进入 Beat_3 自动开始谜题（跳过狐狸对话）。后续对接剧情时
    // 取消勾选 m_autoStart，把 StartPuzzle 绑到对话结束的 Event 步即可。
    public class Beat3Puzzle : PuzzleDirectorBase
    {
        private const string k_canvasName = "题目 Canvas";
        private const string k_whiteRootName = "Dictionary 白";
        private const string k_blackRootName = "Dictionary 黑";
        private const string k_whiteExplanationName = "解释白中文";
        private const string k_blackExplanationName = "解释黑中文";
        private const string k_englishTextName = "题目英文";

        [Header("自动开始（对接剧情前先用它跳过对话）")]
        [Tooltip("勾选：进入场景立即淡入 Canvas 并开始谜题；对接剧情时取消勾选，" +
                 "Canvas 与黑白字牌会先隐藏，由对话结束的 Event 步调用 StartPuzzle 拉起")]
        [SerializeField] private bool m_autoStart = true;

        [Header("完成后的英文字幕")]
        [Tooltip("谜题完成后替换「题目英文」的完整句")]
        [SerializeField] private string m_englishCompletedText =
            "All WHITE things go up, all BLACK things go down.";

        [Header("节奏")]
        [Tooltip("题目 Canvas 淡入时长（秒）")]
        [SerializeField] private float m_canvasFadeDuration = 0.8f;
        [Tooltip("完成后两个解释慢慢淡入的时长（秒）")]
        [SerializeField] private float m_explanationFadeDuration = 2.5f;
        [Tooltip("谜题完成后 Canvas 渐出时长（秒）。0 = 由剧情 Event 步手动调 FadeOutCanvas")]
        [SerializeField] private float m_canvasFadeOutDuration = 1.2f;

        [Header("判断区")]
        [Tooltip("判断区球形触发区的半径（米）")]
        [SerializeField] private float m_slotRadius = 1.2f;
        [Tooltip("判断区沿 Z 相对解释文字的偏移（米）。解释文字的世界坐标是固定的，" +
                 "判断区默认就摆在它正下方的地面；推不到 / 位置不合适时在这里微调")]
        [SerializeField] private float m_slotZOffset = 0f;

        [Header("完成事件（由 PuzzleDirectorBase 的 m_onCompleted 接后续剧情）")]
        [Tooltip("白字牌第一次入位时触发（可选）")]
        [SerializeField] private UnityEvent m_onWhitePlaced = new();
        [Tooltip("黑字牌第一次入位时触发（可选）")]
        [SerializeField] private UnityEvent m_onBlackPlaced = new();

        private const float k_groundHeight = 0f;   // 卷轴地面高度，字牌刚体锁在这一层

        private CanvasGroup m_canvasGroup;
        private CanvasGroup m_whiteExplanationFader;
        private CanvasGroup m_blackExplanationFader;
        private TMP_Text m_englishText;

        private Transform m_whiteRoot;
        private Transform m_blackRoot;
        private Transform m_whiteBody;   // 字牌的刚体所在（Glyph 子物体）
        private Transform m_blackBody;

        private Beat4TriggerZone m_whiteSlot;
        private Beat4TriggerZone m_blackSlot;
        private Func<Collider, bool> m_whiteSlotHandler;
        private Func<Collider, bool> m_blackSlotHandler;

        private Collider m_whiteCollider;   // 字牌真正的碰撞体（Obstacle Footprint 子物体上）
        private Collider m_blackCollider;

        private bool m_whiteWasPlaced;   // 第一次入位后不再重复触发事件
        private bool m_blackWasPlaced;
        private bool m_started;
        private bool m_completed;
        private bool m_hasFadedOut;     // 防止 FadeOutCanvas 被调两次（自动 + 手动）

        // 手动挂载：在 Beat_3 场景的 Diorama Scene Root 下建空物体挂本组件。
        // 运行时判断区 / 字牌高亮反馈在 Start 里自动初始化，不需要额外配置

        private void Start()
        {
            var canvas = FindTransform(k_canvasName);
            m_whiteRoot = FindTransform(k_whiteRootName);
            m_blackRoot = FindTransform(k_blackRootName);

            if (canvas == null || m_whiteRoot == null || m_blackRoot == null)
            {
                Debug.LogWarning("[Beat3Puzzle] 场景里缺少题目 Canvas 或 Dictionary 白 / 黑，谜题未启用。", this);
                enabled = false;
                return;
            }

            m_canvasGroup = canvas.GetComponent<CanvasGroup>();
            if (m_canvasGroup == null)
            {
                Debug.LogWarning("[Beat3Puzzle] 题目 Canvas 上没有 CanvasGroup，无法淡入淡出。", canvas);
            }

            m_englishText = FindText(canvas, k_englishTextName);
            m_whiteExplanationFader = HideExplanation(k_whiteExplanationName);
            m_blackExplanationFader = HideExplanation(k_blackExplanationName);

            m_whiteBody = FindBody(m_whiteRoot, k_whiteRootName);
            m_blackBody = FindBody(m_blackRoot, k_blackRootName);

            // 字牌上的 PushableRock 不需要高亮 / 音效反馈（那是石头专用）
            if (m_whiteBody != null)
            {
                var rock = m_whiteBody.GetComponent<PushableRock>();
                rock?.SetPushFeedback(false, false);
            }
            if (m_blackBody != null)
            {
                var rock = m_blackBody.GetComponent<PushableRock>();
                rock?.SetPushFeedback(false, false);
            }

            // 字牌真正的碰撞体在 DioramaObstacle 生成的 Obstacle Footprint 子物体上
            if (m_whiteBody != null) m_whiteCollider = m_whiteBody.GetComponentInChildren<Collider>(true);
            if (m_blackBody != null) m_blackCollider = m_blackBody.GetComponentInChildren<Collider>(true);

            if (m_whiteBody != null && m_blackBody != null)
            {
                CreateSlots();
            }

            if (m_autoStart)
            {
                StartPuzzle();
            }
            else
            {
                // 对接剧情模式：等对话结束的 Event 步来调 StartPuzzle
                if (m_canvasGroup != null) m_canvasGroup.alpha = 0f;
                m_whiteRoot.gameObject.SetActive(false);
                m_blackRoot.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (m_whiteSlot != null && m_whiteSlotHandler != null)
            {
                m_whiteSlot.Entered -= m_whiteSlotHandler;
            }
            if (m_blackSlot != null && m_blackSlotHandler != null)
            {
                m_blackSlot.Entered -= m_blackSlotHandler;
            }
        }

        private void Update()
        {
            if (!m_started || m_completed) return;

            // 每帧检查：两块是否同时在各自槽里。入位后不锁定，玩家可以继续推，
            // 只要同时在槽内就判定完成
            bool whiteIn = m_whiteCollider != null && m_whiteSlot != null &&
                           m_whiteSlot.IsInside(m_whiteCollider);
            bool blackIn = m_blackCollider != null && m_blackSlot != null &&
                           m_blackSlot.IsInside(m_blackCollider);

            // 第一次入位时触发可选事件（只触发一次）
            if (whiteIn && !m_whiteWasPlaced)
            {
                m_whiteWasPlaced = true;
                m_onWhitePlaced?.Invoke();
                Debug.Log("[Beat3Puzzle] 白字牌入位。", this);
            }
            if (blackIn && !m_blackWasPlaced)
            {
                m_blackWasPlaced = true;
                m_onBlackPlaced?.Invoke();
                Debug.Log("[Beat3Puzzle] 黑字牌入位。", this);
            }

            if (whiteIn && blackIn)
            {
                m_completed = true;
                CompleteSequence();
            }
        }

        // 对外入口：Canvas 淡入、黑白字牌出场、判断区开始工作。
        // 现在由 m_autoStart 自动调用；对接剧情后由对话 Event 步调用
        public void StartPuzzle()
        {
            if (m_started) return;
            m_started = true;

            if (m_canvasGroup != null)
            {
                m_canvasGroup.alpha = 0f;
                m_canvasGroup.DOFade(1f, m_canvasFadeDuration).SetEase(Ease.OutQuad);
            }

            if (m_whiteRoot != null) m_whiteRoot.gameObject.SetActive(true);
            if (m_blackRoot != null) m_blackRoot.gameObject.SetActive(true);

            Debug.Log("[Beat3Puzzle] 题目 Canvas 淡入，谜题开始。", this);
        }

        // ---------- 判断区 ----------

        private void CreateSlots()
        {
            Transform whiteExplanation = FindTransform(k_whiteExplanationName);
            Transform blackExplanation = FindTransform(k_blackExplanationName);
            if (whiteExplanation == null || blackExplanation == null)
            {
                Debug.LogWarning("[Beat3Puzzle] 找不到解释文字（解释白中文 / 解释黑中文），判断区未生成。", this);
                return;
            }

            m_whiteSlot = CreateSlot(GroundPoint(whiteExplanation.position), "Beat 3 Slot 白");
            m_whiteSlotHandler = other => HandleSlotEntered(other, m_whiteBody);
            m_whiteSlot.Entered += m_whiteSlotHandler;

            m_blackSlot = CreateSlot(GroundPoint(blackExplanation.position), "Beat 3 Slot 黑");
            m_blackSlotHandler = other => HandleSlotEntered(other, m_blackBody);
            m_blackSlot.Entered += m_blackSlotHandler;

            Debug.Log($"[Beat3Puzzle] 判断区生成：白槽 {m_whiteSlot.transform.position}，" +
                      $"黑槽 {m_blackSlot.transform.position}，半径 {m_slotRadius:F2} m。", this);
        }

        private Beat4TriggerZone CreateSlot(Vector3 center, string slotName)
        {
            var host = new GameObject(slotName);
            host.transform.position = center;

            var sphere = host.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = m_slotRadius;

            var zone = host.AddComponent<Beat4TriggerZone>();
            return zone;
        }

        // 第一次入位时触发——只是通知，不锁定字牌
        private bool HandleSlotEntered(Collider other, Transform expectedBody)
        {
            if (expectedBody == null) return false;

            var body = other.attachedRigidbody;
            if (body == null || body.transform != expectedBody) return false;

            return true;   // 接受这次触发，让 Beat4TriggerZone 记住
        }

        // ---------- 谜题完成 ----------

        private void CompleteSequence()
        {
            if (m_whiteExplanationFader != null)
            {
                m_whiteExplanationFader.DOFade(1f, m_explanationFadeDuration).SetEase(Ease.InQuad);
            }
            if (m_blackExplanationFader != null)
            {
                m_blackExplanationFader.DOFade(1f, m_explanationFadeDuration).SetEase(Ease.InQuad);
            }
            if (m_englishText != null)
            {
                m_englishText.text = m_englishCompletedText;
            }

            // Canvas 渐出：等解释淡入完再触发，让玩家看清"白山黑水"四个字后再消失。
            // 黑/白字牌跟 Canvas 一起淡出（先 DOFade 隐藏，再 SetActive(false)）
            if (m_canvasFadeOutDuration > 0f)
            {
                float delay = Mathf.Max(m_explanationFadeDuration, 2f);
                DOVirtual.DelayedCall(delay, FadeOutCanvas);
            }
            else
            {
                // 手动渐出模式：只记个标记，FadeOutCanvas 里统一处理
            }

            CompletePuzzle();
            Debug.Log("[Beat3Puzzle] 黑白同时入位，谜题完成，解释淡入。", this);
        }

        // Canvas 渐出：给 StorySequencePlayer Event 步用的公共入口。
        // 黑/白字牌也同步淡出后隐藏。
        public void FadeOutCanvas()
        {
            if (m_hasFadedOut) return;
            m_hasFadedOut = true;

            float dur = m_canvasFadeOutDuration > 0f ? m_canvasFadeOutDuration : 1.2f;

            if (m_canvasGroup != null)
            {
                m_canvasGroup.DOFade(0f, dur).SetEase(Ease.OutQuad);
            }

            if (m_whiteRoot != null)
            {
                var sr = m_whiteRoot.GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    Color c = sr.color;
                    sr.DOColor(new Color(c.r, c.g, c.b, 0f), dur).SetEase(Ease.OutQuad)
                      .OnComplete(() => m_whiteRoot.gameObject.SetActive(false));
                }
                else
                {
                    DOVirtual.DelayedCall(dur, () => m_whiteRoot.gameObject.SetActive(false));
                }
            }
            if (m_blackRoot != null)
            {
                var sr = m_blackRoot.GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    Color c = sr.color;
                    sr.DOColor(new Color(c.r, c.g, c.b, 0f), dur).SetEase(Ease.OutQuad)
                      .OnComplete(() => m_blackRoot.gameObject.SetActive(false));
                }
                else
                {
                    DOVirtual.DelayedCall(dur, () => m_blackRoot.gameObject.SetActive(false));
                }
            }
        }

        // ---------- 工具 ----------

        private Vector3 GroundPoint(Vector3 explanationPosition)
        {
            return new Vector3(explanationPosition.x, k_groundHeight,
                               explanationPosition.z + m_slotZOffset);
        }

        private static Transform FindBody(Transform root, string rootName)
        {
            var rigid = root.GetComponentInChildren<Rigidbody>(true);
            if (rigid == null)
            {
                Debug.LogWarning($"[Beat3Puzzle] 「{rootName}」层级里没有 Rigidbody，" +
                                 "这棵字牌推不了也判不了。", root);
                return null;
            }
            return rigid.transform;
        }

        private CanvasGroup HideExplanation(string explanationName)
        {
            Transform explanation = FindTransform(explanationName);
            if (explanation == null)
            {
                Debug.LogWarning($"[Beat3Puzzle] 场景里没有「{explanationName}」，对应解释不会显示。", this);
                return null;
            }

            var fader = explanation.GetComponent<CanvasGroup>();
            if (fader == null)
            {
                fader = explanation.gameObject.AddComponent<CanvasGroup>();
            }
            fader.alpha = 0f;
            return fader;
        }

        private static TMP_Text FindText(Transform canvas, string textName)
        {
            foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.gameObject.name == textName) return text;
            }
            return null;
        }

        private static Transform FindTransform(string name)
        {
            var go = GameObject.Find(name);
            return go != null ? go.transform : null;
        }
    }
}
