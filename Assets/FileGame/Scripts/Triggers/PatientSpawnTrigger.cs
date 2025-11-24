using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
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

    private AudioSource _audio;
    private bool _fired = false;

    void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;

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

        Transform playerRoot = other.transform;
        if (!playerRoot) return;

        _fired = true;
        if (disableTriggerAfterRun) GetComponent<Collider>().enabled = false;

        StartCoroutine(RunSequence(playerRoot));
    }

    private IEnumerator RunSequence(Transform player)
    {
        // 1) Spawn Patient
        if (!patientPrefab || !spawnPoint) yield break;
        GameObject patient = Instantiate(patientPrefab, spawnPoint.position, spawnPoint.rotation);

        // 2) กล้องมองผู้ป่วย + ล็อกคอนโทรล
        var pc = player.GetComponent<PlayerController3D>();
        if (pc) pc.StartLookFollow(patient.transform, 8f, lockControl: true);

        // 3) เล่นเสียง + เพิ่ม sanity
        if (spawnSfx && _audio)
        {
            _audio.transform.position = patient.transform.position;
            _audio.PlayOneShot(spawnSfx, sfxVolume);
        }
        if (addSanity != 0f && pc) pc.AddSanity(addSanity);

        AmbientRoomAudioManager.FocusDuck();

        // 4) รอสักครู่
        yield return new WaitForSeconds(Random.Range(waitBeforeWalk.x, waitBeforeWalk.y));

        // 5) สั่งเดินไป exitPoint
        if (exitPoint) yield return MovePatientTo(patient, exitPoint.position, patientMoveSpeed);

        // 6) ลบ patient หลังถึงปลายทาง
        yield return new WaitForSeconds(destroyDelayAfterArrive);
        if (patient) Destroy(patient);

        // 7) คืน control
        if (pc) pc.StopLookFollow(unlockControl: true);
    }

    private IEnumerator MovePatientTo(GameObject patient, Vector3 targetPos, float speed)
    {
        if (!patient) yield break;

        var agent = patient.GetComponent<NavMeshAgent>();
        if (agent)
        {
            agent.isStopped = false;
            agent.stoppingDistance = 0.05f;
            agent.SetDestination(targetPos);

            while (patient && agent && (agent.pathPending || agent.remainingDistance > Mathf.Max(agent.stoppingDistance, 0.05f)))
                yield return null;

            yield break;
        }

        // Move manually if no NavMeshAgent
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
}
