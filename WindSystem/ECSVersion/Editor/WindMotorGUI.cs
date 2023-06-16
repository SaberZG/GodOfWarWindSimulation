using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(WindMotor))]
public class WindMotorGUI : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("添加发动机到ECS"))
        {
            GameObject go = Selection.activeGameObject;
            if (go != null && go.GetComponent<WindMotor>() != null)
            {
                go.GetComponent<WindMotor>().TransferMotorToECSEntity();
            }
        }
    }
}
