using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Kamera Mesafesi (Canlý Ayarlanabilir)")]
    public Vector3 offset = new Vector3(0f, 15f, -10f);

    private Transform _target;

    // DEÐÝÞÝKLÝK: null kontrolü yükünden kurtulmak için hedefi oyuna girince bir kez atýyoruz.
    // Oyuncu doðduðunda (Spawned) þu metodu çaðýr: Camera.main.GetComponent<CameraFollow>().SetTarget(transform);
    public void SetTarget(Transform playerTransform)
    {
        _target = playerTransform;
    }

    void LateUpdate()
    {
        // Daha performanslý referans kontrolü
        if (_target)
        {
            transform.position = _target.position + offset;
        }
    }
}