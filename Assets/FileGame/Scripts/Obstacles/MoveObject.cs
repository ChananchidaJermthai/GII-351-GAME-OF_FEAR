using UnityEngine;
using System.Collections;

public class MoveObject : MonoBehaviour
{
    
    [Header("Object to Move")]
    public Transform targetObject;

    [Header("Exact Target Position (World Space)")]
    public Vector3 targetPosition;

    [Header("Movement Settings")]
    public float moveSpeed = 3f;
    public float waitTime = 2f;

    [Header("Trigger Settings")]
    public bool startOnTrigger = true;
    public string playerTag = "Player";

    [Header("Sound Settings (Optional)")]
    public AudioClip moveSound;      // เสียงตอนเริ่มขยับ
    [Range(0f,1f)]
    public float volume = 1f;

    [Header("Camera Shake Settings")]
    public bool enableCameraShake = true;
    public float shakeDuration = 0.5f;   // เวลาสั่น
    public float shakeMagnitude = 0.3f;  // ความแรงของสั่น

    private Vector3 originalPos;
    private bool hasStarted = false;


    public float sanityAmount = 8.0f;
   

    private void Start()
    {
        if (targetObject == null)
        {
            Debug.LogWarning("⚠ โปรดกำหนด targetObject");
            return;
        }

        originalPos = targetObject.position;

        if (!startOnTrigger)
            StartMovement();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!startOnTrigger) return;
        if (hasStarted) return;
        if (!other.CompareTag(playerTag)) return;

        StartMovement();

        PlayerController3D playerController = other.GetComponent<PlayerController3D>();
        if (playerController != null)
        {
            playerController.AddSanity(sanityAmount);
        }
    }

    private void StartMovement()
    {
        hasStarted = true;

        // เล่นเสียงชัด ๆ ในหูผู้เล่น
        if (moveSound != null)
        {
            PlaySoundInPlayerEar(moveSound, volume);
        }

        // เริ่มสั่นกล้องพร้อมเสียง
        if (enableCameraShake)
        {
            StartCoroutine(ShakeCamera(shakeDuration, shakeMagnitude));
        }

        StartCoroutine(MoveRoutine());
    }

    void PlaySoundInPlayerEar(AudioClip clip, float vol)
    {
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            AudioSource audioSource = mainCam.gameObject.AddComponent<AudioSource>();
            audioSource.clip = clip;
            audioSource.volume = vol;
            audioSource.spatialBlend = 0f; // 2D
            audioSource.Play();
            Destroy(audioSource, clip.length);

            
        }
        else
        {
            Debug.LogWarning("❌ ไม่มี Main Camera ใน Scene");
        }
    }

    IEnumerator ShakeCamera(float duration, float magnitude)
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) yield break;

        Vector3 originalCamPos = mainCam.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            mainCam.transform.localPosition = originalCamPos + new Vector3(x, y, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }

        mainCam.transform.localPosition = originalCamPos;
    }

    IEnumerator MoveRoutine()
    {
        yield return MoveTo(targetPosition);
        yield return new WaitForSeconds(waitTime);
        yield return MoveTo(originalPos);

        Destroy(targetObject.gameObject);
        Destroy(gameObject);
    }

    IEnumerator MoveTo(Vector3 destination)
    {
        while (Vector3.Distance(targetObject.position, destination) > 0.01f)
        {
            targetObject.position = Vector3.MoveTowards(
                targetObject.position,
                destination,
                moveSpeed * Time.deltaTime
            );
            yield return null;
        }
    }
}
