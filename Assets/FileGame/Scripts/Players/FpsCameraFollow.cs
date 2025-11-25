using UnityEngine;

[DisallowMultipleComponent]
public class FpsCameraFollow : MonoBehaviour
{
    [Header("Follow Target (ตำแหน่งหัว)")]
    public Transform followTarget;               // ตำแหน่งศีรษะ/หัว
    public Vector3 offset = Vector3.zero;
    [Range(1f, 30f)] public float positionSmooth = 12f;

    [Header("Yaw Options (หมุนตามผู้เล่น)")]
    public bool followYaw = true;                // ให้กล้องหมุน yaw ตามผู้เล่น
    public Transform yawSource;                  // ใส่ Transform ของ Player (ที่หมุน Y)
    public bool forceChildYawIfParented = true;  // ถ้ากล้องเป็นลูกของ Player ให้ใช้ yaw ของพ่อโดยอัตโนมัติ

    [Header("Pitch Control (คุมก้ม/เงยที่กล้อง)")]
    public bool controlPitchHere = false;        // ถ้า true กล้องจะคุม pitch เอง
    public float mouseSensitivityY = 1.2f;
    public float minPitch = -80f, maxPitch = 80f;

    [Header("Head Bob")]
    public bool enableHeadBob = true;
    public float bobAmplitudeWalk = 0.02f;
    public float bobAmplitudeSprint = 0.035f;
    public float bobFrequencyWalk = 7.5f;
    public float bobFrequencySprint = 10.5f;

    [Header("FOV Kick")]
    public Camera cam;
    public float baseFov = 60f;
    public float sprintFov = 68f;
    [Range(1f, 20f)] public float fovLerp = 10f;

    [Header("Optional Link to Controller")]
    public PlayerControllerTest controller;      // ไว้ดู IsSprinting / IsCrouching

#if ENABLE_INPUT_SYSTEM
    private UnityEngine.InputSystem.Mouse ms => UnityEngine.InputSystem.Mouse.current;
#endif

    float _pitch;
    float _bobT;

    void Reset() { cam = GetComponent<Camera>(); }
    void Awake() { if (!cam) cam = GetComponent<Camera>(); if (cam) cam.fieldOfView = baseFov; }

    void LateUpdate()
    {
        if (!followTarget) return;

        // --- 1) ตำแหน่ง: ตามหัว + head-bob ---
        Vector3 targetPos = followTarget.position + offset;

        if (enableHeadBob && controller)
        {
            bool sprint = controller.IsSprinting;
            float amp = sprint ? bobAmplitudeSprint : bobAmplitudeWalk;
            float freq = sprint ? bobFrequencySprint : bobFrequencyWalk;

            _bobT += Time.deltaTime * freq * (sprint ? 1.25f : 1f);
            float bobY = Mathf.Sin(_bobT) * amp;
            float bobX = Mathf.Sin(_bobT * 0.5f) * amp * 0.7f;
            targetPos += transform.right * bobX + Vector3.up * bobY;
        }

        transform.position = Vector3.Lerp(transform.position, targetPos, 1f - Mathf.Exp(-positionSmooth * Time.deltaTime));

        // --- 2) Pitch (ก้ม/เงย) ที่กล้อง (ถ้าเลือกคุมที่นี่) ---
        if (controlPitchHere)
        {
            float dy = 0f;
#if ENABLE_INPUT_SYSTEM
            if (ms != null) dy += -ms.delta.ReadValue().y * 0.1f;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            dy += -Input.GetAxis("Mouse Y") * 10f;
#endif
            _pitch = Mathf.Clamp(_pitch + dy * mouseSensitivityY, minPitch, maxPitch);
        }

        // --- 3) Yaw (หันซ้าย/ขวา) ตามผู้เล่น ---
        Transform yawRef = yawSource;

        // ถ้ากล้องเป็นลูกของ Player และเลือกให้บังคับตามพ่อ ก็ใช้พ่อเป็น yawSource
        if (forceChildYawIfParented && transform.parent != null && yawRef == null)
            yawRef = transform.parent;

        if (followYaw && yawRef != null)
        {
            float yaw = yawRef.eulerAngles.y;
            if (controlPitchHere)
                transform.rotation = Quaternion.Euler(_pitch, yaw, 0f);
            else
            {
                // คง pitch เดิมไว้ (เช่นคุม pitch ที่ PlayerControllerTest)
                var e = transform.eulerAngles;
                transform.rotation = Quaternion.Euler(e.x, yaw, 0f);
            }
        }

        // --- 4) FOV kick ตอนสปรินต์ ---
        if (cam)
        {
            float targetFov = (controller && controller.IsSprinting) ? sprintFov : baseFov;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 1f - Mathf.Exp(-fovLerp * Time.deltaTime));
        }
    }
}
