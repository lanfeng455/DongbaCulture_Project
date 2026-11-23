using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

using DG.Tweening;

using DongbaPrototype;
using Ivan.Diorama;
using Ivan.Scroll;

namespace Ivan.Puzzle
{
    // Beat_4《野猪与老熊》：一个组件统管全关。
    //   1. 斧头一进板栗的触发区就劈开（接触触发，不按任何键）；
    //   2. 劈开节拍：果实掉落 → 斧头弹飞 + 扬尘 → 角色后退 → 果实膨胀 → 裂成两瓣；
    //   3. 两瓣分别推给野猪 / 老熊：进各自的触发区就算送到，随后由巨兽"随身带走"；
    //   4. 还没拿到食物的巨兽会后退蓄力、依次冲撞板栗树（真正抵达锚点才给树反馈）；
    //   5. 玩家进入未喂食巨兽的攻击范围会被连续撞退，撞退优先于冲撞节奏（防跳关）；
    //   6. 拿到食物的巨兽不再撞人，只带着食物让到场地外沿；
    //   7. 金粮：两只巨兽都让路后，场景里的 StorySequencePlayer（X 轴绊线）播 Level 2 对话，
    //      再由它的 Event 步调用 DeliverGrain，把场景里预置的金粮推到玩家面前。
    //
    // 对话全部走 DialogueDisplay.PlayRange + Level 2 对话资产；绊线走 StorySequencePlayer；
    // 巨兽转向走 StageActor。本组件只负责劈果、冲撞、撞退、送食、推金粮这些"游戏逻辑"。
    //
    // 判定全部走 Trigger Enter（Beat4TriggerZone），不做逐帧距离比较。
    //
    // 挂载：无需手动挂载，载入 Beat_4 时 Boot() 自动装配到场景根。
    // 撞树锚点：场景里名为「Beat 4 Bump Anchor 野猪 / 熊」的空物体，可在编辑器里直接拖。
    public class Beat4Puzzle : MonoBehaviour
    {
        private const string k_sceneKeyword = "Beat_4";
        private const string k_axeRootName = "Dictionary Axe 斧头";
        private const string k_nutRootName = "Dictionary Entity 板栗大";
        private const string k_boarName = "Dictionary Entity 野猪";
        private const string k_bearName = "Dictionary Entity 熊";
        private const string k_treeName = "Dictionary Tree 板栗树";
        private const string k_boarAnchorName = "Beat 4 Bump Anchor 野猪";
        private const string k_bearAnchorName = "Beat 4 Bump Anchor 熊";
        private const string k_staleTriggerName = "StorySequencePlayer";
        private const string k_glyphName = "Glyph";

        [Header("触发球余量（米）")]
        [Tooltip("板栗触发球比板栗实体碰撞体大出来的半径。必须大于 0，" +
                 "否则斧头会被实体碰撞体顶在触发区外面")]
        [SerializeField] private float m_nutTriggerMargin = 0.9f;
        [Tooltip("巨兽触发球比巨兽实体碰撞体大出来的半径")]
        [SerializeField] private float m_deliveryTriggerMargin = 1.2f;

        [Header("斧头劈板栗的节拍")]
        [Tooltip("果实掉落前先被顶起的高度（米）")]
        [SerializeField] private float m_nutDropHeight = 0.85f;
        [Tooltip("果实下落时间（秒）")]
        [SerializeField] private float m_nutDropDuration = 0.18f;
        [Tooltip("果实膨胀的幅度（相对缩放）。膨胀时长必须短于 0.5 秒")]
        [SerializeField] private float m_nutInflateAmount = 0.3f;
        [Tooltip("果实膨胀时长（秒），需求要求小于 0.5 秒")]
        [SerializeField] private float m_nutInflateDuration = 0.26f;
        [Tooltip("膨胀结束后等待多久才裂成两瓣（秒）")]
        [SerializeField] private float m_nutSplitDelay = 0.7f;
        [Tooltip("斧头被弹飞的初速度（米/秒）")]
        [SerializeField] private float m_axeKnockSpeed = 7f;

        [Header("劈开后的两瓣")]
        [Tooltip("裂出来的两瓣相对原板栗的缩放")]
        [SerializeField] private float m_halfScale = 0.72f;
        [Tooltip("两瓣沿场地纵深（Z）分开的距离（米）")]
        [SerializeField] private float m_halfSpacing = 0.45f;

        [Header("让路（拿到食物之后）")]
        [Tooltip("巨兽带着食物往外让开的距离（米）")]
        [SerializeField] private float m_beastSideOffset = 2.5f;
        [Tooltip("巨兽走出去的移动速度（米/秒）")]
        [SerializeField] private float m_beastMoveSpeed = 1.5f;

