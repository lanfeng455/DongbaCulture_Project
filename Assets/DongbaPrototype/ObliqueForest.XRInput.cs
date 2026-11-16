using UnityEngine;
using UnityEngine.XR;

namespace DongbaPrototype
{
    public partial class ObliqueForest
    {
        [System.NonSerialized] public bool XRExperienceReady;
        bool xrWasReady, xrJumpHeld, xrAttackHeld, xrCareHeld;
        InputDevice xrLeftDevice, xrRightDevice;
        Vector3 xrMoveForward = Vector3.forward;

        public void ConfigureXRHUD(float worldUnitsPerMetre)
        {
            ConfigureWorldXRHUD(worldUnitsPerMetre);
        }

        static bool XRButton(InputDevice device, InputFeatureUsage<bool> feature)
        {
            return device.isValid && device.TryGetFeatureValue(feature, out bool pressed) && pressed;
        }

        Vector3 ReadXRInput(Vector3 keyboardMove, bool storyLocked)
        {
            if (!XRTrackingControlsCamera) return keyboardMove;
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            bool jump = XRButton(right, CommonUsages.primaryButton); // A
            bool attack = XRButton(right, CommonUsages.triggerButton);
            if (right.isValid && right.TryGetFeatureValue(CommonUsages.trigger, out float trigger)) attack |= trigger > .65f;
            bool care = XRButton(left, CommonUsages.primaryButton); // X
            bool connectedChanged = !xrLeftDevice.Equals(left) || !xrRightDevice.Equals(right);
            bool armed = XRExperienceReady && xrWasReady && !connectedChanged;
            bool jumpDown = armed && jump && !xrJumpHeld;
            bool attackDown = armed && attack && !xrAttackHeld;
            bool careDown = armed && care && !xrCareHeld;
            // Synchronize while scanning, tracking is lost, or devices reconnect.
            // The trigger used to place the forest must not also swing the axe.
            xrJumpHeld = jump; xrAttackHeld = attack; xrCareHeld = care;
            xrWasReady = XRExperienceReady;
            xrLeftDevice = left; xrRightDevice = right;
            if (!XRExperienceReady) return Vector3.zero;

            bool dialogue = fatherIntroActive || ManualStoryDialogueActive;
            if (jumpDown && dialogue)
            {
                if (fatherIntroActive) AdvanceFatherIntro();
                else AdvanceManualStoryDialogue();
            }
            if (!storyLocked && !dialogue && armed)
            {
                if (jumpDown && jumpAge < 0) jumpAge = 0;
                if (attackDown && eastPaperProgress <= 0 && attackAge < 0) { attackAge = 0; hit = false; }
                if (careDown && eastPaperProgress <= 0) Care();
            }
            if (storyLocked || dialogue || !left.isValid) return Vector3.zero;
            if (!left.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick)) return Vector3.zero;
            const float deadzone = .18f;
            float magnitude = Mathf.Min(stick.magnitude, 1);
            if (magnitude <= deadzone) return Vector3.zero;
            var forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (forward.sqrMagnitude > .01f) xrMoveForward = forward.normalized;
            var rightward = Vector3.Cross(Vector3.up, xrMoveForward);
            var direction = rightward * stick.x + xrMoveForward * stick.y;
            return direction.normalized * ((magnitude - deadzone) / (1 - deadzone));
        }
    }
}
