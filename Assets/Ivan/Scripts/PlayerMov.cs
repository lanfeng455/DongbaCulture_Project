using UnityEngine;
using UnityEngine.InputSystem;

// Moves the player on the XZ ground plane with 8-way, camera-relative input.
// Position is clamped to a square bound so the player can never leave the play area.
[RequireComponent(typeof(CharacterController))]
public class PlayerMov : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Ground movement speed in meters per second")]
    [SerializeField] private float m_moveSpeed = 5f;

    [Header("Bounds (world XZ)")]
    [Tooltip("Minimum corner of the playable area (x = world X, y = world Z)")]
    [SerializeField] private Vector2 m_boundsMin = new Vector2(-8f, -8f);

    [Tooltip("Maximum corner of the playable area (x = world X, y = world Z)")]
    [SerializeField] private Vector2 m_boundsMax = new Vector2(8f, 8f);

    [Header("Visual")]
    [Tooltip("Uncheck this if the source glyph walks left instead of right")]
    [SerializeField] private bool m_spriteFacesRight = true;

    private CharacterController m_characterController;
    private Transform m_cameraTransform;
    private InputAction m_moveAction;
    private SpriteRenderer m_visualRenderer;
    private int m_facingSign = 1;

    // Screen-space facing: +1 = right, -1 = left. Equipped tools mirror themselves off this.
    public int FacingSign => m_facingSign;

    private void Awake()
    {
        // Cache component references here
        m_characterController = GetComponent<CharacterController>();
        m_moveAction = InputSystem.actions?.FindAction("Player/Move");

        // The asset builder guarantees a child named "Visual" that carries the sprite
        Transform visual = transform.Find("Visual");
        if (visual != null)
        {
            m_visualRenderer = visual.GetComponent<SpriteRenderer>();
        }

        if (m_moveAction == null)
        {
            Debug.LogError($"[{nameof(PlayerMov)}] Input action 'Player/Move' not found in the project-wide actions asset.", this);
        }
    }

    private void OnEnable()
    {
        m_moveAction?.Enable();
    }

    private void Start()
    {
        // Cache in Start so the main camera is guaranteed to exist in the loaded scene
        if (Camera.main != null)
        {
            m_cameraTransform = Camera.main.transform;
        }
        else
        {
            Debug.LogWarning($"[{nameof(PlayerMov)}] No main camera found; movement stays world-aligned.", this);
        }
    }

    private void OnDisable()
    {
        m_moveAction?.Disable();
    }

    private void Update()
    {
        HandleMovement();
    }

    private void HandleMovement()
    {
        if (m_moveAction == null)
        {
            return;
        }

        Vector2 input = m_moveAction.ReadValue<Vector2>();
        if (input.sqrMagnitude < 0.001f)
        {
            return;
        }

        Vector3 direction;

        if (m_cameraTransform != null)
        {
            // Project the camera axes onto the ground plane so "up" input means screen-up
            Vector3 forward = m_cameraTransform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 right = m_cameraTransform.right;
            right.y = 0f;
            right.Normalize();

            direction = forward * input.y + right * input.x;
        }
        else
        {
            direction = new Vector3(input.x, 0f, input.y);
        }

        // Clamp magnitude so diagonal (8-way) movement is not faster than cardinal movement
        direction = Vector3.ClampMagnitude(direction, 1f);

        UpdateFacing(direction);

        // SimpleMove applies gravity itself and keeps the controller grounded
        m_characterController.SimpleMove(direction * m_moveSpeed);
        ClampToBounds();
    }

    private void UpdateFacing(Vector3 direction)
    {
        if (m_cameraTransform == null)
        {
            return;
        }

        // Horizontal movement in screen space decides the facing; vertical-only input keeps it
        float screenX = Vector3.Dot(direction, m_cameraTransform.right);
        if (screenX > 0.01f)
        {
            SetFacing(1);
        }
        else if (screenX < -0.01f)
        {
            SetFacing(-1);
        }
    }

    private void SetFacing(int sign)
    {
        m_facingSign = sign;
        if (m_visualRenderer != null)
        {
            m_visualRenderer.flipX = m_spriteFacesRight ? sign < 0 : sign > 0;
        }
    }

    private void ClampToBounds()
    {
        Vector3 position = transform.position;
        position.x = Mathf.Clamp(position.x, m_boundsMin.x, m_boundsMax.x);
        position.z = Mathf.Clamp(position.z, m_boundsMin.y, m_boundsMax.y);
        transform.position = position;
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize the movement clamp area in the Scene view
        Vector3 center = new Vector3((m_boundsMin.x + m_boundsMax.x) * 0.5f, 0f, (m_boundsMin.y + m_boundsMax.y) * 0.5f);
        Vector3 size = new Vector3(m_boundsMax.x - m_boundsMin.x, 0.1f, m_boundsMax.y - m_boundsMin.y);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(center, size);
    }
}