        [Header("撞树节拍（没拿到食物时的待机表现）")]
        [Tooltip("后退蓄力时，沿「树 → 自己」方向退开的距离（米）")]
        [SerializeField] private float m_backOffDistance = 1.6f;
        [Tooltip("后退速度（米/秒）")]
        [SerializeField] private float m_backOffSpeed = 2.2f;
        [Tooltip("冲撞速度（米/秒）")]
        [SerializeField] private float m_dashSpeed = 13f;
        [Tooltip("撞到树后停顿多久（秒）")]
        [SerializeField] private float m_impactHoldDuration = 0.12f;
        [Tooltip("撞完缓缓退回蓄力位的速度（米/秒）")]
        [SerializeField] private float m_recoverSpeed = 1.6f;
        [Tooltip("两只巨兽两次冲撞之间的间隔（秒）")]
        [SerializeField] private float m_chargeIdleDuration = 0.5f;
        [Tooltip("找不到锚点物体时，默认停在离树心多远的距离（米）")]
        [SerializeField] private float m_contactDistance = 1.6f;

        [Header("树受撞反馈（只动视觉子物体，不碰树本身）")]
        [Tooltip("撞树时视觉的抖动幅度（米）")]
        [SerializeField] private float m_treePunch = 0.18f;
        [Tooltip("抖动时长（秒）")]
        [SerializeField] private float m_treePunchDuration = 0.4f;

        [Header("撞退（防跳关）")]
        [Tooltip("未喂食巨兽的攻击半径（米），玩家进入就被撞回去")]
        [SerializeField] private float m_threatRadius = 2.4f;
        [Tooltip("撞退初速度（米/秒）")]
        [SerializeField] private float m_knockSpeed = 7.5f;
        [Tooltip("撞退持续时间（秒），越长滑得越远、越容易看清")]
        [SerializeField] private float m_knockDuration = 0.34f;
        [Tooltip("两次撞退之间的冷却（秒）")]
        [SerializeField] private float m_knockCooldown = 0.6f;

        [Header("空气墙（场景实物，两只巨兽让路后关闭）")]
        [Tooltip("场景里已挂好 BoxCollider 的空气墙物体名；这里只负责在解谜后关掉它")]
        [SerializeField] private string m_airWallName = "Beat 4 Air Wall";

        [Header("金粮交付（由 StorySequencePlayer 的 Event 步调用）")]
        [Tooltip("场景里预先摆好的金粮物体名；默认未激活，交付时激活并推到玩家面前")]
        [SerializeField] private string m_grainName = "Beat 4 Golden Grain";
        [Tooltip("金粮绊线物体名；上面的 StorySequencePlayer 默认禁用，两只巨兽让路后才启用")]
        [SerializeField] private string m_grainTripwireName = "Beat 4 Grain Tripwire";
        [Tooltip("金粮朝玩家推送的速度（米/秒）")]
        [SerializeField] private float m_grainPushSpeed = 3.5f;
        [Tooltip("金粮停在玩家身前的距离（米）")]
        [SerializeField] private float m_grainStopDistance = 1.4f;
        [Tooltip("玩家拿到金粮后播放的对话图（Beat4DialogueGraph 的「收下金粮」入口）")]
        [SerializeField] private ForestDialogueGraph m_pickupGraph;
        [SerializeField] private ForestDialogueGraph.Entry m_pickupEntry = ForestDialogueGraph.Entry.GrainPickup;

        [Header("果实受撞提示")]
        [Tooltip("树被撞后，果实紧跟着做更明显的摇晃角度（度），暗示可以砍下来")]
        [SerializeField] private float m_nutHintAngle = 9f;
        [Tooltip("果实摇晃时长（秒）")]
        [SerializeField] private float m_nutHintDuration = 0.55f;

        [Header("场景遗留")]
        [Tooltip("Beat_4 是从 Beat2 复制来的，x≈78 处留着一个播蛇台词并锁移动的触发器。" +
                 "打开后在运行时把它停掉，避免卡住谜题；要保留就取消勾选")]
        [SerializeField] private bool m_disableStaleTrigger = true;

        [Header("节拍事件（可在 Inspector 挂声音 / 动画）")]
        [Tooltip("板栗裂成两瓣、原板栗退场后触发")]
        [SerializeField] private UnityEvent m_onNutSplit = new();
        [Tooltip("任意一只巨兽收到食物并开始让路时触发；两只都喂食会各触发一次")]
        [SerializeField] private UnityEvent m_onBeastFed = new();
        [Tooltip("两只巨兽都让路、空气墙关闭且金粮绊线启用后触发")]
        [SerializeField] private UnityEvent m_onSolved = new();
        [Tooltip("金粮推送结束、停在玩家面前后触发")]
        [SerializeField] private UnityEvent m_onGrainDelivered = new();
        [Tooltip("玩家拾取金粮后触发")]
        [SerializeField] private UnityEvent m_onGrainPickedUp = new();

        // 一只巨兽：蓄力冲撞 + 收到食物后带着食物让路
        private sealed class Beast
        {
            public Transform Root;
            public Vector3 Home;
            public Vector3 OutwardDir;   // 树 → 自己的单位向量
            public Vector3 Anchor;       // 撞树锚点（根节点在撞击瞬间的位置）
            public Vector3 BackSpot;     // 后退蓄力位
            public Vector3 SideSpot;     // 让路后的站位
            public bool HasFood;
            public bool IsLeaving;
            public Transform CarriedRoot;   // 被携带的果实
            public Vector3 CarriedOffset;   // 相对巨兽根节点的偏移（保持送达瞬间的位置关系）
            public float NextKnockTime;
            public Beat4TriggerZone Zone;
            public Func<Collider, bool> EnterHandler;
            public StageActor Actor;   // 复用 Beat2 的演员转向（支持多贴图整只翻转）
        }

