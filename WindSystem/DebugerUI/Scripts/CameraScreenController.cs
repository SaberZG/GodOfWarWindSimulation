using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraScreenController : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public Transform target;
    public float Speed = 1.0f;
    private Vector2 m_ClickPos;
    private Vector3 m_TargetRotation;
    public virtual void OnPointerDown(PointerEventData eventData)
    {
        if (target != null)
        {
            m_ClickPos = eventData.position;
            m_TargetRotation = target.localEulerAngles;
        }
    }

    public virtual void OnDrag(PointerEventData eventData)
    {
        if (target != null)
        {
            Vector2 posOffset = eventData.position - m_ClickPos;
            target.localRotation = Quaternion.Euler(m_TargetRotation + new Vector3(-posOffset.y * Speed, posOffset.x * Speed));
        }
    }
    public virtual void OnPointerUp(PointerEventData eventData)
    {
        
    }
}
