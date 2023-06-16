// 测试脚本，用于定时生成ECS模式下的风力发动机Entity
using UnityEngine;

public class AutoSpawnWindMotor : MonoBehaviour
{
    public GameObject GoMotor;
    private float m_timeGap = 0.0f;
    private float m_spawnTimeGap = 3.0f;
    private float m_lastSpawnTime = 0f;
    
    // Update is called once per frame
    void Update()
    {
        if (Time.time - m_lastSpawnTime > m_spawnTimeGap)
        {
            var go = Instantiate(GoMotor);
            WindMotor motor = go.GetComponent<WindMotor>();
            motor.Loop = false;
            float windTypeRandom = Random.Range(0.0f, 100.0f);
            if (windTypeRandom > 75.0f)
            {
                motor.MotorType = MotorType.Directional;
            }
            else if (windTypeRandom > 50.0f)
            {
                motor.MotorType = MotorType.Omni;
            }
            else if (windTypeRandom > 25.0f)
            {
                motor.MotorType = MotorType.Vortex;
            }
            else
            {
                motor.MotorType = MotorType.Moving;
            }

            float posX = Random.Range(-15, 15);
            float posY = Random.Range(-7, 7);
            float posZ = Random.Range(-15, 15);
            go.transform.position = new Vector3(posX, posY, posZ);

            float force = Random.Range(1.0f, 2f);
            motor.Force = force;

            float range = Random.Range(1.0f, 2f);
            motor.Radius = range;

            float lifeTime = Random.Range(1.0f, 3.5f);
            motor.LifeTime = lifeTime;
            
            motor.Asix = Vector3.up;

            if (WindMgrECS.Instance)
            {
                WindMgrECS.Instance.TransferMotorToECSEntity(motor);
            }
            Destroy(go, 1.0f);
            m_lastSpawnTime = Time.time;
        }
    }
}