        // 板栗裂出来的一瓣
        private sealed class Half
        {
            public Transform Root;
            public Beast Target;
            public bool Delivered;
        }

        private readonly List<Beast> m_beasts = new();
        private readonly List<Half> m_halves = new();

        private Transform m_axe;
        private Transform m_nutRoot;
        private Transform m_nutGlyph;
        private Transform m_tree;
        private Transform m_treeVisual;
        private Vector3 m_nutGlyphBaseScale;
        private Quaternion m_nutGlyphBaseRotation;
        private Sprite m_dustSprite;
        private int m_dustOrder;
        private Transform m_boar;
        private DioramaPlayerController m_controller;
        private Beat4TriggerZone m_nutZone;
        private Beat4TriggerZone m_grainZone;

        private bool m_isSplit;
        private bool m_isSolved;

        // 空气墙 / 金粮交付
        private BoxCollider m_airWall;
        private StorySequencePlayer m_grainSequence;
        private GameObject m_grainObject;
        private bool m_grainDelivered;
        private bool m_hasGrain;

        // 冲撞节奏：同一时刻只有一只巨兽真正冲出去
        private enum ChargePhase
        {
            Dash,
            Impact,
            Recover
        }

        private Beast m_chargingBeast;
        private ChargePhase m_chargePhase = ChargePhase.Dash;
        private float m_chargeAge;
        private int m_nextBeastIndex;

        public bool IsSolved => m_isSolved;

        // 场景加载后自动装配（含从别的场景切进 Beat_4 的情况）
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            TryInstall(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryInstall(scene);
        }

        private static void TryInstall(Scene scene)
        {
            if (!scene.name.Contains(k_sceneKeyword)) return;
            if (FindFirstObjectByType<Beat4Puzzle>() != null) return;

            var host = new GameObject("Beat 4 Puzzle");
            host.AddComponent<Beat4Puzzle>();
        }

        private void Start()
        {
            m_axe = FindGlyph(k_axeRootName);
            m_nutRoot = FindTransform(k_nutRootName);
            if (m_nutRoot != null)
            {
                m_nutGlyph = m_nutRoot.Find(k_glyphName);
            }
            m_tree = FindTransform(k_treeName);

            m_boar = FindTransform(k_boarName);
            var bear = FindTransform(k_bearName);

            if (m_axe == null || m_nutGlyph == null || m_boar == null || bear == null)
            {
                Debug.LogWarning("[Beat4Puzzle] 场景里缺少斧头 / 板栗 / 巨兽，谜题未启用。", this);
                enabled = false;
                return;
            }

            CacheNutVisual();
            CacheTreeVisual();

            m_controller = FindFirstObjectByType<DioramaPlayerController>();

            // 野猪在南侧往 -Z 让，老熊在北侧往 +Z 让
            m_beasts.Add(MakeBeast(m_boar, Vector3.back, k_boarAnchorName));
            m_beasts.Add(MakeBeast(bear, Vector3.forward, k_bearAnchorName));

            SetupNutZone();
            CacheAirWall();
            CacheGrain();
            CacheGrainTripwire();

            if (m_disableStaleTrigger)
            {
                DisableStaleTrigger();
            }
        }

        private void OnDestroy()
        {
            // 触发区挂在场景物件上，组件随场景销毁；这里只做对称解绑，避免残留委托
            foreach (var beast in m_beasts)
            {
                if (beast.Zone != null && beast.EnterHandler != null)
                {
                    beast.Zone.Entered -= beast.EnterHandler;
                }
            }
            if (m_nutZone != null)
            {
                m_nutZone.Entered -= HandleNutZoneEntered;
            }
            if (m_grainZone != null)
            {
                m_grainZone.Entered -= HandleGrainEntered;
            }
        }

        private void Update()
        {
            // 撞退优先：玩家进入攻击范围时先处理撞人，再推进冲撞节奏
            ProcessKnockback();
            ProcessChargeCycle();
            ProcessBeastLeaving();
            ProcessCarriedFood();
            CheckSolved();
        }

        // ---------- 斧头劈板栗 ----------

