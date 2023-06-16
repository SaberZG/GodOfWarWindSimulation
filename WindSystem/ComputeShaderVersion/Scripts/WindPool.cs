using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WindPool : MonoBehaviour
{
    private static WindPool m_Instance;

    public static WindPool Instance
    {
        get
        {
            return m_Instance;
        }
    }

    public Transform WindContainer;

    public GameObject WindMotorPrefab;
    // Type : WindMotor
    private List<GameObject> m_WindMotorPool = new List<GameObject>();
    private int MaxinumNum = 10;
    private int m_WindMotorCurNum = 0;

    void Awake()
    {
        m_Instance = this;
    }
    void Start()
    {
        WindContainer = GameObject.Find("WindContainer").transform;
        m_WindMotorPool.Clear();
        m_WindMotorCurNum = 0;
    }

    public GameObject PopWindMotor()
    {
        GameObject output;
        if (m_WindMotorCurNum > 0)
        {
            m_WindMotorCurNum = m_WindMotorCurNum - 1;
            output = m_WindMotorPool[m_WindMotorCurNum];
            m_WindMotorPool.RemoveAt(m_WindMotorCurNum);
        }
        else
        {
            output = GameObject.Instantiate(WindMotorPrefab, WindContainer);
            // output = new GameObject("WindMotor");
            // output.transform.SetParent(WindContainer);
            // output.AddComponent<WindMotor>();
        }
        output.SetActive(true);
        return output;
    }
    public void PushWindMotor(GameObject windObj)
    {
        windObj.SetActive(false);
        if (m_WindMotorCurNum < MaxinumNum)
        {
            m_WindMotorPool.Add(windObj);
            m_WindMotorCurNum++;
        }
        else
        {
            Object.DestroyImmediate(windObj);
        }
    }
}
