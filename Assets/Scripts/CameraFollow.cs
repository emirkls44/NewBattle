using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Takip Ayarlari")]
    public Transform target;
    public Vector3 offset = new(0f, 15f, -10f);
    public float followSpeed = 25f;

    [Header("Harita Kamera Siniri")]
    public Vector2 mapCenter = Vector2.zero;
    public float cameraFocusRadius = 25f;
    public float cameraAngle = 55f;

    private void Start()
    {
        transform.rotation = Quaternion.Euler(cameraAngle, 0f, 0f);
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 desiredPosition = GetDesiredPosition();
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            Time.deltaTime * followSpeed
        );
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;

        if (target != null)
            transform.position = GetDesiredPosition();
    }

    private Vector3 GetDesiredPosition()
    {
        Vector2 targetFromCenter = new(
            target.position.x - mapCenter.x,
            target.position.z - mapCenter.y
        );

        // Harita kare: kamerayi daire icinde tutmak, kosedeki oyuncunun
        // ekranin kenarina kaymasina yol acardi.
        targetFromCenter = new Vector2(
            Mathf.Clamp(targetFromCenter.x, -cameraFocusRadius, cameraFocusRadius),
            Mathf.Clamp(targetFromCenter.y, -cameraFocusRadius, cameraFocusRadius));

        Vector3 focusPosition = new(
            mapCenter.x + targetFromCenter.x,
            target.position.y,
            mapCenter.y + targetFromCenter.y
        );

        return focusPosition + offset;
    }
}
