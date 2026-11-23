using UnityEngine;

using Unity.Cinemachine;

namespace Ivan.Diorama
{
    // 相机取景边界钳制（Cinemachine 3 扩展，挂在 CinemachineCamera 上）。
    // 场景相机是正交俯视（俯角约 39°），画面落到地面上是一个长方形
    // （16:9 下约 30.2 m 宽 × 27 m 深），而纸面地面只有 111.5 m × 42 m。
    // 相机按跟随偏移跟着玩家走，走到行程两端时这个长方形就会探出纸面，露出背景色。
    // 本扩展在管线最后一站把相机的 X / Z 夹回「画面仍完整落在地面内」的区间。
    // 边界从地面渲染器的包围盒实时读取，改地面尺寸后无需手填数值。
    [DisallowMultipleComponent]
    public class DioramaCameraBoundsClamp : CinemachineExtension
    {
        public const string k_groundObjectName = "Stationary paper ground";

        private const float k_minForwardY = 0.01f;   // 视线接近水平时画面角点投不到地面，直接放行
        private const float k_minAspect = 0.01f;
        private const float k_defaultAspect = 16f / 9f;

        [Tooltip("地面渲染器。留空时自动查找场景里名为 Stationary paper ground 的物体")]
        [SerializeField] private Renderer m_ground;

        [Tooltip("画面边缘与纸面边缘之间额外保留的余量（米）")]
        [SerializeField] private float m_margin;

        [Tooltip("用于读取画面宽高比的相机。留空用 Camera.main")]
        [SerializeField] private Camera m_camera;

        private bool m_isGroundSearched;

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage,
            ref CameraState state,
            float deltaTime)
        {
            // 管线最后一站的位置已经包含跟随、阻尼与噪声，在这里夹才不会和 Cinemachine 抢位置
            if (stage != CinemachineCore.Stage.Finalize) return;
            if (!state.Lens.Orthographic) return;

            EnsureGround();
            if (m_ground == null) return;

            Vector3 forward = state.RawOrientation * Vector3.forward;
            if (Mathf.Abs(forward.y) < k_minForwardY) return;

            Vector3 up = state.RawOrientation * Vector3.up;
            Vector3 right = state.RawOrientation * Vector3.right;
            float halfHeight = state.Lens.OrthographicSize;
            float halfWidth = halfHeight * ResolveAspect();
            Bounds ground = m_ground.bounds;
            float planeY = ground.max.y;

            Vector3 position = state.RawPosition;
            float minHitX = float.PositiveInfinity;
            float maxHitX = float.NegativeInfinity;
            float minHitZ = float.PositiveInfinity;
            float maxHitZ = float.NegativeInfinity;

            // 画面四角沿视线落到地面平面：正交投影下是一个平行四边形，取它的轴对齐包围即可
            for (int i = 0; i < 4; i++)
            {
                float signX = (i & 1) == 0 ? -1f : 1f;
                float signY = (i & 2) == 0 ? -1f : 1f;
                Vector3 corner = position + right * (signX * halfWidth) + up * (signY * halfHeight);
                float distance = (planeY - corner.y) / forward.y;
                Vector3 hit = corner + forward * distance;

                minHitX = Mathf.Min(minHitX, hit.x);
                maxHitX = Mathf.Max(maxHitX, hit.x);
                minHitZ = Mathf.Min(minHitZ, hit.z);
                maxHitZ = Mathf.Max(maxHitZ, hit.z);
            }

            position.x = ClampAxis(position.x, minHitX - position.x, maxHitX - position.x,
                                   ground.min.x, ground.max.x);
            position.z = ClampAxis(position.z, minHitZ - position.z, maxHitZ - position.z,
                                   ground.min.z, ground.max.z);
            state.RawPosition = position;
        }

        // 把相机在某一轴上的位置夹到「画面落点仍在 [groundMin, groundMax] 内」的区间。
        // 画面比地面还长时（正常不会发生）无解，改为让画面在地面上居中
        private float ClampAxis(float cameraValue, float footprintMin, float footprintMax,
                                float groundMin, float groundMax)
        {
            float low = groundMin + m_margin - footprintMin;
            float high = groundMax - m_margin - footprintMax;
            if (low > high)
            {
                return (groundMin + groundMax) * 0.5f - (footprintMin + footprintMax) * 0.5f;
            }
            return Mathf.Clamp(cameraValue, low, high);
        }

        // 只查一次，避免每帧 Find
        private void EnsureGround()
        {
            if (m_ground != null || m_isGroundSearched) return;

            m_isGroundSearched = true;
            var groundObject = GameObject.Find(k_groundObjectName);
            if (groundObject != null)
            {
                m_ground = groundObject.GetComponent<Renderer>();
            }
            if (m_ground == null)
            {
                Debug.LogWarning($"[{GetType().Name}] 场景里找不到「{k_groundObjectName}」，" +
                                 "相机不做取景钳制。", this);
            }
        }

        // 宽高比会随窗口 / 分辨率变化，取运行中相机的实际值
        private float ResolveAspect()
        {
            if (m_camera == null)
            {
                m_camera = Camera.main;
            }
            if (m_camera != null && m_camera.aspect > k_minAspect)
            {
                return m_camera.aspect;
            }
            if (Screen.height > 0)
            {
                return (float)Screen.width / Screen.height;
            }
            return k_defaultAspect;
        }
    }
}
