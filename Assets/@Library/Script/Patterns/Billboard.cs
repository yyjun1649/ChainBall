using System;
using UnityEngine;

public class Billboard : MonoBehaviour
{
    public void LateUpdate()
    {
        transform.forward = MainCamera.Camera.transform.forward;
    }
}
