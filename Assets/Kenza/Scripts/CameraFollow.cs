using UnityEngine;

/// <summary>
/// Smooth chase camera: stays behind and above the target and looks slightly ahead of it.
/// Put it on the Main Camera and drag the Vehicle into "Target".
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;

    [Header("Position")]
    [Tooltip("Offset relative to the car: x = side, y = height, z = distance (negative = behind).")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 4f, -7f);
    [Tooltip("Lower = snappier, higher = floatier.")]
    [SerializeField] private float positionSmoothTime = 0.1f;
    [Tooltip("Max distance (m) the camera may lag behind its ideal spot. 0 = no smoothing at all.")]
    [SerializeField] private float maxLagDistance = 0.5f;

    [Header("Rotation")]
    [Tooltip("Height above the car's pivot the camera looks at.")]
    [SerializeField] private float lookAtHeight = 1f;
    [SerializeField] private float rotationSmoothSpeed = 5f;

    private Vector3 currentVelocity;

    private void Start()
    {
        if (target != null) SnapToTarget(); // avoid a big swoop on the first frame
    }

    // LateUpdate runs after the car has moved this frame -> no jitter.
    private void LateUpdate()
    {
        if (target == null) return;

        // Only use the car's yaw, so bumps/tilts don't shake the camera.
        Quaternion yaw = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
        Vector3 desiredPosition = target.position + yaw * offset;

        Vector3 smoothed = Vector3.SmoothDamp(
            transform.position, desiredPosition, ref currentVelocity, positionSmoothTime);

        // Hard leash: never let the camera drift more than maxLagDistance from its ideal spot.
        Vector3 lag = smoothed - desiredPosition;
        if (lag.magnitude > maxLagDistance)
            smoothed = desiredPosition + lag.normalized * maxLagDistance;

        transform.position = smoothed;

        Vector3 lookPoint = target.position + Vector3.up * lookAtHeight;
        Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, desiredRotation, rotationSmoothSpeed * Time.deltaTime);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null) SnapToTarget();
    }

    private void SnapToTarget()
    {
        Quaternion yaw = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
        transform.position = target.position + yaw * offset;
        transform.LookAt(target.position + Vector3.up * lookAtHeight);
    }
}