using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class PatientSpawnTrigger : MonoBehaviour
{
    [Header("Trigger")]
    public string playerTag = "Player";
    public bool oneShot = true;

    [Header("Spawn")]
    public GameObject patientPrefab;
    public Transform spawnPoint;
    public Transform exitPoint;
    public float patientMoveSpeed = 1.8f;

    [Header("Audio & Sanity")]
    public AudioClip spawnSfx;
    [Range(0, 1)] public float sfxVolume = 1f;
    public float addSanity = 10f;
    public Vector2 waitBeforeWalk = new Vector2(1f, 2f);

    [Header("Cleanup")]
    public float destroyDelayAfterArrive = 0.2f;
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
        // 1) Spawn ผู้ป่วย
        if (!patientPrefab || !spawnPoint) yield break;
        GameObject patient = Instantiate(patientPrefab, spawnPoint.position, spawnPoint.rotation);

        // 2) กล้อง "ตามผู้ป่วย" + ล็อกคอนโทรลผู้เล่น
        var pc = player.GetComponent<PlayerController3D>();
        if (pc) pc.StartLookFollow(patient.transform, 8f, true);

        // 3) เล่นเสียง + เพิ่ม Sanity
        if (spawnSfx) { _audio.transform.position = patient.transform.position; _audio.PlayOneShot(spawnSfx, sfxVolume); }
        TryAddSanity(player.gameObject, addSanity);

        // 4) รอสัก 1–2 วิ ก่อนสั่งเดิน
        float wait = Random.Range(waitBeforeWalk.x, waitBeforeWalk.y);
        yield return new WaitForSeconds(wait);

        // 5) สั่งเดินไปยังกำหนด แล้วหายไป
        if (exitPoint) yield return MovePatientTo(patient, exitPoint.position, patientMoveSpeed);
        yield return new WaitForSeconds(destroyDelayAfterArrive);
        if (patient) Destroy(patient);

        // 6) คืนกล้อง/คอนโทรลผู้เล่น
        if (pc) pc.StopLookFollow(true);
    }

    IEnumerator MovePatientTo(GameObject patient, Vector3 targetPos, float speed)
    {
        if (!patient) yield break;

        var agent = patient.GetComponent<NavMeshAgent>();
        if (agent)
        {
            agent.isStopped = false;
            agent.stoppingDistance = 0.05f;
            agent.SetDestination(targetPos);
            while (patient && agent && agent.pathPending) yield return null;
            while (patient && agent && agent.remainingDistance > Mathf.Max(agent.stoppingDistance, 0.05f)) yield return null;
            yield break;
        }

        // ไม่มี NavMeshAgent: เดินธรรมดา
        var tr = patient.transform;
        float minStop = 0.05f;
        while (patient && (tr.position - targetPos).sqrMagnitude > minStop * minStop)
        {
            Vector3 dir = targetPos - tr.position;
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

    void TryAddSanity(GameObject playerGO, float amount)
    {
        if (amount == 0f) return;
        var pc3d = playerGO.GetComponent<PlayerController3D>();
        if (pc3d) { pc3d.AddSanity(amount); return; }
    }
}

