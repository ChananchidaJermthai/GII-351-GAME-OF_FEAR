using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class HorrorTrigger_ShadowDash : MonoBehaviour
{
    [Header("Trigger Settings")]
    public string playerTag = "Player";
    public bool singleUse = true;
    public float startDelay = 0.1f;

    [Header("Dash Path")]
    public Transform pointA;     // จุดเริ่ม (สุดทางด้านซ้าย/ขวา)
    public Transform pointB;     // จุดจบ (อีกฝั่ง)
    public float dashDuration = 0.6f;   // เร็วหน่อยจะดู “พุ่งผ่าน”

    [Header("Actor")]
    public GameObject shadowPrefab;     // ใช้โมเดล/เงาดำ
    public bool faceMoveDirection = true;
    public float destroyAfter = 0.2f;   // หลังถึง B ให้หายไป

    [Header("SFX (optional)")]
    public AudioClip whooshSfx;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    bool _used;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_used && singleUse) return;
        if (!other.CompareTag(playerTag)) return;
        if (!shadowPrefab || !pointA || !pointB) return;

        StartCoroutine(DashRoutine());
        if (singleUse) _used = true;
    }

    IEnumerator DashRoutine()
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        var actor = Instantiate(shadowPrefab, pointA.position, Quaternion.identity);

        if (whooshSfx)
            AudioSource.PlayClipAtPoint(whooshSfx, pointA.position, sfxVolume);

        float t = 0f;
        while (t < dashDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dashDuration);

            Vector3 pos = Vector3.Lerp(pointA.position, pointB.position, k);
            actor.transform.position = pos;

            if (faceMoveDirection)
            {
                Vector3 dir = (pointB.position - pointA.position);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                    actor.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }

            yield return null;
        }

        if (destroyAfter > 0f)
            Destroy(actor, destroyAfter);
    }
}
