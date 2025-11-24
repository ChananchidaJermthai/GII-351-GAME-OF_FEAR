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

    // Internal
    private Key[] sequence;
    private int index = 0;
    private float lastInputTime = -999f;

    // Static cache สำหรับ char -> Key
    private static readonly Key[] keyMap = new Key[26];
    static CommandLoadScene()
    {
        for (int i = 0; i < 26; i++)
            keyMap[i] = Key.A + i;
    }

    void Awake()
    {
        BuildSequence();
    }

    void OnValidate()
    {
        if (!Application.isPlaying)
            BuildSequence();
    }

    void Update()
    {
        if (Keyboard.current == null || sequence == null || sequence.Length == 0)
            return;

        // หมดเวลาเว้นวรรค -> reset
        if (index > 0 && Time.unscaledTime - lastInputTime > inputTimeout)
        {
            if (debugLogs) Debug.Log($"[CommandLoadScene] timeout -> reset index");
            index = 0;
        }

        Key expected = sequence[index];

        // กด key ที่คาดหวัง
        if (Keyboard.current[expected].wasPressedThisFrame)
        {
            StepForward();
            return;
        }

        // ตรวจสอบตัวอักษร A-Z อื่น ๆ
        Key first = sequence[0];
        for (Key k = Key.A; k <= Key.Z; k++)
        {
            if (Keyboard.current[k].wasPressedThisFrame)
            {
                if (k == expected)
                {
                    StepForward();
                }
                else if (k == first)
                {
                    index = 1;
                    lastInputTime = Time.unscaledTime;
                    if (debugLogs) Debug.Log($"[CommandLoadScene] start new sequence with {k}");
                }
                else
                {
                    index = 0;
                    if (debugLogs) Debug.Log($"[CommandLoadScene] wrong key {k} -> reset index");
                }
                return;
            }
        }
    }

    private void StepForward()
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

    private void BuildSequence()
    {
        if (string.IsNullOrWhiteSpace(commandWord))
        {
            sequence = new Key[0];
            return;
        }

        commandWord = commandWord.Trim();
        sequence = new Key[commandWord.Length];

        for (int i = 0; i < commandWord.Length; i++)
        {
            char c = char.ToUpperInvariant(commandWord[i]);
            sequence[i] = (c >= 'A' && c <= 'Z') ? keyMap[c - 'A'] : Key.None;
        }
    }
}
