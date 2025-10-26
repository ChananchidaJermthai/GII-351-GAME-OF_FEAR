using System.Collections;
using UnityEngine;
#if UNITY_AI_NAVIGATION || UNITY_2019_1_OR_NEWER
using UnityEngine.AI;
#endif

[DisallowMultipleComponent]
public class PatientSpawnTrigger : MonoBehaviour
{
    [Header("Trigger")]
    public string playerTag = "Player";
    public bool oneShot = true;            // ให้เกิดครั้งเดียว

    [Header("Spawn")]
    public GameObject patientPrefab;       // พรีแฟบคนไข้
    public Transform spawnPoint;           // จุดเกิด
    public Transform exitPoint;            // จุดที่คนไข้จะเดินไปก่อนหาย
    public float patientMoveSpeed = 1.8f;  // ใช้เมื่อไม่มี NavMeshAgent

    [Header("Look & Lock Player")]
    public float faceDuration = 0.35f;     // เวลาหันตัวผู้เล่นไปหาคนไข้
    public bool lockPlayerInput = true;    // ปิด PlayerInput ชั่วคราว
    public bool zeroVelocityOnLock = true; // กันผู้เล่นไหล

    [Header("Audio & Sanity")]
    public AudioClip spawnSfx;             // เสียงเล่น 1 ครั้งตอนเจอ
    [Range(0, 1)] public float sfxVolume = 1f;
    public float addSanity = 10f;          // ค่าที่จะบวกให้ผู้เล่น
    public Vector2 waitBeforeWalk = new Vector2(1f, 2f); // รอ 1–2 วิ ก่อนเริ่มเดิน

    [Header("Cleanup")]
    public float destroyDelayAfterArrive = 0.2f; // เวลาหน่วงก่อนลบคนไข้
    public bool disableTriggerAfterRun = true;

