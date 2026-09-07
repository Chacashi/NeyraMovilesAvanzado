using Sirenix.OdinInspector;
using System;
using UnityEngine;
using UnityEngine.Events;

public class Test : MonoBehaviour
{
    public UnityEvent gaaaa;

    [Button]
    public void test()
    {
       gaaaa.Invoke();
    }
}
