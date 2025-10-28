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

    private Key[] sequence;      // ลำดับปุ่มตามคำ
    private int index = 0;       // ความคืบหน้า
    private float lastInputTime; // เวลาที่กดล่าสุด
    private bool isActive = false;

    void Awake()
    {
        BuildSequence();
        lastInputTime = -999f;
        if (targetLight) isActive = targetLight.activeSelf;
    }

    void OnValidate() => BuildSequence();

    void Update()
    {
        if (Keyboard.current == null || sequence == null || sequence.Length == 0)
            return;

        // หมดเวลาเว้นวรรค → reset
        if (index > 0 && Time.unscaledTime - lastInputTime > inputTimeout)
            index = 0;

        // คีย์ที่คาดหวัง
        Key expected = sequence[index];

        // ถ้ากดตัวที่ "คาดหวัง" -> เดินหน้า
        if (Keyboard.current[expected].wasPressedThisFrame)
        {
            StepForward();
            return;
        }

        // ถ้ากดตัวอื่นใดใน A..Z ในเฟรมนี้ -> ตรวจว่าเป็นตัวเริ่มคำหรือไม่
        Key first = sequence[0];
        for (Key k = Key.A; k <= Key.Z; k++)
        {
            if (k == expected && Keyboard.current[k].wasPressedThisFrame) { StepForward(); return; }
            if (k != expected && Keyboard.current[k].wasPressedThisFrame)
            {
                if (k == first) { index = 1; lastInputTime = Time.unscaledTime; }
                else { index = 0; }
                if (debugLogs) Debug.Log($"[CommandLight] wrong key {k} -> index={index}");
                return;
            }
        }
    }

    void StepForward()
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

    void ToggleLight()
    {
        isActive = !isActive;
        if (targetLight) targetLight.SetActive(isActive);
        if (debugLogs) Debug.Log($"[CommandLight] TOGGLE => {(isActive ? "ON" : "OFF")}");
    }

    void BuildSequence()
    {
        if (string.IsNullOrEmpty(commandWord)) { sequence = new Key[0]; return; }
        commandWord = commandWord.Trim();
        sequence = new Key[commandWord.Length];
        for (int i = 0; i < commandWord.Length; i++)
            sequence[i] = CharToKey(commandWord[i]);
    }

    static Key CharToKey(char c)
    {
        c = char.ToUpperInvariant(c);
        if (c >= 'A' && c <= 'Z') return Key.A + (c - 'A'); // A..Z ต่อเนื่องใน enum
        // เผื่ออักขระนอกช่วง: map เป็น Key.None (จะกดไม่ได้)
        return Key.None;
    }
}
