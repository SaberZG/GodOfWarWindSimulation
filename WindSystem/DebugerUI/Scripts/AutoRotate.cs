using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoRotate : MonoBehaviour
{

    public float Speed = 1f;
    // Update is called once per frame
    void Update()
    {
        Vector3 curRotate = this.transform.localEulerAngles;
        curRotate.y += Speed * Time.deltaTime;
        this.transform.rotation = Quaternion.Euler(curRotate);
    }
}
