using UnityEngine;
using UnityEngine.UI;

public class MusicSlider : MonoBehaviour
{
    [SerializeField] private AudioSource musicSource; // ลาก BGM AudioSource มาใส่
    [SerializeField] private Slider slider;           // ลาก Slider มาใส่

    private void Start()
    {
        // โหลดค่าที่เคยเซฟไว้
        float vol = PlayerPrefs.GetFloat("bgm_volume", 0.8f);

        // ตั้งค่า slider โดยไม่ให้ยิง event
        slider.SetValueWithoutNotify(vol);

        // ตั้งค่าเสียงตามที่เคยเซฟ
        musicSource.volume = vol;

        // ผูก event (หลังจากตั้งค่า default เสร็จแล้ว)
        slider.onValueChanged.AddListener(SetVolume);
    }

    private void OnDestroy()
    {
        slider.onValueChanged.RemoveListener(SetVolume);
    }

    public void SetVolume(float value)
    {
        musicSource.volume = value;              // 0 ถึง 1
        PlayerPrefs.SetFloat("bgm_volume", value); // เซฟค่าไว้ใช้คราวหน้า
    }
}
