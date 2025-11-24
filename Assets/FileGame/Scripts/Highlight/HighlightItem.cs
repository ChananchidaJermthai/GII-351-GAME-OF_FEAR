using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class HighlightItem : MonoBehaviour
{
    [Header("Highlight Settings")]
    [Tooltip("Material ที่ใช้ตอน Highlight (เช่น Outline / Emission)")]
    public Material highlightMaterial;

    [Tooltip("ถ้าว่าง จะใช้ Renderer ทั้งหมดในลูกหลานอัตโนมัติ")]
    public Renderer[] targetRenderers;

    [Tooltip("เริ่มต้นแบบไม่ Highlight")]
    public bool startDisabled = true;

    // เก็บ material เดิมเพื่อสลับกลับ
    private Material[][] _originalMats;
    private bool _initialized = false;
    private bool _isHighlighted = false;

    void Awake()
    {
        // ถ้าไม่มี target ให้ดึง Renderer ทั้งหมดในลูกหลาน
        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            targetRenderers = GetComponentsInChildren<Renderer>();
        }

        // เก็บ material เดิม
        _originalMats = new Material[targetRenderers.Length][];
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            var r = targetRenderers[i];
            if (r != null)
            {
                _originalMats[i] = r.sharedMaterials;
            }
        }

        _initialized = true;

        // ตั้งค่า highlight ตาม startDisabled
        SetHighlight(!startDisabled);
    }

    /// <summary>
    /// สลับ highlight ของ object
    /// </summary>
    public void SetHighlight(bool value)
    {
        if (!_initialized || _isHighlighted == value) return;
        _isHighlighted = value;

        if (value)
            ApplyHighlight();
        else
            RestoreOriginal();
    }

    /// <summary>
    /// ใช้ highlightMaterial แทน material เดิม
    /// </summary>
    private void ApplyHighlight()
    {
        if (highlightMaterial == null)
        {
            Debug.LogWarning($"[HighlightItem] {name} ไม่มี highlightMaterial ให้ตั้งใน Inspector");
            return;
        }

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            var r = targetRenderers[i];
            if (r == null) continue;

            // ใช้ highlightMaterial ทุกช่อง แต่ reuse array เพื่อลด allocation
            int len = r.sharedMaterials.Length;
            Material[] newMats = new Material[len];
            for (int j = 0; j < len; j++)
                newMats[j] = highlightMaterial;

            r.sharedMaterials = newMats;
        }
    }

    /// <summary>
    /// คืน material เดิมกลับ
    /// </summary>
    private void RestoreOriginal()
    {
        for (int i = 0; i < targetRenderers.Length && i < _originalMats.Length; i++)
        {
            var r = targetRenderers[i];
            if (r == null || _originalMats[i] == null) continue;
            r.sharedMaterials = _originalMats[i];
        }
    }
}
