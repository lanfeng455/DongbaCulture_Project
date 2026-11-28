using UnityEngine;
using UnityEngine.XR;
namespace DongbaPrototype {
// Native controller input, not emulated keyboard events. A press belongs to one action.
public static class MRPuzzleInput {
    public static bool Active {get;private set;}
    public static bool MenuOpen {get;set;}
    public static bool HelpPressed=>Active&&helpFrame==Time.frameCount;
    public static Vector2 Move {get;private set;}
    public static bool JumpPressed=>Active&&jumpFrame==Time.frameCount;
    public static bool AttackPressed=>Active&&attackFrame==Time.frameCount;
    public static bool InteractPressed=>Active&&interactFrame==Time.frameCount;
    public static bool DialogueHold=>Active&&dialogueHold;
    static bool leftArmed,rightArmed,aHeld,xHeld,triggerHeld,languageHeld,dialogueHold,helpHeld;
    static int helpFrame=-1,jumpFrame=-1,attackFrame=-1,interactFrame=-1;
    static InputDevice lastLeft,lastRight;
#if UNITY_EDITOR
    public static bool Simulate;
    public static bool TestLeftValid=true,TestRightValid=true;
    public static Vector2 TestMove;
    public static bool TestA,TestX,TestTrigger,TestLanguage,TestB;
#endif
    public static void Begin(){Reset();Active=true;}
    public static void Reset(){MenuOpen=helpHeld=Active=leftArmed=rightArmed=aHeld=xHeld=triggerHeld=languageHeld=dialogueHold=false;helpFrame=-1;Move=Vector2.zero;jumpFrame=attackFrame=interactFrame=-1;}
    public static void LatchDialogue(){dialogueHold=true;}
    static bool Button(InputDevice d,InputFeatureUsage<bool> usage)=>d.isValid&&d.TryGetFeatureValue(usage,out bool value)&&value;
    public static void Poll(bool ready){
        if(!Active)return;
        var left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        bool leftReconnect=!left.Equals(lastLeft),rightReconnect=!right.Equals(lastRight);lastLeft=left;lastRight=right;
        bool leftReady=ready&&left.isValid,rightReady=ready&&right.isValid;
        left.TryGetFeatureValue(CommonUsages.primary2DAxis,out Vector2 stick);
        bool help=Button(right,CommonUsages.secondaryButton);
        bool a=Button(right,CommonUsages.primaryButton),x=Button(left,CommonUsages.primaryButton),trigger=Button(right,CommonUsages.triggerButton),language=Button(right,CommonUsages.primary2DAxisClick);
        if(right.TryGetFeatureValue(CommonUsages.trigger,out float pressure))trigger|=pressure>.65f;
#if UNITY_EDITOR
        if(Simulate){help=TestB;stick=TestMove;a=TestA;x=TestX;trigger=TestTrigger;language=TestLanguage;leftReady=ready&&TestLeftValid;rightReady=ready&&TestRightValid;leftReconnect=rightReconnect=false;}
#endif
        // A sleeping right hand must not disable the left joystick (and vice versa).
        // Re-arm each hand independently; held buttons on reconnect are not new presses.
        bool acceptLeft=leftReady&&leftArmed&&!leftReconnect,acceptRight=rightReady&&rightArmed&&!rightReconnect;
        Move=acceptLeft&&stick.magnitude>.18f?Vector2.ClampMagnitude(stick.normalized*((stick.magnitude-.18f)/.82f),1):Vector2.zero;
        if(acceptRight&&help&&!helpHeld)helpFrame=Time.frameCount;helpHeld=help;
        if(acceptRight&&a&&!aHeld)jumpFrame=Time.frameCount;
        if(acceptLeft&&x&&!xHeld)interactFrame=Time.frameCount;
        if(acceptRight&&trigger&&!triggerHeld)attackFrame=Time.frameCount;
        if(acceptRight&&language&&!languageHeld)DongbaLanguage.Set(DongbaLanguage.Chinese?DongbaLocale.English:DongbaLocale.Chinese,true);
        aHeld=a;xHeld=x;triggerHeld=trigger;languageHeld=language;leftArmed=leftReady;rightArmed=rightReady;
        if(!rightReady||!a)dialogueHold=false;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Initialize(){Reset();}
}}
