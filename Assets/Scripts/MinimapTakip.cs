using UnityEngine;

public class MinimapTakip : MonoBehaviour
{
    public Transform player;
    public float yukseklik = 50f;

    // Önbelleðe alýnmýþ sabit rotasyon (Her karede Quaternion.Euler hesaplanmaz)
    private Quaternion mapRotation = Quaternion.Euler(90f, 0f, 0f);

    void LateUpdate()
    {
        if (player != null)
        {
            // PERFORMANS: SetPositionAndRotation C++ tarafýnda tek seferde iþlenerek daha az CPU döngüsü harcar.
            transform.SetPositionAndRotation(
                new Vector3(player.position.x, yukseklik, player.position.z),
                mapRotation
            );
        }
    }
}