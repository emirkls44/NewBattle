using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Takip Ayarlarý")]
    public Transform target;
    public float smoothTime = 0.125f;
    public Vector3 offset = new Vector3(0f, 15f, -10f);

    private Vector3 _currentVelocity = Vector3.zero;

    private void Start()
    {
        transform.rotation = Quaternion.Euler(55f, 0f, 0f);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Vektör bileþenlerini doðrudan atayarak tahsisten (GC) kaçýnma
        Vector3 desiredPosition = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            target.position.z + offset.z
        );

        // Frame-rate baðýmsýz, matematiksel olarak doðru ve titremeyi önleyen yaklaþým
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _currentVelocity, smoothTime);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}