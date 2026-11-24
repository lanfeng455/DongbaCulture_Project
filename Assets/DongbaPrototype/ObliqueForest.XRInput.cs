using UnityEngine;
using UnityEngine.XR;

namespace DongbaPrototype
{
    public partial class ObliqueForest
    {
        [System.NonSerialized] public bool XRExperienceReady;
        bool xrWasReady, xrJumpHeld, xrAttackHeld, xrCareHeld, xrLanguageHeld;
#if UNITY_EDITOR
        bool xrSimulating; Vector2 xrSimStick; bool xrSimA, xrSimTrigger, xrSimX;
#endif
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
#if UNITY_EDITOR
            if(xrSimulating)return ProcessXRInput(xrSimStick,xrSimA,xrSimTrigger,xrSimX,false,storyLocked,false);
#endif
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            bool jump = XRButton(right, CommonUsages.primaryButton); // A
            bool attack = XRButton(right, CommonUsages.triggerButton);
            if (right.isValid && right.TryGetFeatureValue(CommonUsages.trigger, out float trigger)) attack |= trigger > .65f;
            bool care = XRButton(left, CommonUsages.primaryButton); // X
            bool connectedChanged = !xrLeftDevice.Equals(left) || !xrRightDevice.Equals(right);
            xrLeftDevice = left; xrRightDevice = right;
            left.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick);
            UpdateXRHelpInput(XRButton(right,CommonUsages.secondaryButton),XRExperienceReady&&xrWasReady&&!connectedChanged&&!storyLocked);
            bool language=XRButton(right,CommonUsages.primary2DAxisClick);
            return ProcessXRInput(stick,jump,attack,care,language,storyLocked,connectedChanged);
        }

        Vector3 ProcessXRInput(Vector2 stick,bool jump,bool attack,bool care,bool language,bool storyLocked,bool connectedChanged)
        {
            bool armed=XRExperienceReady&&xrWasReady&&!connectedChanged;
            bool jumpDown=armed&&jump&&!xrJumpHeld;
            bool attackDown=armed&&attack&&!xrAttackHeld;
            bool careDown=armed&&care&&!xrCareHeld;
            bool languageDown=armed&&language&&!xrLanguageHeld;
            xrJumpHeld=jump;xrAttackHeld=attack;xrCareHeld=care;xrLanguageHeld=language;xrWasReady=XRExperienceReady;
            if(!XRExperienceReady)return Vector3.zero;
            bool dialogue=fatherIntroActive||ManualStoryDialogueActive;
            if(jumpDown&&dialogue){
                if(!TryCompleteForestSubtitle()){
                    if(fatherIntroActive)AdvanceFatherIntro();else AdvanceManualStoryDialogue();
                }
            }
            if(languageDown&&!dialogue)DongbaLanguage.Set(DongbaLanguage.Chinese?DongbaLocale.English:DongbaLocale.Chinese,true);
            if(!storyLocked&&!dialogue&&armed){
                if(jumpDown&&jumpAge<0){jumpAge=0;PlayForestSfx("jump",Feet);}
                if(attackDown&&eastPaperProgress<=0&&attackAge<0){
                    var desired=XRStickMovement(stick);
                    if(desired.sqrMagnitude>0){heading=desired.normalized;if(Mathf.Abs(desired.x)>.001f)facing=desired.x>0?1:-1;}
                    BeginAxeAttack();
                }
                if(careDown&&eastPaperProgress<=0)Care();
            }
            if(storyLocked||dialogue||!armed)return Vector3.zero;
            return XRStickMovement(stick);
        }

        Vector3 XRStickMovement(Vector2 stick){
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
