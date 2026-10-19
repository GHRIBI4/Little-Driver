using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LittleDriver.Prototype
{
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider), typeof(AudioSource))]
    public sealed class SprintPrototypeCar : MonoBehaviour
    {
        [Header("Straight two-lane course")]
        [SerializeField] private float laneOffset = 1.75f;
        [SerializeField] private float laneChangeSpeed = 5f;
        [SerializeField] private float maximumSpeed = 10f;
        [SerializeField] private float acceleration = 4f;
        [SerializeField] private float coastingDeceleration = 2f;
        [SerializeField] private float brakingDeceleration = 18f;
        [SerializeField] private float finishZ = 110f;
        [SerializeField] private Camera followCamera;
        [SerializeField] private Vector3 cameraOffset = new Vector3(0, 12, -11);
        [SerializeField] private float cameraLookAhead = 3f;

        public bool IsBuckled { get; private set; }
        public bool IsFinished { get; private set; }
        public int CollisionCount { get; private set; }
        public int HornCount { get; private set; }
        public string LastContact { get; private set; } = "None";
        public bool IsHonking => hornSource != null && hornSource.isPlaying;
        public float CollisionUntil { get; private set; }
        public float Speed => Mathf.Max(0, body.linearVelocity.z);
        public int Lane => lane;
        public event Action Restarted;

        private Rigidbody body;
        private AudioSource hornSource;
        private AudioClip hornClip;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private int lane = 1;
        private float requestedSpeed;
        private bool goHeld;
        private bool brakeHeld;
        private bool hornHeld;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            hornSource = GetComponent<AudioSource>();
            startPosition = body.position;
            startRotation = body.rotation;
            // Temporary synthesized sound; replace with a licensed horn recording for final art.
            const int sampleRate = 22050;
            var samples = new float[(int)(sampleRate * 0.22f)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Min(1, t / 0.015f) * Mathf.Min(1, (0.22f - t) / 0.06f);
                samples[i] = (Mathf.Sin(t * 330 * 2 * Mathf.PI) + Mathf.Sin(t * 440 * 2 * Mathf.PI)) * 0.12f * envelope;
            }
            hornClip = AudioClip.Create("Prototype gentle horn", samples.Length, 1, sampleRate, false);
            hornClip.SetData(samples, 0);
            hornSource.clip = hornClip;
            hornSource.loop = true;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            UpdateHorn();
            if (keyboard == null) return;
            if (keyboard.rKey.wasPressedThisFrame) Restart();
            if (!IsBuckled || IsFinished) return;
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) SelectLeftLane();
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) SelectRightLane();
        }

        private void FixedUpdate()
        {
            if (!IsBuckled || IsFinished)
            {
                requestedSpeed = 0;
                body.linearVelocity = Vector3.zero;
                return;
            }

            var keyboard = Keyboard.current;
            bool braking = brakeHeld || (keyboard != null && (keyboard.spaceKey.isPressed || keyboard.downArrowKey.isPressed));
            bool accelerating = goHeld || (keyboard != null && (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed));
            float target = accelerating && !braking ? maximumSpeed : 0;
            float rate = braking ? brakingDeceleration : accelerating ? acceleration : coastingDeceleration;
            requestedSpeed = Mathf.MoveTowards(requestedSpeed, target, rate * Time.fixedDeltaTime);
            float targetX = lane == 0 ? -laneOffset : laneOffset;
            float nextX = Mathf.MoveTowards(body.position.x, targetX, laneChangeSpeed * Time.fixedDeltaTime);
            body.linearVelocity = new Vector3((nextX - body.position.x) / Time.fixedDeltaTime, 0, requestedSpeed);

            if (body.position.z >= finishZ)
            {
                IsFinished = true;
                ReleaseControls();
                body.linearVelocity = Vector3.zero;
            }
        }

        private void LateUpdate()
        {
            if (followCamera == null) return;
            followCamera.transform.position = new Vector3(0, transform.position.y, transform.position.z) + cameraOffset;
            followCamera.transform.LookAt(new Vector3(0, 0, transform.position.z + cameraLookAhead));
        }

        public void SetGoHeld(bool held) => goHeld = held;
        public void SetBrakeHeld(bool held) => brakeHeld = held;
        public void SelectLeftLane() { if (IsBuckled && !IsFinished) lane = 0; }
        public void SelectRightLane() { if (IsBuckled && !IsFinished) lane = 1; }
        public void Buckle() => IsBuckled = true;

        public void SetHornHeld(bool held)
        {
            hornHeld = held;
            UpdateHorn();
        }

        private void UpdateHorn()
        {
            if (hornSource == null) return;
            bool keyboardHeld = Keyboard.current != null && Keyboard.current.hKey.isPressed;
            bool shouldPlay = IsBuckled && !IsFinished && (hornHeld || keyboardHeld);
            if (shouldPlay && !hornSource.isPlaying)
            {
                HornCount++;
                hornSource.Play();
            }
            else if (!shouldPlay && hornSource.isPlaying) hornSource.Stop();
        }

        private void OnCollisionEnter(Collision collision)
        {
            CollisionCount++;
            LastContact = collision.gameObject.name;
            CollisionUntil = Time.unscaledTime + 1.5f;
            requestedSpeed = 0;
            Debug.Log($"Player collided with '{LastContact}' (collision #{CollisionCount}).", this);
        }

        public void ReleaseControls()
        {
            goHeld = false;
            brakeHeld = false;
            hornHeld = false;
            if (hornSource != null) hornSource.Stop();
        }

        private void OnApplicationFocus(bool focused) { if (!focused) ReleaseControls(); }
        private void OnApplicationPause(bool paused) { if (paused) ReleaseControls(); }
        private void OnDisable() => ReleaseControls();

        public void Restart()
        {
            ReleaseControls();
            IsBuckled = false;
            IsFinished = false;
            requestedSpeed = 0;
            lane = 1;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = startPosition;
            body.rotation = startRotation;
            CollisionCount = HornCount = 0;
            LastContact = "None";
            CollisionUntil = 0;
            hornSource.Stop();
            Restarted?.Invoke();
        }

        private void OnDestroy() { if (hornClip != null) Destroy(hornClip); }
    }
}