    AudioSource _audio; bool _fired = false;

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
        if (!_audio)
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.loop = false;
            _audio.spatialBlend = 1f;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_fired && oneShot) return;
        if (!other.CompareTag(playerTag)) return;

        var player = other.GetComponentInParent<Transform>();
        if (!player) return;

        StartCoroutine(RunSequence(player));
        _fired = true;
        if (disableTriggerAfterRun) GetComponent<Collider>().enabled = false;
    }

    IEnumerator RunSequence(Transform player)
    {
        // 1) Spawn patient
        if (!patientPrefab || !spawnPoint)
        {
            Debug.LogWarning("[PatientSpawnTrigger] Missing patientPrefab or spawnPoint.");
            yield break;
        }
        GameObject patient = Instantiate(patientPrefab, spawnPoint.position, spawnPoint.rotation);

        // 2) Lock player input (optional)
        MonoBehaviour playerInput = null;
        CharacterController cc = null;

        if (lockPlayerInput)
        {
            // รองรับ PlayerInput (New Input System) หรือสคริปต์ควบคุมอื่นที่เปิด/ปิดได้
            playerInput = player.GetComponent<MonoBehaviour>(); // fallback
            var pi = player.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (pi) { playerInput = pi; pi.enabled = false; }

            // กันไหล
            cc = player.GetComponent<CharacterController>();
        }
        if (zeroVelocityOnLock && cc != null)
        {
            // เนียน ๆ: ดันเคลื่อนที่เล็กน้อยเพื่อตัดโมเมนตัม
            cc.Move(Vector3.zero);
        }

        // 3) เล่นเสียงหนึ่งครั้ง
        if (spawnSfx)
        {
            if (_audio)
            {
                _audio.transform.position = spawnPoint.position;
                _audio.PlayOneShot(spawnSfx, sfxVolume);
            }
            else
            {
                AudioSource.PlayClipAtPoint(spawnSfx, spawnPoint.position, sfxVolume);
            }
        }

        // 4) หันผู้เล่นไปหาคนไข้ (หมุนตามแนวนอน + ยกกล้องขึ้นลงตามแนวดิ่งถ้ามี holder)
        yield return FaceTargetSmooth(player, patient.transform.position, faceDuration);

        // 5) เพิ่ม Sanity ให้ผู้เล่น
        TryAddSanity(player.gameObject, addSanity);

        // 6) รอ 1–2 วิ (สุ่มในช่วงที่กำหนด)
        float wait = Random.Range(waitBeforeWalk.x, waitBeforeWalk.y);
        yield return new WaitForSeconds(wait);

        // 7) ให้คนไข้เดินไป exitPoint แล้วหายไป
        if (exitPoint)
            yield return MovePatientTo(patient, exitPoint.position, patientMoveSpeed);

        yield return new WaitForSeconds(destroyDelayAfterArrive);
        if (patient) Destroy(patient);

        // 8) ปลดล็อกผู้เล่น
        if (playerInput) playerInput.enabled = true;
    }

    IEnumerator FaceTargetSmooth(Transform player, Vector3 targetWorldPos, float dur)
    {
        // หมุน yaw ของตัวผู้เล่นไปทางเป้าหมาย
        Vector3 flatToTarget = targetWorldPos - player.position;
        flatToTarget.y = 0f;
        if (flatToTarget.sqrMagnitude < 0.0001f) yield break;

        Quaternion start = player.rotation;
        Quaternion goal = Quaternion.LookRotation(flatToTarget.normalized, Vector3.up);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, dur);
            player.rotation = Quaternion.Slerp(start, goal, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        // ถ้ามีกล้องลูก (cameraHolder) แนะนำให้วางไว้ในฮีรารคีของ player เพื่อเงย/ก้มตาม pivot ด้วย
    }

    IEnumerator MovePatientTo(GameObject patient, Vector3 targetPos, float speed)
    {
        if (!patient) yield break;

        // ถ้ามี NavMeshAgent ให้ใช้ก่อน
#if UNITY_AI_NAVIGATION || UNITY_2019_1_OR_NEWER
        var agent = patient.GetComponent<NavMeshAgent>();
        if (agent)
        {
            agent.isStopped = false;
            agent.stoppingDistance = 0.05f;
            agent.SetDestination(targetPos);

            // รอจนถึง
            while (patient && agent && agent.pathPending) yield return null;
            while (patient && agent && agent.remainingDistance > Mathf.Max(agent.stoppingDistance, 0.05f))
                yield return null;

            yield break;
        }
#endif
        // ถ้าไม่มี NavMeshAgent: เดินแบบธรรมดา
        var tr = patient.transform;
        float minStop = 0.05f;
        while (patient && (tr.position - targetPos).sqrMagnitude > minStop * minStop)
        {
            Vector3 dir = (targetPos - tr.position);
            dir.y = 0f;
            float d = dir.magnitude;
            if (d > 0.001f)
            {
                dir /= d;
                tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(dir, Vector3.up), Time.deltaTime * 6f);
                tr.position += dir * speed * Time.deltaTime;
            }
            yield return null;
        }
    }

    // -------- Sanity helpers ----------
    void TryAddSanity(GameObject playerGO, float amount)
    {
        if (amount == 0f) return;

        // 1) ถ้า PlayerController3D มีเมธอด AddSanity(float) ให้เรียก
        var pc3d = playerGO.GetComponent<PlayerController3D>();
        if (pc3d)
        {
            // มีเมธอดแบบ public ไหม?
            var m = typeof(PlayerController3D).GetMethod("AddSanity", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (m != null)
            {
                m.Invoke(pc3d, new object[] { amount });
                return;
            }
            // ถ้าไม่มี ให้ลองเมธอด TryAddSanity หรืออื่น ๆ
            m = typeof(PlayerController3D).GetMethod("TryAddSanity", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (m != null)
            {
                m.Invoke(pc3d, new object[] { amount });
                return;
            }
        }

        // 2) ถ้ามีอะแดปเตอร์/บริดจ์ที่ทำ ISanity
        var sanityIface = playerGO.GetComponent<ISanityReceiver>();
        if (sanityIface != null) { sanityIface.AddSanity(amount); return; }

        // 3) หา Inventory/Controller อื่น ๆ ตามโปรเจ็กต์ของคุณก็ได้
        Debug.Log("[PatientSpawnTrigger] Could not add sanity. Consider adding a public AddSanity(float) to your PlayerController3D or attach ISanityReceiver.");
    }

    // เผื่อคุณอยากทำตัวกลางง่าย ๆ: แปะคอมโพเนนต์นี้บน Player แล้ว map ไปที่ PlayerController3D เอง
    public interface ISanityReceiver { void AddSanity(float amount); }
}
