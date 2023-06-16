using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FixedJoystick : Joystick
{
    public Transform target;
    public float Speed = 1.0f;
    private void Update()
    {
        if (target != null)
        {
            Vector3 position = target.position;
            position += Vertical * target.forward * Time.deltaTime * Speed;
            position += Horizontal * target.right * Time.deltaTime * Speed;
            target.position = position;
        }
    }
}