// using Unity.VisualScripting;
using UnityEngine;
using System;

public abstract class ReactToCollider: MonoBehaviour
{
    public abstract void MyReact();
    
    private void OnCollisionEnter(Collision collision)
    {
        MyReact();
    }
}
