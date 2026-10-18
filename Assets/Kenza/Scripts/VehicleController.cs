using UnityEngine;

/// <summary>
/// Forward / coast / brake movement for the kids' car.
/// - Go held      -> accelerates up to maxSpeed
/// - Go released  -> slows down gradually (coasting)
/// - Stop held    -> brakes hard (stops in ~0.5 s from top speed)
/// Stop always wins if both buttons are held.
/// Steering is NOT handled here (out of scope), so a teammate can add it on top.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class VehicleController : MonoBehaviour
{
    [Header("Speed (m/s)")]
    [SerializeField] private float maxSpeed = 8f;            // ~30 km/h, gentle for kids

    [Header("Rates (m/s per second)")]
    [SerializeField] private float acceleration = 4f;        // 0 -> max in ~2 s
    [SerializeField] private float coastDeceleration = 2.5f; // max -> 0 in ~3 s when Go is released
    [SerializeField] private float brakeDeceleration = 16f;  // max -> 0 in ~0.5 s when Stop is held

    [Header("Physics")]
    [Tooltip("Lowers the centre of mass so the car doesn't tip over.")]
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.4f, 0f);

    [Header("Editor testing")]
    [SerializeField] private bool allowKeyboardInEditor = true; // Up arrow = Go, Space = Stop

    private Rigidbody rb;
    private bool goPressed;
    private bool stopPressed;

    /// <summary>Current forward speed in m/s (useful for UI, sounds, etc.).</summary>
    public float CurrentSpeed { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass += centerOfMassOffset;
        rb.interpolation = RigidbodyInterpolation.Interpolate; // smooth visuals for the camera
    }

    // ---------- Called by the HoldButton events ----------
    public void StartAccelerating() => goPressed = true;
    public void StopAccelerating()  => goPressed = false;
    public void StartBraking()      => stopPressed = true;
    public void StopBraking()       => stopPressed = false;

    private void FixedUpdate()
    {
        bool go = goPressed;
        bool brake = stopPressed;

#if UNITY_EDITOR && ENABLE_LEGACY_INPUT_MANAGER
        if (allowKeyboardInEditor)
        {
            go    |= Input.GetKey(KeyCode.UpArrow);
            brake |= Input.GetKey(KeyCode.Space);
        }
#endif

        Vector3 velocity = GetVelocity();
        float speed = Vector3.Dot(velocity, transform.forward); // forward part of the velocity
        float dt = Time.fixedDeltaTime;

        if (brake)
            speed = Mathf.MoveTowards(speed, 0f, brakeDeceleration * dt);
        else if (go)
            speed = Mathf.MoveTowards(speed, maxSpeed, acceleration * dt);
        else
            speed = Mathf.MoveTowards(speed, 0f, coastDeceleration * dt);

        CurrentSpeed = speed;

        // Keep gravity (vertical velocity), replace horizontal movement with our forward speed.
        Vector3 newVelocity = transform.forward * speed;
        newVelocity.y = velocity.y;
        SetVelocity(newVelocity);
    }

    // Unity 6 renamed Rigidbody.velocity to linearVelocity.
    private Vector3 GetVelocity()
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    private void SetVelocity(Vector3 v)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }
}
