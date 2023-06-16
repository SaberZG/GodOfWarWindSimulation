using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class WinWindMotorController : MonoBehaviour
{
    public MotorJoystick motorJoystick;
    public Slider SliderSpeed;
    public Text TxtSpeed;
    public Toggle TogDirectional;
    public Toggle TogOmni;
    public Toggle TogVortex;
    public Toggle TogMoving;

    public Slider SliderRadius;
    public Text TxtRadius;
    public Slider SliderForce;
    public Text TxtForce;

    public Button BtnDelete;
    private WindMotor m_WindMotor;

    // Start is called before the first frame update
    void Start()
    {
        SliderSpeed.onValueChanged.AddListener((float value) => OnSliderSpeedChanged(value));
        TogDirectional.onValueChanged.AddListener((bool isOn) => OnClickToggle(isOn, MotorType.Directional));
        TogOmni.onValueChanged.AddListener((bool isOn) => OnClickToggle(isOn, MotorType.Omni));
        TogVortex.onValueChanged.AddListener((bool isOn) => OnClickToggle(isOn, MotorType.Vortex));
        TogMoving.onValueChanged.AddListener((bool isOn) => OnClickToggle(isOn, MotorType.Moving));
        SliderRadius.onValueChanged.AddListener((float value) => OnSliderRadiusChanged(value));
        SliderForce.onValueChanged.AddListener((float value) => OnSliderForceChanged(value));
        
        EventManager.AddEvent(EventName.UpdateWinWindMotorDetail, (WindMotor windMotor) => InitWindMotor(windMotor));
        EventManager.AddEvent(EventName.DeleteWindHandler, (WindMotor windMotor) => DeleteWindMotor());
        BtnDelete.onClick.AddListener(() =>
        {
            if(m_WindMotor != null)
                EventManager.DispatchEvent(EventName.DeleteWindMotor, m_WindMotor);
        });
        gameObject.SetActive(false);
    }

    void OnSliderSpeedChanged(float value)
    {
        if (motorJoystick != null)
        {
            motorJoystick.Speed = value;
            TxtSpeed.text = "Speed:" + value.ToString("F2");
        }
    }
    void OnClickToggle(bool isOn, MotorType motorType)
    {
        if (m_WindMotor != null && isOn)
        {
            m_WindMotor.MotorType = motorType;
            EventManager.DispatchEvent(EventName.UpdateHandlerIcon);
        }
    }

    void OnSliderRadiusChanged(float value)
    {
        if (m_WindMotor != null)
        {
            m_WindMotor.Radius = value;
            TxtRadius.text = "Radius:" + value.ToString("F2");
        }
    }
    void OnSliderForceChanged(float value)
    {
        if (m_WindMotor != null)
        {
            m_WindMotor.Force = value;
            TxtForce.text = "Force:" + value.ToString("F2");
        }
    }

    private void InitWindMotor(WindMotor windMotor)
    {
        m_WindMotor = windMotor;
        if (m_WindMotor != null)
        {
            motorJoystick.target = m_WindMotor.transform;
            SliderSpeed.value = motorJoystick.Speed;
            TxtSpeed.text = "Speed:" + motorJoystick.Speed.ToString("F2");
            motorJoystick.gameObject.SetActive(true);
            TogDirectional.isOn = m_WindMotor.MotorType == MotorType.Directional;
            TogOmni.isOn = m_WindMotor.MotorType == MotorType.Omni;
            TogVortex.isOn = m_WindMotor.MotorType == MotorType.Vortex;
            TogMoving.isOn = m_WindMotor.MotorType == MotorType.Moving;

            SliderRadius.value = m_WindMotor.Radius;
            TxtRadius.text = "Radius:" + m_WindMotor.Radius.ToString("F2");

            SliderForce.value = m_WindMotor.Force;
            TxtForce.text = "Force:" + m_WindMotor.Force.ToString("F2");
            gameObject.SetActive(true);
        }
    }

    private void DeleteWindMotor()
    {
        motorJoystick.gameObject.SetActive(false);
        motorJoystick.target = null;
        m_WindMotor = null;
        gameObject.SetActive(false);
    }
}
