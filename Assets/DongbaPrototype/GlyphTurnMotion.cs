using UnityEngine;
namespace DongbaPrototype {
// One presentation curve for both PC and tracked-camera forests. Logic stays instant;
// a critically damped signed width makes reversals continuous, including rapid taps.
public sealed class GlyphTurnMotion {
    public float Value { get; private set; }=1;
    float velocity;
    public int Facing=>Value<0?-1:1;
    public float Width=>Mathf.Max(.035f,Mathf.Abs(Value));
    public void Snap(int direction){Value=direction<0?-1:1;velocity=0;}
    public void Tick(int direction,float dt,float duration){
        int target=direction<0?-1:1;
        if(duration<=0){Snap(target);return;}
        Value=Mathf.SmoothDamp(Value,target,ref velocity,Mathf.Max(.015f,duration*.22f),Mathf.Infinity,dt);
        Value=Mathf.Clamp(Value,-1,1);
        if(Mathf.Abs(Value-target)<.001f&&Mathf.Abs(velocity)<.025f)Snap(target);
    }
}
}
