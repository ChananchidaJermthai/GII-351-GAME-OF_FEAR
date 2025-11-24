using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class CommandLight : MonoBehaviour
{
    [Header("Target to Toggle")]
    public GameObject targetLight;

    [Header("Settings")]
    [Tooltip("คำสั่งที่ต้องพิมพ์ (ตัวพิมพ์ใหญ่/เล็กได้เท่ากัน)")]
    public string commandWord = "LIGHT";
    [Tooltip("เวลาสูงสุดระหว่างการกดตัวอักษรแต่ละตัว (วินาที)")]
    public float inputTimeout = 1.2f;
    public bool debugLogs = false;

    // Internal
    private Key[] sequence;          // ลำดับปุ่มตามคำ
    private int index = 0;           // ความคืบหน้า
    private float lastInputTime;     // เวลาที่กดล่าสุด
    private bool isActive = false;

    // Static cache สำหรับแปลง char → Key
    private static readonly Key[] keyMap = new Key[26];
    static CommandLight()
    {
        for (int i = 0; i < 26; i++)
            keyMap[i] = Key.A + i;
    }

    void Awake()
    {
        BuildSequence();              // สร้าง sequence ครั้งเดียว
        lastInputTime = -999f;
        if (targetLight) isActive = targetLight.activeSelf;
    }

    void OnValidate()
    {
        // Build sequence เฉพาะใน editor เมื่อเปลี่ยนค่า
        if (Application.isPlaying == false)
            BuildSequence();
    }

    void Update()
    {
        if (Keyboard.current == null || sequence == null || sequence.Length == 0)
            return;

        // หมดเวลาเว้นวรรค → reset
        if (index > 0 && Time.unscaledTime - lastInputTime > inputTimeout)
        {
            if (debugLogs) Debug.Log($"[CommandLight] timeout -> reset index");
            index = 0;
        }

        Key expected = sequence[index];

        // กดปุ่มที่คาดหวัง
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
                    if (debugLogs) Debug.Log($"[CommandLight] start new sequence with {k}");
                }
                else
                {
                    index = 0;
                    if (debugLogs) Debug.Log($"[CommandLight] wrong key {k} -> reset index");
                }
                return;
            }
        }
    }

    private void StepForward()
    {
        index++;
        lastInputTime = Time.unscaledTime;

        if (debugLogs) Debug.Log($"[CommandLight] progress {index}/{sequence.Length}");

        if (index >= sequence.Length)
        {
            index = 0;
            ToggleLight();
        }
    }

    private void ToggleLight()
    {
        isActive = !isActive;
        if (targetLight) targetLight.SetActive(isActive);
        if (debugLogs) Debug.Log($"[CommandLight] TOGGLE => {(isActive ? "ON" : "OFF")}");
    }

    private void BuildSequence()
    {
        if (string.IsNullOrEmpty(commandWord))
        {
            sequence = new Key[0];
            return;
        }

        commandWord = commandWord.Trim();
        sequence = new Key[commandWord.Length];

        for (int i = 0; i < commandWord.Length; i++)
            sequence[i] = CharToKey(commandWord[i]);
    }

    private static Key CharToKey(char c)
    {
        c = char.ToUpperInvariant(c);
        if (c >= 'A' && c <= 'Z')
            return keyMap[c - 'A'];
        return Key.None;
    }
}
