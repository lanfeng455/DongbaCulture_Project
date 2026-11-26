using System;
using UnityEngine;
using Ivan.Diorama;

namespace Ivan.Puzzle
{
    // 可推动的石头 / 字牌：玩家用身体顶它，它会挡住视线。
    // 刚体配置与玩家一致——锁 Y 与旋转、无重力，只做平面位移。
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Puzzle/Pushable Rock")]
    public class PushableRock : MonoBehaviour
    {
        [Tooltip("质量，越大越推不动（玩家质量默认 1）")]
        [SerializeField] private float m_mass = 3f;

        [Tooltip("线性阻力，越大滑行距离越短")]
        [SerializeField] private float m_damping = 6f;

        [Tooltip("是否在被推动时变色提示。解谜字牌等不需要反馈的可以关掉")]
        [SerializeField] private bool m_enablePushHighlight = true;

        [Tooltip("是否在被推动时播放音效")]
        [SerializeField] private bool m_enablePushSound = true;

        private SpriteRenderer m_spriteRenderer;
        private Color m_oriColor;

        [SerializeField] private Color m_pushColor;

        // 挂组件的瞬间（编辑器）就把刚体配好：关重力、锁 Y 与旋转。
        // 否则默认重力开着，一进 Play 就掉进虚空
        private void Reset()
        {
            PrepareBody();
        }

        private void Awake()
        {
            PrepareBody();

            m_spriteRenderer = GetComponent<SpriteRenderer>();
            if (m_spriteRenderer != null)
            {
                m_oriColor = m_spriteRenderer.color;
            }

            // 挡住玩家 + 挡住视线，两者用同一个占位尺寸
            var obstacle = GetComponent<DioramaObstacle>();
            if (obstacle == null)
            {
                obstacle = gameObject.AddComponent<DioramaObstacle>();
            }
            obstacle.EnsureCollider();

            if (m_enablePushSound)
            {
                obstacle.OnBeingPushed += PlaySound;
            }
            if (m_enablePushHighlight && m_spriteRenderer != null)
            {
                obstacle.OnBeingPushed += ChangeColorToPush;
                obstacle.OnFinishPushed += ChangeColorToOri;
            }

            if (GetComponent<SightBlocker>() == null)
            {
                gameObject.AddComponent<SightBlocker>();
            }

            // 呼吸动效提示"这块可以推"；已有 Breathing 组件则跳过
            if (GetComponent<Breathing>() == null)
            {
                gameObject.AddComponent<Breathing>();
            }
        }

        private void OnDestroy()
        {
            var obstacle = GetComponent<DioramaObstacle>();
            if (obstacle != null)
            {
                obstacle.OnBeingPushed -= PlaySound;
                obstacle.OnBeingPushed -= ChangeColorToPush;
                obstacle.OnFinishPushed -= ChangeColorToOri;
            }
        }

        // 外部开关：解谜字牌不需要高亮 / 音效，让 Beat3Puzzle 初始化时关掉
        public void SetPushFeedback(bool highlight, bool sound)
        {
            m_enablePushHighlight = highlight;
            m_enablePushSound = sound;
        }

        private void PlaySound()
        {
            AudioManager.Instance.PlaySe("Assets/Ivan/Audio/SE/MoveStone");
        }

        private void ChangeColorToPush()
        {
            if (m_spriteRenderer != null)
            {
                m_spriteRenderer.color = m_pushColor;
            }
        }

        private void ChangeColorToOri()
        {
            if (m_spriteRenderer != null)
            {
                m_spriteRenderer.color = m_oriColor;
            }
        }

        // 本场景移动是纯平面（XZ）的：无重力、锁竖直位移与旋转，其余交给物理
        private void PrepareBody()
        {
            var body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation |
                               RigidbodyConstraints.FreezePositionY;
            body.mass = m_mass;
            body.linearDamping = m_damping;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }
    }
}
