using UnityEngine;

// Attached by the VegetationSpawner to every plant that counts toward the
// fertility field. When the plant is destroyed (chopped, room cleanup...) the
// registration unwinds automatically — no polling, no changes to the plant's
// own scripts or prefabs.
public class FertilityRegistration : MonoBehaviour
{
    private FertilityField m_field;
    private Vector3 m_registeredPosition;

    public static FertilityRegistration Attach(GameObject plant, FertilityField field)
    {
        FertilityRegistration registration = plant.AddComponent<FertilityRegistration>();
        registration.m_field = field;
        registration.m_registeredPosition = plant.transform.position;
        return registration;
    }

    private void OnDestroy()
    {
        if (m_field != null)
        {
            // Unwind at the position captured at attach time: the plant may
            // have been moved (or scaled to zero) before destruction
            m_field.Unregister(m_registeredPosition);
        }
    }
}
