using UnityEngine;

// Moves the entity at a constant velocity; the simplest IMovable implementation.
// Combine with any entity type (the cloud pairs it with SceneEntity).
[RequireComponent(typeof(SceneEntity))]
public class DriftMover : MonoBehaviour, IMovable
{
    [Tooltip("World-space drift speed in meters per second")]
    [SerializeField] private Vector3 m_velocity = new Vector3(0.3f, 0f, 0f);

    public Vector3 Velocity
    {
        get => m_velocity;
        set => m_velocity = value;
    }

    private void Update()
    {
        transform.position += m_velocity * Time.deltaTime;
    }
}
