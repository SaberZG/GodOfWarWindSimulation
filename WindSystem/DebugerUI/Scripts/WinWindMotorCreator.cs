using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WinWindMotorCreator : MonoBehaviour
{
    public GameObject WindHandlerPrefab;
    public Button BtnDirectional;
    public Button BtnOmni;
    public Button BtnVortex;
    public Button BtnMoving;
    // Start is called before the first frame update
    public Dictionary<WindMotor, GameObject> m_WindHandlerDic = new Dictionary<WindMotor, GameObject>();
    void Start()
    {
        BtnDirectional.onClick.AddListener(()=>EventManager.DispatchEvent(EventName.CreateWindMotor, MotorType.Directional));
        BtnOmni.onClick.AddListener(()=>EventManager.DispatchEvent(EventName.CreateWindMotor, MotorType.Omni));
        BtnVortex.onClick.AddListener(()=>EventManager.DispatchEvent(EventName.CreateWindMotor, MotorType.Vortex));
        BtnMoving.onClick.AddListener(()=>EventManager.DispatchEvent(EventName.CreateWindMotor, MotorType.Moving));
        EventManager.AddEvent(EventName.UpdateWinWindMotor, (WindMotor windMotor) => OnUpdateWindMotor(windMotor));
        EventManager.AddEvent(EventName.DeleteWindHandler, (WindMotor windMotor) => OnDeleteWindMotor(windMotor));
    }

    void OnUpdateWindMotor(WindMotor windMotor)
    {
        if (windMotor != null)
        {
            GameObject windHandlerObj = GameObject.Instantiate(WindHandlerPrefab, UIFacade.Instance.WindHandlerNode);
            UIWindHandler windHandler = windHandlerObj.GetComponent<UIWindHandler>();
            if (windHandler != null)
            {
                windHandler.UpdateWindMotor(windMotor);
            }
            m_WindHandlerDic.Add(windMotor, windHandlerObj);
        }
    }

    void OnDeleteWindMotor(WindMotor windMotor)
    {
        if (windMotor != null && m_WindHandlerDic.ContainsKey(windMotor))
        {
            GameObject windHandlerObj;
            m_WindHandlerDic.Remove(windMotor, out windHandlerObj);
            DestroyImmediate(windHandlerObj);
        }
    }

    void Update()
    {
        foreach (var keyvalue in m_WindHandlerDic)
        {
            keyvalue.Value.GetComponent<UIWindHandler>().UpdateTransformPosition();
        }
    }
}
