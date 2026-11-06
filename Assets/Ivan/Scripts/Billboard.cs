using UnityEngine;

// Rotates the sprite so it always fully faces the main camera.
// Full camera-facing (not yaw-only) keeps sprites from looking squashed under the tilted camera pitch.
public class Billboard : MonoBehaviour
{
    private Transform m_cameraTransform;

    private void Start()
    {
        if (Camera.main != null)
        {
            m_cameraTransform = Camera.main.transform;
        }
        else
        {
            Debug.LogWarning($"[{nameof(Billboard)}] No main camera found; billboard disabled.", this);
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        // Run after all movement so the sprite matches the final camera pose of the frame
        transform.rotation = m_cameraTransform.rotation;
    }
}
