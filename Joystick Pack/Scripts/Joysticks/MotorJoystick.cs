using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MotorJoystick : Joystick
{
    public Transform MainCamera;
    public Transform target;
    public float Speed = 1.0f;

    private void Start()
    {
        base.Start();
        MainCamera = Camera.main.transform;
    }
    private void Update()
    {
        if (target != null)
        {
            Vector3 forward = MainCamera.forward;
            forward.y = 0;
            forward = Vector3.Normalize(forward);
            
            Vector3 right = MainCamera.right;
            right.y = 0;
            right = Vector3.Normalize(right);
            
            Vector3 position = target.position;
            position += Vertical * forward * Time.deltaTime * Speed;
            position += Horizontal * right * Time.deltaTime * Speed;
            target.position = position;
        }
    }
}