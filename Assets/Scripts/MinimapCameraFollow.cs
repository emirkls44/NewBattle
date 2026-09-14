using UnityEngine;

public class MinimapCameraFollow : MonoBehaviour
{
    [Header("Settings")]
    public Transform playerTarget;
    public float mapHeight = 50f;

    private readonly Quaternion _mapRotation = Quaternion.Euler(90f, 0f, 0f);
    private Vector3 _desiredPosition;

    void LateUpdate()
    {
        if (playerTarget == null) return;

        _desiredPosition.x = playerTarget.position.x;
        _desiredPosition.y = mapHeight;
        _desiredPosition.z = playerTarget.position.z;

        transform.SetPositionAndRotation(_desiredPosition, _mapRotation);
    }

    public void SetTarget(Transform newTarget)
    {
        playerTarget = newTarget;
    }
}