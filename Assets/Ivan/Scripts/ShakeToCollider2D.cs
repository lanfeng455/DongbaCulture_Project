using System;
using UnityEngine;
using DG.Tweening;

public class ShakeToCollider2D :  ReactToCollider
{
    [SerializeField] private float duration;
    [SerializeField] private float rotateAmount = 1f;

    [SerializeField] private Transform target;

    private Quaternion initialRotation;
    // public void MyReact()
    // {
    //     transform.DOShakeRotation(duration, rotateAmount).SetEase(Ease.OutBack);
    // }

    private void Awake()
    {
        initialRotation = target.localRotation;
    }

    public override void MyReact()
    {
        target.DOKill();
        target.localRotation = initialRotation;
        target.DOShakeRotation(duration, rotateAmount).SetEase(Ease.OutBack);
    }
}
