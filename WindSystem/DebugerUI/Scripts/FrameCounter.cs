using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class FrameCounter : MonoBehaviour
{
    public Text ShowText;

    private int m_Frame;

    public float UpdateTimer = 0.5f;

    private float m_runtime = 0.0f;
    // Update is called once per frame
    void Update()
    {
        m_Frame++;
        m_runtime += Time.deltaTime;
        if (m_runtime >= UpdateTimer)
        {
            ShowText.text = "FPS:" + Mathf.RoundToInt(m_Frame / m_runtime) + "\nApplication.targetFrameRate = " + Application.targetFrameRate;
            m_runtime = 0.0f;
            m_Frame = 0;
        }
    }
}
