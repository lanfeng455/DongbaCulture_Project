using UnityEngine;

namespace DongbaPrototype
{
    // Sprites use XY as their artwork plane. Rotate around world Y only so
    // their foot pivots stay on the scanned surface even when looking down.
    [DefaultExecutionOrder(10000)]
    public sealed class XRUprightBillboard : MonoBehaviour
    {
        Camera viewer;
        ObliqueForest forest;
        Quaternion authoredFacing, lastWritten;
        Quaternion worldTilt = Quaternion.identity;
        float roll;
        bool written, artworkScaled;

        public void Initialize(ObliqueForest owner, Camera camera, Quaternion originalFacing)
        {
            forest = owner;
            viewer = camera;
            authoredFacing = originalFacing;
        }

        public void ApplyArtworkScale(float multiplier)
        {
            if (artworkScaled || multiplier <= 1f) return;
            // Parent cards scale attached art with them. Scaling those children again
            // would make held tools and layered tree leaves disproportionately large.
            for (var parent = transform.parent; parent != null; parent = parent.parent)
                if (parent.TryGetComponent<XRUprightBillboard>(out _)) return;
            transform.localScale *= multiplier;
            artworkScaled = true;
        }

        public void SetWorldTilt(Quaternion tilt) => worldTilt = tilt;

        void LateUpdate()
        {
            if (forest == null || !forest.XRTrackingControlsCamera || viewer == null) return;
            // Attached artwork (axe, leaves, etc.) must inherit its parent card's
            // turn rather than rotate independently and break the composition.
            for (var parent = transform.parent; parent != null; parent = parent.parent)
                if (parent.TryGetComponent<XRUprightBillboard>(out _)) return;
            var current = transform.rotation;
            if (!written || Quaternion.Angle(current, lastWritten) > .01f)
                roll = (Quaternion.Inverse(authoredFacing) * current).eulerAngles.z;
            var awayFromViewer = transform.position - viewer.transform.position;
            awayFromViewer.y = 0;
            if (awayFromViewer.sqrMagnitude < .0001f) return;
            lastWritten = worldTilt * Quaternion.LookRotation(awayFromViewer, Vector3.up) * Quaternion.Euler(0, 0, roll);
            transform.rotation = lastWritten;
            written = true;
        }
    }
}