        // 触发球比板栗实体碰撞体大一圈：斧头先进入触发区，再被实体顶住也不会错过
        private void SetupNutZone()
        {
            float nutSolid = MeasureSolidRadius(m_nutGlyph.GetComponent<DioramaObstacle>());
            float axeSolid = MeasureSolidRadius(m_axe.GetComponent<DioramaObstacle>());

            var sphere = m_nutRoot.gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = nutSolid + m_nutTriggerMargin;
            // 根节点在板栗图形上方 1.2 m，碰撞体却默认落在根节点原点。
            // 不把球心挪到图形上，球会悬在半空，斧头在地面水平贴住也碰不到
            sphere.center = m_nutRoot.InverseTransformPoint(m_nutGlyph.position);

            m_nutZone = m_nutRoot.gameObject.AddComponent<Beat4TriggerZone>();
            m_nutZone.Entered += HandleNutZoneEntered;

            Debug.Log($"[Beat4Puzzle] 触发校验：斧头实体半径 {axeSolid:F2} m，板栗实体半径 {nutSolid:F2} m，" +
                      $"板栗触发球半径 {sphere.radius:F2} m（比实体大 {m_nutTriggerMargin:F2} m）；" +
                      $"球心世界坐标 {m_nutRoot.TransformPoint(sphere.center)}；" +
                      $"斧头世界坐标 {m_axe.position}，板栗世界坐标 {m_nutGlyph.position}，" +
                      $"当前平面距离 {FlatDistance(m_axe.position, m_nutGlyph.position):F2} m，" +
                      $"高度差 {Mathf.Abs(m_axe.position.y - m_nutGlyph.position.y):F2} m", this);
        }

        private void CacheNutVisual()
        {
            m_nutGlyphBaseScale = m_nutGlyph.localScale;
            m_nutGlyphBaseRotation = m_nutGlyph.localRotation;

            // 扬尘只是复用果实自己的贴图染色，不引入新美术资源
            var renderer = m_nutGlyph.GetComponentInChildren<SpriteRenderer>();
            if (renderer != null)
            {
                m_dustSprite = renderer.sprite;
                m_dustOrder = renderer.sortingOrder - 1;
            }
        }

        // 树本身不动：只缓存一个带 SpriteRenderer 的视觉子物体用来做受撞抖动
        private void CacheTreeVisual()
        {
            if (m_tree == null) return;

            var renderers = m_tree.GetComponentsInChildren<SpriteRenderer>();
            if (renderers.Length == 0) return;

            m_treeVisual = renderers[0].transform;
        }

        private bool HandleNutZoneEntered(Collider other)
        {
            if (m_isSplit) return false;

            var body = other.attachedRigidbody;
            if (body == null || body.gameObject != m_axe.gameObject) return false;

            PlayNutBreak();
            return true;
        }

        // 掉落 → 斧头弹飞 + 扬尘 → 角色后退 → 膨胀 → 等待 → 裂成两瓣
        private void PlayNutBreak()
        {
            m_isSplit = true;

            Vector3 rest = m_nutGlyph.position;
            StepPlayerBack(rest);

            m_nutGlyph.DOKill();
            m_nutGlyph.localScale = m_nutGlyphBaseScale;
            m_nutGlyph.localRotation = m_nutGlyphBaseRotation;

            // 先被顶起一小段，再落下
            m_nutGlyph.DOMove(rest + Vector3.up * m_nutDropHeight, 0.06f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    if (this == null) return;
                    m_nutGlyph.DOMove(rest, m_nutDropDuration)
                        .SetEase(Ease.InQuad)
                        .OnComplete(() => HandleNutLanded(rest));
                });

            Debug.Log("[Beat4Puzzle] 斧头进入板栗触发区 → 果实开始掉落。", this);
        }

        private void HandleNutLanded(Vector3 rest)
        {
            if (this == null) return;

            // 落地压扁回弹
            m_nutGlyph.DOPunchScale(new Vector3(0.22f, -0.18f, 0.22f), 0.24f, 8, 0.6f);

            KnockAxeAway(rest);
            SpawnDust(rest, 6);

            // 膨胀（短于 0.5 秒）
            m_nutGlyph.DOPunchScale(Vector3.one * m_nutInflateAmount, m_nutInflateDuration, 4, 0.5f);

            DOVirtual.DelayedCall(m_nutInflateDuration + m_nutSplitDelay, () =>
            {
                if (this == null) return;
                SplitNut();
            });
        }

        // 角色被冲击波推后：走撞退通道，连续位移，不是瞬移
        private void StepPlayerBack(Vector3 from)
        {
            if (m_controller == null) return;

            Vector3 away = m_controller.transform.position - from;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
            {
                away = Vector3.right;
            }

            m_controller.ApplyKnockback(away.normalized * m_knockSpeed, m_knockDuration);
        }

        // 斧头被弹飞：直接给刚体初速度，剩下的交给物理与阻尼
        private void KnockAxeAway(Vector3 from)
        {
            var body = m_axe.GetComponentInParent<Rigidbody>();
            if (body == null) return;

            Vector3 away = m_axe.position - from;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
            {
                away = Vector3.right;
            }

            body.isKinematic = false;
            body.linearVelocity = away.normalized * m_axeKnockSpeed;
        }

