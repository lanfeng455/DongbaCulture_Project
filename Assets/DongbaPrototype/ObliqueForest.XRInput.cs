using UnityEngine;
using UnityEngine.XR;

namespace DongbaPrototype
{
    public partial class ObliqueForest
    {
        [System.NonSerialized] public bool XRExperienceReady;
        bool xrWasReady, xrActionHeld, xrPickupHeld, xrJumpHeld, xrAttackHeld, xrCareHeld;
        InputDevice xrLeftDevice, xrRightDevice;
        Vector3 xrMoveForward = Vector3.forward;

        [Header("Father interaction")]
        [Min(.1f)] public float FatherTalkRange = 2.6f;
        bool fatherSpoken;
        float fatherLineEndsAt;
        bool CanTalkToFather => FirstAxeActive && XRExperienceReady && !fatherSpoken && !woodenAxe
            && player != null && storyFatherRoot != null && storyFatherRoot.gameObject.activeInHierarchy
            && (storyFatherRoot.position - Feet).sqrMagnitude <= FatherTalkRange * FatherTalkRange;
        bool FatherOpeningSubtitleVisible => FirstAxeActive && XRExperienceReady && Time.unscaledTime < fatherLineEndsAt;

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
            bool action = XRButton(right, CommonUsages.primaryButton); // A
            bool pickup = XRButton(right, CommonUsages.secondaryButton); // B
            bool jump = XRButton(left, CommonUsages.primaryButton); // X
            bool attack = XRButton(right, CommonUsages.triggerButton);
            if (right.isValid && right.TryGetFeatureValue(CommonUsages.trigger, out float trigger)) attack |= trigger > .65f;
            bool care = XRButton(left, CommonUsages.gripButton);
            bool connectedChanged = !xrLeftDevice.Equals(left) || !xrRightDevice.Equals(right);
            xrLeftDevice = left; xrRightDevice = right;
            if (!DispatchXRButtons(action, pickup, jump, attack, care, storyLocked, connectedChanged)) return Vector3.zero;

            if (left.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 hotbarStick)) UpdateRiverHotbar(hotbarStick.x);
            if (!right.isValid) return Vector3.zero;
            if (!right.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick)) return Vector3.zero;
            const float deadzone = .18f;
            float magnitude = Mathf.Min(stick.magnitude, 1);
            if (magnitude <= deadzone) return Vector3.zero;
            var forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (forward.sqrMagnitude > .01f) xrMoveForward = forward.normalized;
            var rightward = Vector3.Cross(Vector3.up, xrMoveForward);
            var direction = rightward * stick.x + xrMoveForward * stick.y;
            return direction.normalized * ((magnitude - deadzone) / (1 - deadzone));
        }

        bool DispatchXRButtons(bool action, bool pickup, bool jump, bool attack, bool care, bool storyLocked, bool connectedChanged = false)
        {
            bool armed = XRExperienceReady && xrWasReady && !connectedChanged;
            bool actionDown = armed && action && !xrActionHeld;
            bool pickupDown = armed && pickup && !xrPickupHeld;
            bool jumpDown = armed && jump && !xrJumpHeld;
            bool attackDown = armed && attack && !xrAttackHeld;
            bool careDown = armed && care && !xrCareHeld;
            // Latch before dispatch so a held button cannot fire on placement or reconnection.
            xrActionHeld = action;
            xrPickupHeld = pickup;
            xrJumpHeld = jump;
            xrAttackHeld = attack;
            xrCareHeld = care;
            xrWasReady = XRExperienceReady;
            if (!XRExperienceReady) return false;

            bool dialogue = fatherIntroActive || ManualStoryDialogueActive;
            if (actionDown && (dialogue || !storyLocked)) HandleXRPrimaryAction();
            if (storyLocked || dialogue) return false;
            if (pickupDown) TryPickUpWood();
            if (jumpDown && jumpAge < 0 && attackAge < 0) jumpAge = 0;
            if (attackDown) HandleXRTriggerAction();
            if (careDown && eastPaperProgress <= 0) Care();
            return true;
        }

        void HandleXRPrimaryAction()
        {
            if (!XRExperienceReady) return;
            if (fatherIntroActive) { AdvanceFatherIntro(); return; }
            if (ManualStoryDialogueActive) { AdvanceManualStoryDialogue(); return; }
            if (TryCraftFirstAxe() || TryCraftSpear()) return;
            if (CanTalkToFather)
            {
                fatherSpoken = true;
                fatherLineEndsAt = Time.unscaledTime + 3f;
                return;
            }
        }

        void HandleXRTriggerAction()
        {
            if (!XRExperienceReady || eastPaperProgress > 0 || attackAge >= 0) return;
            attackAge = 0;
            hit = false;
        }
    }
}
