using UnityEngine;

public class CanBariSabitleyici : MonoBehaviour
{
    private Transform kamera;

    void Start()
    {
        // Unity 2020+ sürümlerinde Camera.main arkada önbelleklendiði için GC oluþturmaz, kullanýmý güvenlidir.
        if (Camera.main != null)
        {
            kamera = Camera.main.transform;
        }
    }

    void LateUpdate()
    {
        if (kamera != null)
        {
            // PERFORMANS: LookAt (Trigonometri) yerine doðrudan rotasyon kopyalama (Zero-Math).
            transform.rotation = kamera.rotation;
        }
    }
}