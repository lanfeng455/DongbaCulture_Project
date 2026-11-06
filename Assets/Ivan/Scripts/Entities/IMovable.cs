using UnityEngine;

// Movement capability of an entity. Keeping movement as a pluggable component instead of
// a base-class branch is what stops the static/dynamic x movable/immovable matrix from
// exploding into four classes.
public interface IMovable
{
    Vector3 Velocity { get; set; }
}
