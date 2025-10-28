using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class CommandLoadScene : MonoBehaviour
{
    [Header("Command")]
    [Tooltip("คำที่ต้องพิมพ์ให้ครบเพื่อโหลดซีนปัจจุบัน")]
    public string commandWord = "SCENE";
    [Tooltip("เวลาสูงสุดระหว่างตัวอักษรแต่ละตัว (วินาที)")]
    public float inputTimeout = 1.2f;

    [Header("Debug")]
    public bool debugLogs = false;

    private Key[] sequence;
    private int index = 0;
    private float lastInputTime = -999f;

    void Awake() => BuildSequence();
    void OnValidate() => BuildSequence();

    void Update()
    {
        if (Keyboard.current == null || sequence == null || sequence.Length == 0) return;

        // หมดเวลาเว้นวรรค -> reset
        if (index > 0 && Time.unscaledTime - lastInputTime > inputTimeout)
            index = 0;

        Key expected = sequence[index];

        // ถ้ากด key ที่ "คาดหวัง"
        if (Keyboard.current[expected].wasPressedThisFrame)
        {
            StepForward();
            return;
        }

        // ถ้ากดตัวอักษร A..Z อื่นในเฟรมนี้ -> ตรวจว่าตรงตัวแรกของคำไหม
        Key first = sequence[0];
        for (Key k = Key.A; k <= Key.Z; k++)
        {
            if (k == expected && Keyboard.current[k].wasPressedThisFrame) { StepForward(); return; }
            if (k != expected && Keyboard.current[k].wasPressedThisFrame)
            {
                if (k == first) { index = 1; lastInputTime = Time.unscaledTime; }
                else { index = 0; }
                if (debugLogs) Debug.Log($"[CommandLoadScene] wrong key {k} -> index={index}");
                return;
            }
        }
    }

    void StepForward()
    {
        index++;
        lastInputTime = Time.unscaledTime;
        if (debugLogs) Debug.Log($"[CommandLoadScene] progress {index}/{sequence.Length}");

        if (index >= sequence.Length)
        {
            index = 0;
            ReloadCurrentScene();
        }
    }

    public void ReloadCurrentScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (debugLogs) Debug.Log($"[CommandLoadScene] Reload: {scene.name} ({scene.buildIndex})");
        SceneManager.LoadScene(scene.buildIndex);
    }

    void BuildSequence()
    {
        if (string.IsNullOrWhiteSpace(commandWord)) { sequence = new Key[0]; return; }
        commandWord = commandWord.Trim();
        sequence = new Key[commandWord.Length];
        for (int i = 0; i < commandWord.Length; i++)
        {
            char c = char.ToUpperInvariant(commandWord[i]);
            sequence[i] = (c >= 'A' && c <= 'Z') ? Key.A + (c - 'A') : Key.None;
        }
    }
}
