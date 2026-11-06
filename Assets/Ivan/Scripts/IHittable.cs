using UnityEngine;

// Anything a tool can hit. Implementers own their HP, hit feedback, and death,
// so tools never need to know what they hit and new targets need no tool changes.
public interface IHittable
{
    bool IsAlive { get; }

    void ApplyHit(int damage, Vector3 hitPoint);
}
