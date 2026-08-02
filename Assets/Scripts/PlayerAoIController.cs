using Fusion;
using UnityEngine;

public class PlayerAoIController : NetworkBehaviour
{
    [Tooltip("Mobil cihazlar için optimize edilmiþ görüþ yarýçapý.")]
    [SerializeField] private float viewRadius = 30f;

    // Optimizasyon: Sadece belirli bir mesafe gidildiðinde güncelleme yapmak için threshold.
    private Vector3 _lastAoIPosition;
    private readonly float _updateThresholdSq = 4f; // 2 metrelik hareket (Karekök hesabýndan kaçýnmak için karesi alýnýr)

    public override void Spawned()
    {
        if (Runner.IsServer)
        {
            UpdateAoI();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Runner.IsServer) return;

        // Karekök kullanmayan, iþlemci dostu mesafe kontrolü (Zero-Math)
        if ((transform.position - _lastAoIPosition).sqrMagnitude > _updateThresholdSq)
        {
            UpdateAoI();
        }
    }

    private void UpdateAoI()
    {
        Runner.AddPlayerAreaOfInterest(Object.InputAuthority, transform.position, viewRadius);
        _lastAoIPosition = transform.position;
    }
}