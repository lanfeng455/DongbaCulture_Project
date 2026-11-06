using UnityEngine;

// 掉落物：一个 SpriteRenderer + 一个 Collider（好让玩家的拾取范围检测到）就够，
// 不需要实体那套描边/状态机结构。
//
// 在地上自转直到被捡起；被捡起后挂到玩家的持有物槽位（背包做出来之前，手上已满则直接销毁）。
public class DroppedItem : MonoBehaviour
{
    [Header("地上表现")]
    [Tooltip("自转速度（度/秒），绕自身 Y 轴旋转")]
    [SerializeField] private float m_spinDegreesPerSecond = 120f;

    [Tooltip("SpriteRenderer 的 Order in Layer；设得比 3D 物体高、比瞄准圆环(500)低即可")]
    [SerializeField] private int m_sortingOrder = 100;

    [Tooltip("FaceCamera 时，沿相机方向额外前推多少米；跨过薄门/薄架子但不影响厚墙遮挡")]
    [SerializeField] private float m_depthPushInMeters = 0.05f;

    [Header("文字身份")]
    [Tooltip("这个掉落物代表的文字（如 石、白）；留空表示它只是普通道具，不能参与瞄准融合")]
    [SerializeField] private string m_glyphWord;

    private Collider m_collider;
    private Rigidbody m_rigidbody;
    private SpriteRenderer m_spriteRenderer;
    private bool m_isCollected;

    public bool IsCollected => m_isCollected;

    // 供瞄准/融合系统读取：手里拿的到底是哪个字
    public string GlyphWord => m_glyphWord;
    public SpriteRenderer GlyphRenderer => m_spriteRenderer;

    private void Awake()
    {
        m_collider = GetComponent<Collider>();
        if (m_collider == null)
        {
            Debug.LogWarning($"[{GetType().Name}] 缺少 Collider；玩家的拾取范围检测不到它。", this);
        }

        // 掉落物可能带刚体（从桌上掉下来那种），捡起时要处理，避免拿着还受重力
        m_rigidbody = GetComponent<Rigidbody>();
        m_spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // 给 SpriteRenderer 设 sortingOrder：PaperSprite.shader 已用 ZTest Always，
        // 不会被 3D 物体挡住，但 Transparent 队列内多图层叠仍需 sortingOrder 排序
        if (m_spriteRenderer != null)
        {
            m_spriteRenderer.sortingOrder = m_sortingOrder;
        }
    }

    private void Update()
    {
        // 本地空间自转：躺平还是立起由预制体的初始朝向决定，代码不干预
        transform.Rotate(0f, m_spinDegreesPerSecond * Time.deltaTime, 0f, Space.Self);
    }

    // 收进持有物槽位：先禁用碰撞体并停用自转，再挂接
    // （挂到手里后会和拾取范围重叠，不先失效会反复触发拾取）
    public void Collect(Transform heldSlot)
    {
        if (m_isCollected) return;
        m_isCollected = true;
        DisableForHolding();
        if (heldSlot != null)
        {
            AttachToSlot(heldSlot);
        }
    }

    // 只做收集状态 + 禁用物理/自转，不挂到任何槽位（进 Hotbar 但还没选中）
    public void CollectSilently()
    {
        if (m_isCollected) return;
        m_isCollected = true;
        DisableForHolding();
    }

    // 改写这个掉落物代表的文字（融合结果）。sprite 为空时只改语义、不改贴图。
    public void SetGlyphWord(string word, Sprite sprite)
    {
        m_glyphWord = word;

        if (sprite != null && m_spriteRenderer != null)
        {
            m_spriteRenderer.sprite = sprite;
        }
    }

    // 发射时脱手：只解除父子关系，保留已收集状态（禁用碰撞、停用自转），
    // 这样飞行期间不会重新被拾取范围检测到
    public void DetachFromSlot()
    {
        transform.SetParent(null, true);
    }

    // 飞回手中：重新挂到槽位。这里刻意不看 m_isCollected——
    // 物品早已被收集，只是短暂离手飞行
    public void AttachToSlot(Transform heldSlot)
    {
        if (heldSlot == null)
        {
            return;
        }

        transform.SetParent(heldSlot, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    // 正对镜头 + 沿相机方向前推一点点（m_depthPushInMeters）。
    // 前推让 sprite 跨过薄门/薄架子，深度拉平 shader 负责消除顶点深度交错；
    // 两者配合让道具看起来自然贴在台面之上，又不被厚墙穿帮。
    public void FaceCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        transform.rotation = camera.transform.rotation;

        if (m_depthPushInMeters > 0f)
        {
            // 沿相机方向推几厘米：配合 PaperSprite shader 的深度拉平，
            // 让飞行中的道具跨过薄门/薄架子又不被厚墙穿帮
            transform.position += camera.transform.forward * m_depthPushInMeters;
        }
    }

    // 手上已有持有物、背包又还没做：直接销毁掉落物
    public void Consume()
    {
        if (m_isCollected)
        {
            return;
        }

        m_isCollected = true;
        Destroy(gameObject);
    }

    // 被收走/拿在手里时必须彻底停止碰撞与自转，否则会反复触发拾取
    private void DisableForHolding()
    {
        if (m_collider != null)
        {
            m_collider.enabled = false;
        }

        if (m_rigidbody != null)
        {
            // 改成运动学：拿在手里跟着槽位走，不再受重力也不会撞场景
            m_rigidbody.isKinematic = true;

            // 去掉 X/Z 的位移冻结。掉落物落地时通常冻结了 X/Z（只留 Y 自由下落），
            // 捡起来后要跟着玩家移动，这两个轴的冻结必须解除
            RigidbodyConstraints constraints = m_rigidbody.constraints;
            constraints &= ~RigidbodyConstraints.FreezePositionX;
            constraints &= ~RigidbodyConstraints.FreezePositionZ;
            m_rigidbody.constraints = constraints;
        }

        enabled = false;
    }
}
