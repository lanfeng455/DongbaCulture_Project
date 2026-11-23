using UnityEngine;
public class ShakeToCollider2D : ReactToCollider
{
    [SerializeField] private float duration = .3f;
    [SerializeField] private float rotateAmount = 1f;
    [SerializeField] private Transform target;
    Quaternion initialRotation;
    float elapsed;
    bool shaking;
    void Awake(){if(target==null)target=transform;initialRotation=target.localRotation;}
    public override void MyReact(){elapsed=0;shaking=true;}
    void Update(){
        if(!shaking||target==null)return;
        elapsed+=Time.deltaTime;float t=Mathf.Clamp01(elapsed/Mathf.Max(.01f,duration));
        target.localRotation=initialRotation*Quaternion.Euler(0,0,Mathf.Sin(t*Mathf.PI*6)*rotateAmount*(1-t));
        if(t>=1){target.localRotation=initialRotation;shaking=false;}
    }
    void OnDisable(){if(target!=null)target.localRotation=initialRotation;shaking=false;}
}
