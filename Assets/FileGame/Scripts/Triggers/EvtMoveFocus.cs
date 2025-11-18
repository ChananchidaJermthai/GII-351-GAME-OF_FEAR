using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class EvtMoveFocus : MonoBehaviour
{
    [Header("Trigger")]
    public bool oneShot = true;
    public string playerTag = "Player";
    bool hasTriggered = false;

    [Header("Player / Sanity")]
    public PlayerController3D player;
    public float sanityAddAmount = 10f;

    [Header("Spawn / Scene Target")]
    public bool useSpawnMode = true;          // true = spawn prefab, false = ใช้ GameObject ในฉาก
    public GameObject prefabToSpawn;
    public Transform spawnPoint;
    public GameObject sceneTarget;

    [Tooltip("ถ้า true จะใช้ prefab ที่ spawn ออกมาเป็นตัวเคลื่อนที่ (moveTarget) อัตโนมัติ")]
    public bool moveSpawnedObject = true;

    [Header("Move Target")]
    public Transform moveTarget;              // ใช้กับโหมดอ้างอิงของในฉาก
    public Transform pointFrom;
    public Transform pointTo;
    public float moveDuration = 1.5f;
    public AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public bool moveX = true;
    public bool moveY = true;
    public bool moveZ = true;

    [Header("Camera / Look")]
    public bool lockPlayerControl = true;
    public float lookRotateSeconds = 0.4f;
    public float lookHoldSeconds = 1.0f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip sfx;

    [Header("Cleanup")]
    [Tooltip("ลบตัวเป้าหมาย (เช่น คนไข้ที่ spawn) ออกจาก Scene หลังจบ sequence")]
    public bool destroyTargetAfterSequence = true;

    [Tooltip("ลบตัว Trigger นี้ออกจาก Scene ด้วยหลังจบ sequence")]
    public bool destroyTriggerAfterSequence = false;

    private void Reset()
    {
        if (!player) player = FindObjectOfType<PlayerController3D>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag))
            return;

        if (oneShot && hasTriggered)
            return;

        var pc = player ? player : other.GetComponentInParent<PlayerController3D>();
        if (!pc) return;

        hasTriggered = true;
        player = pc;

        GameObject focusObj = null;
        Transform moveTransform = moveTarget;

        // ---------- เลือก focusObj + moveTransform ----------
        if (useSpawnMode)
        {
            if (prefabToSpawn != null)
            {
                Transform sp = spawnPoint ? spawnPoint : transform;
                focusObj = Instantiate(prefabToSpawn, sp.position, sp.rotation);

                // ใช้ prefab ที่ spawn ออกมาเป็นตัวเคลื่อนที่
                if (moveSpawnedObject && focusObj != null)
                {
                    moveTransform = focusObj.transform;
                }
            }
        }
        else
        {
            focusObj = sceneTarget;

            // ถ้าไม่ได้ตั้ง moveTarget แยกไว้ แต่มี sceneTarget → ใช้มันเป็นตัวเคลื่อนที่ได้
            if (!moveTransform && sceneTarget != null)
            {
                moveTransform = sceneTarget.transform;
            }
        }

        // ---------- กล้องมอง ----------
        if (focusObj != null)
        {
            player.LookAtWorld(focusObj.transform.position, lookRotateSeconds, lookHoldSeconds);
        }

        // ---------- ล็อกคอนโทรล ----------
        if (lockPlayerControl)
            player.LockControl(true);

        // ---------- เสียง ----------
        if (sfx != null)
        {
            if (audioSource)
                audioSource.PlayOneShot(sfx);
            else
                AudioSource.PlayClipAtPoint(sfx, focusObj ? focusObj.transform.position : transform.position);
        }

        // ---------- เพิ่ม Sanity ----------
        if (sanityAddAmount != 0f)
        {
            player.AddSanity(sanityAddAmount);
        }

        // ---------- เริ่ม Sequence เคลื่อนที่ + เคลียร์ตอนจบ ----------
        StartCoroutine(Co_DoSequence(moveTransform, focusObj));
    }

    private IEnumerator Co_DoSequence(Transform moveTransform, GameObject focusObj)
    {
        // เคลื่อนถ้ามี target + pointTo
        if (moveTransform != null && pointTo != null && moveDuration > 0f)
        {
            Vector3 startPos = moveTransform.position;

            if (pointFrom != null)
                startPos = pointFrom.position;

            Vector3 endPos = pointTo.position;

            float t = 0f;
            while (t < moveDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / moveDuration);
                float eval = moveCurve != null ? moveCurve.Evaluate(k) : k;

                Vector3 p = moveTransform.position;

                if (moveX) p.x = Mathf.Lerp(startPos.x, endPos.x, eval);
                if (moveY) p.y = Mathf.Lerp(startPos.y, endPos.y, eval);
                if (moveZ) p.z = Mathf.Lerp(startPos.z, endPos.z, eval);

                moveTransform.position = p;
                yield return null;
            }
        }

        // รอให้กล้องถือมุมมองอีกนิด
        yield return new WaitForSeconds(lookHoldSeconds);

        // ปลดล็อก control กลับ
        if (lockPlayerControl && player != null)
        {
            player.LockControl(false);
        }

        // ---------- ลบออกจาก Scene ----------
        if (destroyTargetAfterSequence)
        {
            if (moveTransform != null)
            {
                Destroy(moveTransform.gameObject);
            }
            else if (focusObj != null)
            {
                Destroy(focusObj);
            }
        }

        if (destroyTriggerAfterSequence)
        {
            Destroy(gameObject);
        }
    }
}
