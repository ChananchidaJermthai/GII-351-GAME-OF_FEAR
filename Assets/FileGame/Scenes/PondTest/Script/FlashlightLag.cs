using UnityEngine;
using UnityEngine.InputSystem;

public class FlashlightLag : MonoBehaviour
{
    [Header("Follow Target")]
    public Transform target;                       // ใส่ CameraHolder หรือ Main Camera

    [Tooltip("ตามแบบเวิลด์ (เหมาะเมื่อไฟฉายไม่เป็นลูกของกล้อง). ถ้าไฟฉายเป็นลูกกล้อง ให้ปิด")]
    public bool followInWorld = true;

    [Header("Position Follow")]
    [Tooltip("หน่วงตำแหน่งด้วยหรือไม่ (ถ้าปิด จะ 'ไม่ขยับตำแหน่ง' เลย คงไว้ตามที่คุณวางไว้ข้างกล้อง)")]
    public bool followPosition = true;

    [Tooltip("ใช้ตำแหน่งเริ่มต้นของไฟฉาย (เทียบกับ target) เป็นออฟเซ็ตอัตโนมัติ")]
    public bool useInitialOffset = true;

    [Tooltip("ออฟเซ็ตเพิ่มเติมจากออฟเซ็ตเริ่มต้น (หน่วย local ของ target)")]
    public Vector3 extraLocalOffset = Vector3.zero;

    [Tooltip("เวลาในการหน่วงตำแหน่ง (วินาที)")]
    public float positionSmoothTime = 0.06f;

    [Header("Rotation Follow")]
    [Tooltip("เวลาในการหน่วงการหมุน (วินาที)")]
    public float rotationSmoothTime = 0.08f;

    [Header("Orientation Fix")]
    [Tooltip("แก้ทิศโมเดลไฟฉายให้ตรงกับกล้อง (เช่น โมเดลหันผิดแกน)")]
    public Vector3 aimAxisCorrectionEuler = Vector3.zero; // ลอง (0,90,0) หรือ (90,0,0) ถ้าชี้ผิด
    private Quaternion aimAxisCorrection;

    [Header("Sway (optional)")]
    public bool enableSway = true;
    public float swayAnglePerMouse = 0.02f;   // องศาต่อพิกเซลเมาส์
    public float swayMaxAngle = 6f;
    public float swayReturnSharpness = 6f;

    // --- internals ---
    private Vector3 capturedLocalOffset;      // ออฟเซ็ตที่ "จับไว้" ตอนเริ่ม จากตำแหน่งที่คุณวางจริง
    private Quaternion rotOffsetWorld;        // สำหรับ followInWorld=true
    private Quaternion rotOffsetLocal;        // สำหรับ followInWorld=false (child)
    private Vector3 posVel;
    private Vector2 swayCurrent;

    void Awake()
    {
        if (!target)
        {
            Debug.LogWarning("[FlashlightLag_v3] Missing target.");
            enabled = false;
            return;
        }

        aimAxisCorrection = Quaternion.Euler(aimAxisCorrectionEuler);

        // 1) จับออฟเซ็ตตำแหน่ง "จากที่คุณวางไว้จริง"
        // ไม่ว่าไฟฉายจะเป็นลูกหรือไม่ เราแปลงตำแหน่งตอนเริ่มให้ไปอยู่ใน local space ของ target
        capturedLocalOffset = target.InverseTransformPoint(transform.position);

        // 2) จับออฟเซ็ตการหมุนตอนเริ่ม เพื่อคงทิศโมเดลไว้
        if (followInWorld)
        {
            // world: rotFlash ≈ targetRot * aimCorrection * rotOffsetWorld
            rotOffsetWorld = Quaternion.Inverse(target.rotation) * transform.rotation * Quaternion.Inverse(aimAxisCorrection);
        }
        else
        {
            // child/local: rotFlash(local) ≈ targetLocalRot * aimCorrection * rotOffsetLocal
            rotOffsetLocal = Quaternion.Inverse(target.localRotation) * transform.localRotation * Quaternion.Inverse(aimAxisCorrection);
        }
    }

    void LateUpdate()
    {
        if (!target) return;

        // ---------- POSITION ----------
        if (followPosition)
        {
            // ใช้ "ออฟเซ็ตที่จับไว้" + extraLocalOffset -> จะรักษาตำแหน่งข้างกล้อง ไม่ดูดเข้ากลาง
            Vector3 desiredPos = target.TransformPoint(capturedLocalOffset + extraLocalOffset);
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref posVel, positionSmoothTime);
        }
        // else: ไม่แตะตำแหน่งเลย → จะคงไว้ตรงข้างกล้องแบบที่คุณวาง

        // ---------- ROTATION ----------
        // หมุนให้หันตามกล้อง + แกนแก้ + ออฟเซ็ตหมุนที่จับไว้
        Quaternion targetRot = followInWorld
            ? target.rotation * aimAxisCorrection * rotOffsetWorld
            : target.rotation * aimAxisCorrection * rotOffsetLocal; // ใช้ world rotation เพื่อความนิ่ง

        // เพิ่ม Sway (ตามแกนของกล้อง)
        if (enableSway)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                swayCurrent.x = Mathf.Clamp(swayCurrent.x - delta.y * swayAnglePerMouse, -swayMaxAngle, swayMaxAngle); // pitch
                swayCurrent.y = Mathf.Clamp(swayCurrent.y + delta.x * swayAnglePerMouse, -swayMaxAngle, swayMaxAngle); // yaw
            }
            swayCurrent = Vector2.Lerp(swayCurrent, Vector2.zero, 1f - Mathf.Exp(-swayReturnSharpness * Time.deltaTime));

            Quaternion swayRot =
                Quaternion.AngleAxis(swayCurrent.y, target.up) *
                Quaternion.AngleAxis(swayCurrent.x, target.right);

            targetRot = targetRot * swayRot;
        }

        // หน่วงการหมุนแบบ slerp ดีกว่า (นิ่ง)
        float t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, rotationSmoothTime));
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
    }
}