        private void SpawnDust(Vector3 position, int count)
        {
            if (m_dustSprite == null) return;

            for (int i = 0; i < count; i++)
            {
                var puff = new GameObject("Nut Dust");
                puff.transform.position = position
                    + new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), 0.02f,
                                  UnityEngine.Random.Range(-0.35f, 0.35f));
                puff.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.12f, 0.2f);

                var renderer = puff.AddComponent<SpriteRenderer>();
                renderer.sprite = m_dustSprite;
                renderer.color = new Color(0.72f, 0.68f, 0.6f, 0.8f);
                renderer.sortingOrder = m_dustOrder;

                Vector3 targetScale = puff.transform.localScale * 2.2f;
                puff.transform.DOScale(targetScale, 0.45f).SetEase(Ease.OutQuad);
                renderer.DOFade(0f, 0.45f).SetEase(Ease.InQuad)
                    .OnComplete(() => Destroy(puff));
            }
        }

        private void SplitNut()
        {
            Transform parent = m_nutRoot.parent;

            for (int i = 0; i < 2; i++)
            {
                Vector3 offset = Vector3.forward * (i == 0 ? -m_halfSpacing : m_halfSpacing);
                var clone = Instantiate(m_nutGlyph.gameObject, parent);
                clone.name = $"Nut Half {i + 1}";
                clone.transform.position = m_nutGlyph.position + offset;
                clone.transform.localScale = m_nutGlyphBaseScale * m_halfScale;
                MakePushable(clone);

                // 食物必须能穿过空气墙，否则玩家被墙挡住就送不到巨兽那儿
                IgnoreAirWallFor(clone.GetComponent<Collider>());

                m_halves.Add(new Half
                {
                    // 南那瓣给南边的野猪，北那瓣给北边的熊
                    Target = m_beasts[i],
                    Root = clone.transform,
                });
            }

            // 原板栗（连同它的影子、触发球）退场
            m_nutRoot.gameObject.SetActive(false);
            m_onNutSplit?.Invoke();
            Debug.Log("[Beat4Puzzle] 果实裂成两瓣。", this);
        }

        // ---------- 食物送到巨兽 ----------

        private Beast MakeBeast(Transform root, Vector3 outward, string anchorName)
        {
            Vector3 treePoint = m_tree != null ? m_tree.position : m_nutGlyph.position;
            Vector3 toBeast = root.position - treePoint;
            toBeast.y = 0f;
            Vector3 outwardDir = toBeast.sqrMagnitude > 0.0001f ? toBeast.normalized : outward;

            // 锚点优先用场景里可拖动的空物体；没有就按「离树心 m_contactDistance」兜底
            Transform anchor = FindTransform(anchorName);
            Vector3 anchorPosition = anchor != null
                ? anchor.position
                : treePoint + outwardDir * m_contactDistance;
            anchorPosition.y = root.position.y;

            var beast = new Beast
            {
                Root = root,
                Home = root.position,
                OutwardDir = outwardDir,
                Anchor = anchorPosition,
                BackSpot = root.position + outwardDir * m_backOffDistance,
                SideSpot = root.position + outward * m_beastSideOffset,
            };
            beast.EnterHandler = other => HandleDeliveryEntered(other, beast);

            // 转向复用 Beat2 的 StageActor：它会把整只巨兽的所有贴图一起翻
            beast.Actor = root.GetComponent<StageActor>();

            var obstacle = root.GetComponentInChildren<DioramaObstacle>(true);
            float solid = MeasureSolidRadius(obstacle);
            var sphere = root.gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = solid + m_deliveryTriggerMargin;
            // 和板栗同理：球心要贴在图形上，不能留在高 1.2 m 的根节点原点
            Vector3 anchorPoint = obstacle != null ? obstacle.transform.position : root.position;
            sphere.center = root.InverseTransformPoint(anchorPoint);

            beast.Zone = root.gameObject.AddComponent<Beat4TriggerZone>();
            beast.Zone.Entered += beast.EnterHandler;

            if (anchor == null)
            {
                Debug.LogWarning($"[Beat4Puzzle] 场景里没有撞树锚点「{anchorName}」，暂时用默认距离 " +
                                 $"{m_contactDistance:F2} m。新建同名的空物体即可手动拖动锚点。", root);
            }

            return beast;
        }

        private bool HandleDeliveryEntered(Collider other, Beast beast)
        {
            var body = other.attachedRigidbody;
            if (body == null) return false;

            foreach (var half in m_halves)
            {
                if (half.Delivered || half.Target != beast || half.Root != body.transform) continue;

                Deliver(half);
                return true;
            }
            return false;
        }

        // 不传送：果实留在原地，随后跟着巨兽一起走
        private void Deliver(Half half)
        {
            half.Delivered = true;

            // 食物不再参与物理（关碰撞体，别挡路）
            var body = half.Root.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
            }
            var obstacle = half.Root.GetComponent<DioramaObstacle>();
            if (obstacle != null)
            {
                obstacle.SetEnabled(false);
            }

            Beast beast = half.Target;
            beast.CarriedRoot = half.Root;
            beast.CarriedOffset = half.Root.position - beast.Root.position;
            beast.HasFood = true;
            beast.IsLeaving = true;

            m_onBeastFed?.Invoke();
            Debug.Log($"[Beat4Puzzle] 「{beast.Root.name}」叼走食物，开始让路；此后不再撞玩家。", beast.Root);
        }

        // ---------- 巨兽表现：蓄力冲撞 / 让路 / 携带 ----------

        // 两只巨兽先一起后退；随后依次冲撞板栗树，撞完缓缓退回去
        private void ProcessChargeCycle()
        {
            if (m_isSolved) return;

            float dt = Time.deltaTime;

            // 非当前冲撞者：退回蓄力位（一只撞的时候，另一只后退蓄力）
            foreach (var beast in m_beasts)
            {
                if (beast.HasFood || beast.IsLeaving) continue;
                if (beast == m_chargingBeast) continue;

                MoveBeast(beast, beast.BackSpot, m_backOffSpeed, dt);
            }

            if (m_chargingBeast == null)
            {
                m_chargeAge += dt;
                if (m_chargeAge < m_chargeIdleDuration) return;

                Beast next = PickChargeableBeast();
                if (next == null) return;

                // 玩家正在攻击范围里：先撞人，不冲撞树
                if (IsPlayerInsideThreat(next)) return;

                m_chargingBeast = next;
                m_chargePhase = ChargePhase.Dash;
                return;
            }

            switch (m_chargePhase)
            {
                case ChargePhase.Dash:
                {
                    bool arrived = MoveBeast(m_chargingBeast, m_chargingBeast.Anchor, m_dashSpeed, dt);
                    if (arrived)
                    {
                        // 真正抵达锚点才给树反馈
                        PlayTreeImpact(m_chargingBeast);
                        m_chargePhase = ChargePhase.Impact;
                        m_chargeAge = 0f;
                    }
                    break;
                }
                case ChargePhase.Impact:
                {
                    m_chargeAge += dt;
                    if (m_chargeAge >= m_impactHoldDuration)
                    {
                        m_chargePhase = ChargePhase.Recover;
                    }
                    break;
                }
                case ChargePhase.Recover:
                {
                    bool arrived = MoveBeast(m_chargingBeast, m_chargingBeast.BackSpot,
                                             m_recoverSpeed, dt);
                    if (arrived)
                    {
                        m_chargingBeast = null;
                        m_chargeAge = 0f;
                        m_chargePhase = ChargePhase.Dash;
                    }
                    break;
                }
            }
        }

        private Beast PickChargeableBeast()
        {
            for (int i = 0; i < m_beasts.Count; i++)
            {
                int index = (m_nextBeastIndex + i) % m_beasts.Count;
                Beast beast = m_beasts[index];
                if (beast.HasFood || beast.IsLeaving) continue;

                m_nextBeastIndex = (index + 1) % m_beasts.Count;
                return beast;
            }
            return null;
        }

        // 树受撞反馈：只抖动视觉子物体的位置，树根、碰撞体、原有逻辑都不动
        private void PlayTreeImpact(Beast beast)
        {
            if (m_treeVisual != null)
            {
                m_treeVisual.DOKill();
                var punch = new Vector3(m_treePunch, 0f, 0f);
                m_treeVisual.DOPunchPosition(punch, m_treePunchDuration, 10, 0.6f);
            }

            SpawnDust(beast.Root.position, 3);

            // 紧跟着树的抖动，未落地的果实再做一次更明显的摇晃，暗示可以砍下来
            if (!m_isSplit && m_nutGlyph != null)
            {
                DOVirtual.DelayedCall(m_treePunchDuration, () =>
                {
                    if (this == null || m_isSplit || m_nutGlyph == null) return;

                    m_nutGlyph.DOKill();
                    m_nutGlyph.DOPunchRotation(new Vector3(0f, 0f, m_nutHintAngle),
                                              m_nutHintDuration, 12, 0.5f);
                });
            }
        }

        // 拿到食物：走到自己的站位，站着不动（就在附近，不走远）
        private void ProcessBeastLeaving()
        {
            foreach (var beast in m_beasts)
            {
                if (!beast.IsLeaving) continue;

                bool arrived = MoveBeast(beast, beast.SideSpot, m_beastMoveSpeed, Time.deltaTime);
                if (arrived)
                {
                    beast.IsLeaving = false;
                }
            }
        }

        // 食物跟着巨兽走：保持送达瞬间的相对偏移
        private void ProcessCarriedFood()
        {
            foreach (var beast in m_beasts)
            {
                if (beast.CarriedRoot == null) continue;

                beast.CarriedRoot.position = beast.Root.position + beast.CarriedOffset;
            }
        }

        // 让巨兽朝移动方向转身：交给 StageActor，整只巨兽一起翻
        private void FaceBeast(Beast beast, Vector3 moveDelta)
        {
            if (beast.Actor == null || Mathf.Abs(moveDelta.x) < 0.01f) return;

            beast.Actor.Face(moveDelta.x);
        }

        // 统一位移入口：走一步、按位移转向，返回是否已抵达
        private bool MoveBeast(Beast beast, Vector3 target, float speed, float dt)
        {
            Vector3 before = beast.Root.position;
            Vector3 next = Vector3.MoveTowards(before, target, speed * dt);
            beast.Root.position = next;

            Vector3 delta = next - before;
            if (delta.sqrMagnitude > 0.0000001f)
            {
                FaceBeast(beast, delta);
            }

            return (next - target).sqrMagnitude < 0.0004f;
        }

        // ---------- 空气墙 ----------

        // 不可见的实体墙：玩家从两只巨兽中间穿不过去，食物与斧头可以穿过去。
        // 墙是场景里的实物（BoxCollider 已挂好），这里只取引用；解谜后关掉它
        private void CacheAirWall()
        {
            var host = FindTransform(m_airWallName);
            m_airWall = host != null ? host.GetComponent<BoxCollider>() : null;

            if (m_airWall == null)
            {
                Debug.LogWarning($"[Beat4Puzzle] 空气墙「{m_airWallName}」上没有 BoxCollider，" +
                                 "玩家可能从巨兽之间穿过去。", this);
            }
        }

        // 食物要能穿墙到巨兽那边，否则玩家被墙挡住就再也送不到
        private void IgnoreAirWallFor(Collider target)
        {
            if (m_airWall == null || target == null) return;

            Physics.IgnoreCollision(m_airWall, target, true);
        }

        // ---------- 金粮交付 ----------

        // 金粮是场景里预先摆好的实物（默认未激活）。触线由场景里的
        // StorySequencePlayer 负责，它播完对话后在 Event 步调用 DeliverGrain
        private void CacheGrain()
        {
            var grain = FindTransformIncludingInactive(m_grainName);
            if (grain == null)
            {
                Debug.LogWarning($"[Beat4Puzzle] 场景里没有金粮「{m_grainName}」，交付动作不会发生。", this);
                return;
            }

            m_grainObject = grain.gameObject;
            m_grainObject.SetActive(false);

            // 金粮的外形复用板栗贴图染成金色；美术要换成专属贴图时直接改场景里的 SpriteRenderer
            var grainRenderer = m_grainObject.GetComponent<SpriteRenderer>();
            if (grainRenderer != null && m_dustSprite != null)
            {
                grainRenderer.sprite = m_dustSprite;
                grainRenderer.color = new Color(1f, 0.86f, 0.32f, 1f);
                grainRenderer.sortingOrder = m_dustOrder + 2;
            }

            m_grainZone = m_grainObject.GetComponent<Beat4TriggerZone>();
            if (m_grainZone != null)
            {
                m_grainZone.Entered += HandleGrainEntered;
            }
        }

        // 绊线默认关掉：两只巨兽都让路之前，玩家不该走到那里，也不该提前播交付
        private void CacheGrainTripwire()
        {
            var tripwire = FindTransform(m_grainTripwireName);
            m_grainSequence = tripwire != null ? tripwire.GetComponent<StorySequencePlayer>() : null;

            if (m_grainSequence == null)
            {
                Debug.LogWarning($"[Beat4Puzzle] 绊线「{m_grainTripwireName}」上没有 StorySequencePlayer，" +
                                 "金粮交付不会触发。", this);
                return;
            }

            m_grainSequence.enabled = false;
        }

        // 供 StorySequencePlayer 的 Event 步绑定：巨兽把金粮推到玩家面前
        public void DeliverGrain()
        {
            if (m_grainDelivered || m_grainObject == null || m_controller == null) return;

            m_grainDelivered = true;
            _ = DeliverGrainAsync();
        }

        private async Awaitable DeliverGrainAsync()
        {
            Vector3 playerPos = m_controller.transform.position;
            Beast giver = PickGrainGiver(playerPos);
            Vector3 start = giver != null
                ? giver.Root.position
                : playerPos - Vector3.right * 2f;
            start.y = playerPos.y + 0.1f;

            Vector3 toPlayer = playerPos - start;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.0001f)
            {
                toPlayer = Vector3.back;
            }
            Vector3 end = playerPos - toPlayer.normalized * m_grainStopDistance;
            end.y = start.y;

            m_grainObject.transform.position = start;
            m_grainObject.transform.localScale = Vector3.one * 0.6f;
            m_grainObject.SetActive(true);

            await PushGrainAsync(end);

            if (this == null) return;
            m_onGrainDelivered?.Invoke();
            Debug.Log("[Beat4Puzzle] 金粮已推到玩家面前，等玩家上前拾取。", this);
        }

        // 由离玩家最近的那只巨兽推粮
        private Beast PickGrainGiver(Vector3 playerPos)
        {
            Beast nearest = null;
            float best = float.MaxValue;

            foreach (var beast in m_beasts)
            {
                if (!beast.HasFood) continue;

                float distance = FlatDistance(beast.Root.position, playerPos);
                if (distance >= best) continue;

                best = distance;
                nearest = beast;
            }
            return nearest;
        }

        private bool HandleGrainEntered(Collider other)
        {
            if (m_hasGrain || m_grainObject == null) return false;

            var body = other.attachedRigidbody;
            if (body == null || m_controller == null) return false;
            if (body.transform != m_controller.transform) return false;

            CollectGrain();
            return true;
        }

        // 拾取：预置的金粮缩小退回（不销毁场景实物），再播「收下金粮」那段对话
        private void CollectGrain()
        {
            m_hasGrain = true;

            if (m_grainObject != null)
            {
                m_grainObject.transform.DOScale(Vector3.zero, 0.25f)
                    .OnComplete(() =>
                    {
                        if (m_grainObject != null) m_grainObject.SetActive(false);
                    });
            }

            // 收下金粮的旁白走对话图（节点编辑器维护），不再是 CSV 行号
            var start = m_pickupGraph != null ? m_pickupGraph.StartFor(m_pickupEntry) : null;
            if (start != null)
            {
                _ = DialogueGraphPlayer.PlayAsync(m_pickupGraph, start.id);
            }
            else
            {
                Debug.LogWarning("[Beat4Puzzle] 未指定收下金粮的对话图（m_pickupGraph），旁白不会播放。", this);
            }

            m_onGrainPickedUp?.Invoke();
            Debug.Log("[Beat4Puzzle] 玩家取得金粮（剧情道具，不进背包）。", this);
        }

        // 金粮沿直线滑向玩家，带一点起伏，读起来像被"推"过去
        private async Awaitable PushGrainAsync(Vector3 end)
        {
            if (m_grainObject == null) return;

            Transform grain = m_grainObject.transform;
            Vector3 start = grain.position;
            float distance = FlatDistance(start, end);
            float duration = Mathf.Max(0.35f, distance / Mathf.Max(0.5f, m_grainPushSpeed));
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (this == null || grain == null) return;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
                position.y = start.y + Mathf.Sin(t * Mathf.PI) * 0.35f;
                grain.position = position;
                grain.Rotate(Vector3.up, 360f * Time.deltaTime, Space.World);

                await Awaitable.NextFrameAsync();
            }

            if (grain == null) return;
            grain.position = end;
        }

        // ---------- 撞退（防跳关） ----------

        private void ProcessKnockback()
        {
            if (m_controller == null) return;

            foreach (var beast in m_beasts)
            {
                // 拿到食物的巨兽不再撞人
                if (beast.HasFood) continue;
                if (Time.time < beast.NextKnockTime) continue;
                if (!IsPlayerInsideThreat(beast)) continue;

                beast.NextKnockTime = Time.time + m_knockCooldown;

                Vector3 away = m_controller.transform.position - beast.Root.position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.0001f)
                {
                    away = -beast.OutwardDir;
                }

                m_controller.ApplyKnockback(away.normalized * m_knockSpeed, m_knockDuration);
                Debug.Log($"[Beat4Puzzle] 玩家进入「{beast.Root.name}」的攻击范围，被撞退。", beast.Root);
            }
        }

        private bool IsPlayerInsideThreat(Beast beast)
        {
            if (m_controller == null) return false;

            Vector3 player = m_controller.transform.position;
            player.y = 0f;
            Vector3 center = beast.Root.position;
            center.y = 0f;

            return Vector3.Distance(player, center) <= m_threatRadius;
        }

        // ---------- 谜题完成 ----------

        private void CheckSolved()
        {
            if (m_isSolved) return;

            foreach (var beast in m_beasts)
            {
                if (!beast.HasFood || beast.IsLeaving) return;
            }

            m_isSolved = true;

            // 两只巨兽都让路后，空气墙撤掉，玩家可以继续往前走
            if (m_airWall != null)
            {
                m_airWall.enabled = false;
            }

            // 通路打开后，金粮绊线才生效：玩家越过它就会触发交付演出
            if (m_grainSequence != null)
            {
                m_grainSequence.enabled = true;
            }

            m_onSolved?.Invoke();
            Debug.Log("[Beat4Puzzle] 两只巨兽都已让路，中间通路打开。", this);
        }

        // ---------- 工具 ----------

        // 占位半径取实体碰撞体在世界 XZ 上的半宽（Capsule 的 x/z 半径相同）
        private static float MeasureSolidRadius(DioramaObstacle obstacle)
        {
            if (obstacle == null || obstacle.Collider == null) return 0f;

            Bounds bounds = obstacle.Collider.bounds;
            return Mathf.Max(bounds.extents.x, bounds.extents.z);
        }

        // 让被劈出来的一瓣变成"推得动、挡得住"的小块
        private static void MakePushable(GameObject target)
        {
            var body = target.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = target.AddComponent<Rigidbody>();
            }
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            body.mass = 1f;
            body.linearDamping = 6f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var obstacle = target.GetComponent<DioramaObstacle>();
            if (obstacle == null)
            {
                obstacle = target.AddComponent<DioramaObstacle>();
            }
            obstacle.EnsureCollider();
        }

        // Beat_4 是从 Beat2 复制的：这个触发器播的是"蛇"的台词并会锁住玩家移动，
        // 正好压在谜题区（x≈78），不关掉会卡住整个流程
        private void DisableStaleTrigger()
        {
            var stale = GameObject.Find(k_staleTriggerName);
            if (stale == null) return;

            stale.SetActive(false);
            Debug.LogWarning($"[Beat4Puzzle] 已停用场景里的「{k_staleTriggerName}」：" +
                             "它播放的是 Beat2 的蛇台词并会锁定玩家移动，会挡住谜题。", stale);
        }

        private static Transform FindTransform(string name)
        {
            var go = GameObject.Find(name);
            return go != null ? go.transform : null;
        }

        // GameObject.Find 只能找到激活物体；预置的金粮默认是关闭的，这里连未激活的一起找
        private static Transform FindTransformIncludingInactive(string name)
        {
            Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform t in all)
            {
                if (t.name == name) return t;
            }
            return null;
        }

        private static Transform FindGlyph(string rootName)
        {
            var root = FindTransform(rootName);
            return root != null ? root.Find(k_glyphName) : null;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
