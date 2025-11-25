using UnityEngine;

public class CameraTilt : MonoBehaviour
{
    public bool startTilt = false;
    public float tiltSpeed = 20f;
    public float targetAngle = 75f; // เอียงลง 75 องศาเหมือนล้ม

    void Update()
    {
        if (startTilt)
        {
            float angle = Mathf.LerpAngle(transform.localEulerAngles.x, targetAngle, Time.deltaTime * tiltSpeed);
            transform.localEulerAngles = new Vector3(angle, transform.localEulerAngles.y, transform.localEulerAngles.z);
        }
    }

    public void TriggerTilt()
    {
        startTilt = true;
    }
}
