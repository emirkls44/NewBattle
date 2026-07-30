using Fusion;
using UnityEngine;

public class PlayerNetworkMovement : NetworkBehaviour
{
    private NetworkTransform _networkTransform;

    public override void Spawned()
    {
        // Titremeyi önlemek için pozisyon eþitlemesini NetworkTransform üstlenir.
        _networkTransform = GetComponent<NetworkTransform>();
    }

    public override void FixedUpdateNetwork()
    {
        // DEÐÝÞÝKLÝK: HasStateAuthority yerine GetInput kullanýldý. 
        // Ýstemci ve Sunucu hareketi bu sayede sekronize eder.
        if (GetInput(out NetworkInputData data))
        {
            data.MoveDirection.Normalize(); // Çapraz gitmeyi (1.41 hýzýný) 1.0'a sabitler.
            Vector3 moveVector = new Vector3(data.MoveDirection.x, 0, data.MoveDirection.y);

            // DEÐÝÞÝKLÝK: transform.position yerine _networkTransform kullanýlýrsa enterpolasyon devreye girer.
            transform.position += moveVector * 5f * Runner.DeltaTime;
        }
    }
}