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
    static bool armed,aHeld,xHeld,triggerHeld,languageHeld,dialogueHold,helpHeld;
    static int helpFrame=-1,jumpFrame=-1,attackFrame=-1,interactFrame=-1;
    static InputDevice lastLeft,lastRight;
#if UNITY_EDITOR
    public static bool Simulate;
    public static Vector2 TestMove;
    public static bool TestA,TestX,TestTrigger,TestLanguage,TestB;
#endif
    public static void Begin(){Reset();Active=true;}
    public static void Reset(){MenuOpen=helpHeld=Active=armed=aHeld=xHeld=triggerHeld=languageHeld=dialogueHold=false;helpFrame=-1;Move=Vector2.zero;jumpFrame=attackFrame=interactFrame=-1;}
    public static void LatchDialogue(){dialogueHold=true;}
    static bool Button(InputDevice d,InputFeatureUsage<bool> usage)=>d.isValid&&d.TryGetFeatureValue(usage,out bool value)&&value;
    public static void Poll(bool ready){
        if(!Active)return;
        var left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        bool reconnect=!left.Equals(lastLeft)||!right.Equals(lastRight);lastLeft=left;lastRight=right;
        left.TryGetFeatureValue(CommonUsages.primary2DAxis,out Vector2 stick);
        bool help=Button(right,CommonUsages.secondaryButton);
        bool a=Button(right,CommonUsages.primaryButton),x=Button(left,CommonUsages.primaryButton),trigger=Button(right,CommonUsages.triggerButton),language=Button(right,CommonUsages.primary2DAxisClick);
        if(right.TryGetFeatureValue(CommonUsages.trigger,out float pressure))trigger|=pressure>.65f;
        ready&=left.isValid&&right.isValid;
#if UNITY_EDITOR
        if(Simulate){help=TestB;stick=TestMove;a=TestA;x=TestX;trigger=TestTrigger;language=TestLanguage;ready=true;reconnect=false;}
#endif
        bool accept=ready&&armed&&!reconnect;
        Move=accept&&stick.magnitude>.18f?Vector2.ClampMagnitude(stick.normalized*((stick.magnitude-.18f)/.82f),1):Vector2.zero;
        if(accept&&help&&!helpHeld)helpFrame=Time.frameCount;helpHeld=help;
        if(accept&&a&&!aHeld)jumpFrame=Time.frameCount;
        if(accept&&x&&!xHeld)interactFrame=Time.frameCount;
        if(accept&&trigger&&!triggerHeld)attackFrame=Time.frameCount;
        if(accept&&language&&!languageHeld)DongbaLanguage.Set(DongbaLanguage.Chinese?DongbaLocale.English:DongbaLocale.Chinese,true);
        aHeld=a;xHeld=x;triggerHeld=trigger;languageHeld=language;armed=ready;
        if(!a)dialogueHold=false;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Initialize(){Reset();}
}}
