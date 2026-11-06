using UnityEngine;
using Ivan.Diorama;

namespace Ivan.Puzzle
{
    // 可推动的石头：玩家用身体顶它，它会挡住两条蛇的视线。
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

        // 挂组件的瞬间（编辑器）就把刚体配好：关重力、锁 Y 与旋转。
        // 否则默认重力开着，一进 Play 石头就掉进虚空
        private void Reset()
        {
            PrepareBody();
        }

        private void Awake()
        {
            PrepareBody();

            // 挡住玩家 + 挡住视线，两者用同一个占位尺寸
            var obstacle = GetComponent<DioramaObstacle>();
            if (obstacle == null)
            {
                obstacle = gameObject.AddComponent<DioramaObstacle>();
            }
            obstacle.EnsureCollider();

            if (GetComponent<SightBlocker>() == null)
            {
                gameObject.AddComponent<SightBlocker>();
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
