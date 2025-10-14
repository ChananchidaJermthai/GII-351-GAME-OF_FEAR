using UnityEngine;
using UnityEngine.InputSystem; // แค่เพื่ออ่าน mouse delta ทำเอฟเฟกต์ sway (ไม่บังคับ)

/// ติดที่ไฟฉาย ให้ตาม Target (เช่น CameraHolder) แบบหน่วง/สปริง + ส่ายเล็กน้อยตามการลากเมาส์
public class FlashlightLag : MonoBehaviour
{
    [Header("Follow Target")]
    public Transform target;                 // ใส่ CameraHolder
    public Vector3 localOffset = new Vector3(0f, -0.02f, 0.02f); // ระยะห่างจากจุด target (ทดสอบปรับได้)

    [Header("Smoothing")]
    [Tooltip("เวลาหน่วงตำแหน่ง (วินาที) ยิ่งน้อยยิ่งไว")]
    public float positionSmoothTime = 0.06f;
    [Tooltip("เวลาหน่วงการหมุน (วินาที) ยิ่งน้อยยิ่งไว")]
    public float rotationSmoothTime = 0.08f;

    [Header("Sway (เอฟเฟกต์ไฟแกว่งตามเมาส์)")]
    public bool enableSway = true;
    [Tooltip("แรงส่ายองศาต่อความเร็วเมาส์")]
    public float swayAnglePerMouse = 0.02f;      // องศา/พิกเซลเมาส์
    public float swayMaxAngle = 6f;               // ไม่ให้ส่ายเกินกี่องศา
    public float swayReturnSharpness = 6f;        // ค่ากลับสู่ศูนย์ (spring)

    private Vector3 posVel;                       // สำหรับ SmoothDamp position
    private float velPitch, velYaw;               // สำหรับ SmoothDampAngle rotation
    private Vector2 swayCurrent;                  // pitch(x-ขึ้นลง), yaw(y-ซ้ายขวา)

    void LateUpdate()
    {
        if (!target) return;

        // 1) ตำแหน่ง: ไล่ตามแบบ SmoothDamp
        Vector3 desiredPos = target.TransformPoint(localOffset);
        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref posVel, positionSmoothTime);

        // 2) การหมุนหลัก: ไล่ตาม yaw/pitch ของ target แบบหน่วงองศา
        Vector3 cur = transform.rotation.eulerAngles;
        Vector3 tgt = target.rotation.eulerAngles;

        float yaw = Mathf.SmoothDampAngle(cur.y, tgt.y, ref velYaw, rotationSmoothTime);
        float pitch = Mathf.SmoothDampAngle(cur.x, tgt.x, ref velPitch, rotationSmoothTime);

        // 3) อ่านเมาส์เพื่อใส่ "sway" (ไฟแกว่งเล็กน้อย) – ใช้ New Input System
        if (enableSway)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();   // พิกเซลต่อเฟรม
                // เพิ่มออฟเซ็ต (กลับด้านนิดหน่อยให้ดูแกว่ง)
                swayCurrent.x = Mathf.Clamp(swayCurrent.x - delta.y * swayAnglePerMouse, -swayMaxAngle, swayMaxAngle); // pitch
                swayCurrent.y = Mathf.Clamp(swayCurrent.y + delta.x * swayAnglePerMouse, -swayMaxAngle, swayMaxAngle); // yaw
            }
            // ให้ส่ายค่อย ๆ กลับศูนย์ (spring)
            swayCurrent = Vector2.Lerp(swayCurrent, Vector2.zero, 1f - Mathf.Exp(-swayReturnSharpness * Time.deltaTime));
        }
        else
        {
            swayCurrent = Vector2.zero;
        }

        // 4) ประกอบมุมรวม (ตามกล้อง + sway)
        float finalPitch = pitch + swayCurrent.x;
        float finalYaw = yaw + swayCurrent.y;

        transform.rotation = Quaternion.Euler(finalPitch, finalYaw, 0f);
    }
}
