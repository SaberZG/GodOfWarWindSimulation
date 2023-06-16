using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIFacade
{
    private static UIFacade m_Instance;

    public static UIFacade Instance
    {
        get
        {
            if (m_Instance == null)
            {
                m_Instance = new UIFacade();
            }

            return m_Instance;
        }
    }

    public GameObject MainCanvas;
    public Camera MainCamera;
    public Camera UICamera;
    public Transform WindHandlerNode;
    public UIFacade()
    {
        MainCanvas = GameObject.Find("Canvas");
        MainCamera = Camera.main;
        GameObject cameraObj = GameObject.Find("UICamera");
        UICamera = cameraObj ? cameraObj.GetComponent<Camera>() : null;
        GameObject whnObj = GameObject.Find("Canvas/WindHandlerNode");
        WindHandlerNode = whnObj ? whnObj.transform : null;
    }

    public void GetWorldPosToScreenPos(Vector3 worldPos, out Vector2 screenPos)
    {
        screenPos = Vector2.zero;
        if (MainCamera)
        {
            RectTransform canvasRtm = MainCanvas.GetComponent<RectTransform>();
            float width = canvasRtm.sizeDelta.x;
            float height = canvasRtm.sizeDelta.y;
            screenPos = MainCamera.WorldToScreenPoint(worldPos);
            screenPos.x *= width / Screen.width;
            screenPos.y *= height / Screen.height;
            screenPos.x -= width * 0.5f;
            screenPos.y -= height * 0.5f;
        }
    }
}
