using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class Outline : MonoBehaviour
{
    public enum Mode
    {
        OutlineAll,
        OutlineVisible,
        OutlineHidden,
        OutlineAndSilhouette,
        SilhouetteOnly
    }

    [Header("Outline Settings")]
    public Mode outlineMode;
    public Color outlineColor = Color.white;
    [Range(0f, 10f)] public float outlineWidth = 2f;

    [Header("Distance Check")]
    public bool useDistanceCheck = true;
    public float maxDistance = 10f;
    public string targetTag = "Player";

    private Transform player;
    private Renderer[] renderers;
    private Material outlineMaskMaterial;
    private Material outlineFillMaterial;
    private bool isEnabled = false;
    private bool materialsReady = false;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();

        outlineMaskMaterial = Instantiate(Resources.Load<Material>("Materials/OutlineMask"));
        outlineFillMaterial = Instantiate(Resources.Load<Material>("Materials/OutlineFill"));

        outlineMaskMaterial.name = "OutlineMask (Instance)";
        outlineFillMaterial.name = "OutlineFill (Instance)";

        UpdateMaterialProperties();
        materialsReady = true;
    }

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag(targetTag);
        if (p != null)
            player = p.transform;

        DisableOutlineMaterials(); // เริ่มต้นปิดไว้ก่อน
    }

    void Update()
    {
        if (!materialsReady) return;

        if (useDistanceCheck && player != null)
        {
            float distance = Vector3.Distance(transform.position, player.position);
            bool inRange = distance <= maxDistance;

            if (inRange && !isEnabled)
            {
                EnableOutlineMaterials();
            }
            else if (!inRange && isEnabled)
            {
                DisableOutlineMaterials();
            }
        }

        // ไม่ใช้ Distance Check = เปิดค้างเสมอ
        if (!useDistanceCheck && !isEnabled)
        {
            EnableOutlineMaterials();
        }
    }

    // -----------------------------------------------------
    // MATERIAL CONTROL
    // -----------------------------------------------------

    private void EnableOutlineMaterials()
    {
        foreach (var renderer in renderers)
        {
            var mats = renderer.sharedMaterials.ToList();
            if (!mats.Contains(outlineMaskMaterial))
            {
                mats.Add(outlineMaskMaterial);
                mats.Add(outlineFillMaterial);
            }
            renderer.materials = mats.ToArray();
        }

        UpdateMaterialProperties(); // อัพเดทสี/ความหนาทันที
        isEnabled = true;
    }

    private void DisableOutlineMaterials()
    {
        foreach (var renderer in renderers)
        {
            var mats = renderer.sharedMaterials.ToList();
            mats.Remove(outlineMaskMaterial);
            mats.Remove(outlineFillMaterial);
            renderer.materials = mats.ToArray();
        }
        isEnabled = false;
    }

    // -----------------------------------------------------
    // OUTLINE MATERIAL PROPERTIES
    // -----------------------------------------------------

    private void UpdateMaterialProperties()
    {
        if (outlineFillMaterial == null) return;
        if (outlineMaskMaterial == null) return;

        outlineFillMaterial.SetColor("_OutlineColor", outlineColor);
        outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);

        switch (outlineMode)
        {
            case Mode.OutlineAll:
                outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                break;

            case Mode.OutlineVisible:
                outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
                break;

            case Mode.OutlineHidden:
                outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Greater);
                break;

            case Mode.OutlineAndSilhouette:
                outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
                outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                break;

            case Mode.SilhouetteOnly:
                outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
                outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Greater);
                outlineFillMaterial.SetFloat("_OutlineWidth", 0f);
                break;
        }
    }

    // สำหรับเวลาแก้ค่าใน Inspector ตอนรัน
    void OnValidate()
    {
        if (outlineFillMaterial != null && outlineMaskMaterial != null)
            UpdateMaterialProperties();
    }
}
