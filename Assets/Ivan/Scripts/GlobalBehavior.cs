using UnityEngine;
using DG.Tweening;


public class GlobalBehavior : MonoBehaviour
{
    private static GlobalBehavior s_instance;
    private void Awake()
    {
        if (s_instance == null)
        {
            s_instance = this;
        }
        else
        {
            Destroy(this.gameObject);
        }
    }
    private void OnDestroy()
    {
        if (s_instance == this)
        {
            s_instance = null;
        }
    }

    public static GlobalBehavior Instance => s_instance;

    public void DoShakeCamera(float shakeAmount, float shakeDuration)
    {
        Camera.main.DOShakePosition(shakeAmount, shakeDuration);
    }
}
