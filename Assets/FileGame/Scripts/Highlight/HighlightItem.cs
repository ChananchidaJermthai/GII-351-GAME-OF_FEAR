using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class HighlightItem : MonoBehaviour
{
    [Header("Highlight Settings")]
    [Tooltip("ใส่ Material ที่ใช้ตอน Highlight (เช่น Outline / Emission)")]
    public Material highlightMaterial;

    [Tooltip("ถ้าเว้นว่างไว้ จะใช้ Renderer ทั้งหมดในลูกหลานอัตโนมัติ")]
    public Renderer[] targetRenderers;

    [Tooltip("ให้เริ่มแบบไม่ Highlight")]
    public bool startDisabled = true;

    // เก็บ material เดิมไว้เพื่อสลับกลับ
    List<Material[]> _originalMats = new List<Material[]>();
    bool _initialized = false;
    bool _isHighlighted = false;

    void Awake()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            targetRenderers = GetComponentsInChildren<Renderer>();
        }

        foreach (var r in targetRenderers)
        {
            if (r == null) continue;
            _originalMats.Add(r.sharedMaterials);
        }

        _initialized = true;

        if (startDisabled)
            SetHighlight(false);
        else
            SetHighlight(true);
    }

    public void SetHighlight(bool value)
    {
        if (!_initialized) return;
        if (_isHighlighted == value) return;
        _isHighlighted = value;

        if (value)
            ApplyHighlight();
        else
            RestoreOriginal();
    }

    void ApplyHighlight()
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

            // ทำ array material ใหม่ที่ยาวเท่าเดิม แต่แทนที่ทุกช่องด้วย highlightMaterial
            Material[] newMats = new Material[r.sharedMaterials.Length];
            for (int m = 0; m < newMats.Length; m++)
                newMats[m] = highlightMaterial;

            r.sharedMaterials = newMats;
        }
    }

    void RestoreOriginal()
    {
        for (int i = 0; i < targetRenderers.Length && i < _originalMats.Count; i++)
        {
            var r = targetRenderers[i];
            if (r == null) continue;
            r.sharedMaterials = _originalMats[i];
        }
    }
}
