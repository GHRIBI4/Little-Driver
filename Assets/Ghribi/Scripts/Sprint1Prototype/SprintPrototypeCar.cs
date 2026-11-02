using System;
using System.Collections.Generic;
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

        [Header("Horn manners")]
        [SerializeField] private AudioClip hornSound;
        [Tooltip("Tunisian Arabic: بالشويّة على الزمّارة! نستعملوها كان وقت الحاجة.")]
        [SerializeField] private AudioClip hornWarningVoice;
        [SerializeField, Range(0f, 1f)] private float hornWarningVolume = 1f;
        [SerializeField, Min(0.1f)] private float hornPressWindow = 2f;
        [SerializeField, Min(1)] private int hornPressLimit = 3;
        [SerializeField, Min(0.1f)] private float maximumHornHold = 2f;
        [SerializeField, Min(0.1f)] private float hornCooldown = 3f;

        [Header("Gentle collision recovery")]
        [SerializeField] private AudioClip collisionSound;
        [SerializeField] private ParticleSystem collisionEffect;
        [SerializeField, Min(0.1f)] private float recoveryDistance = 2f;
        [SerializeField, Min(0.1f)] private float recoveryDuration = 0.65f;

        public bool IsBuckled { get; private set; }
        public bool IsFinished { get; private set; }
        public int CollisionCount { get; private set; }
        public int HornCount { get; private set; }
        public string LastContact { get; private set; } = "None";
        public bool IsHonking => hornSource != null && hornSource.isPlaying;
        public bool IsHornBlocked => Time.unscaledTime < hornBlockedUntil || (voiceSource != null && voiceSource.isPlaying);
        public bool CanHorn => IsBuckled && !IsFinished && !IsRecovering && !IsHornBlocked && !hornNeedsRelease;
        public bool IsRecovering { get; private set; }
        public float CollisionUntil { get; private set; }
        public float Speed => Mathf.Max(0, body.linearVelocity.z);
        public float MaximumSpeedKmh
        {
            get => maximumSpeed * 3.6f;
            set => maximumSpeed = Mathf.Max(1, value) / 3.6f;
        }
        public int Lane => lane;
        public event Action Restarted;


        private Rigidbody body;
        private AudioSource hornSource;
        private AudioClip hornClip;
        private AudioClip bumpClip;
        private AudioSource voiceSource;
        private AudioSource effectsSource;
        private readonly Queue<float> hornPresses = new Queue<float>();
        private float hornStartedAt;
        private float hornBlockedUntil;
        private bool hornWasHeld;
        private bool hornNeedsRelease;
        private Vector3 recoveryStart;
        private float recoveryElapsed;
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
            if (hornSound == null)
            {
                hornClip = AudioClip.Create("Prototype gentle horn", samples.Length, 1, sampleRate, false);
                hornClip.SetData(samples, 0);
            }
            hornSource.clip = hornSound != null ? hornSound : hornClip;
            hornSource.loop = true;
            hornSource.playOnAwake = false;
            voiceSource = gameObject.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            effectsSource = gameObject.AddComponent<AudioSource>();
            effectsSource.playOnAwake = false;
            if (collisionSound == null)
            {
                var bumpSamples = new float[(int)(sampleRate * 0.25f)];
                for (int i = 0; i < bumpSamples.Length; i++)
                {
                    float t = (float)i / sampleRate;
                    bumpSamples[i] = Mathf.Sin(2 * Mathf.PI * (520 * t - 360 * t * t)) *
                        Mathf.Sin(Mathf.PI * t / 0.25f) * Mathf.Exp(-12 * t) * 0.25f;
                }
                bumpClip = AudioClip.Create("Prototype soft bump", bumpSamples.Length, 1, sampleRate, false);
                bumpClip.SetData(bumpSamples, 0);
            }
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
            if (IsRecovering)
            {
                recoveryElapsed += Time.fixedDeltaTime;
                float progress = Mathf.Clamp01(recoveryElapsed / recoveryDuration);
                body.MovePosition(recoveryStart + Vector3.back * (recoveryDistance * Mathf.SmoothStep(0, 1, progress)));
                if (progress >= 1)
                {
                    body.position = recoveryStart + Vector3.back * recoveryDistance;
                    IsRecovering = false;
                    body.isKinematic = false;
                    body.linearVelocity = Vector3.zero;
                }
                return;
            }
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
            bool held = hornHeld || keyboardHeld;
            if (!held) hornNeedsRelease = false;
            bool pressed = held && !hornWasHeld;
            hornWasHeld = held;
            if (!held || !CanHorn)
            {
                hornSource.Stop();
                return;
            }
            float now = Time.unscaledTime;
            if (pressed)
            {
                while (hornPresses.Count > 0 && now - hornPresses.Peek() > hornPressWindow) hornPresses.Dequeue();
                hornPresses.Enqueue(now);
                HornCount++;
                if (hornPresses.Count >= hornPressLimit)
                {
                    BlockHorn();
                    return;
                }
                hornStartedAt = now;
                hornSource.Play();
            }
            if (hornSource.isPlaying && now - hornStartedAt >= maximumHornHold) BlockHorn();
        }

        private void BlockHorn()
        {
            hornSource.Stop();
            hornNeedsRelease = true;
            hornBlockedUntil = Time.unscaledTime + hornCooldown;
            hornPresses.Clear();
            if (hornWarningVoice != null) voiceSource.PlayOneShot(hornWarningVoice, hornWarningVolume);
            else Debug.LogWarning("Assign Horn Warning Voice: بالشويّة على الزمّارة! نستعملوها كان وقت الحاجة.", this);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsBuckled || IsFinished || IsRecovering) return;
            // Supporting road contacts are not obstacle impacts.
            bool obstacleContact = false;
            for (int i = 0; i < collision.contactCount; i++)
                if (collision.GetContact(i).normal.y < 0.65f) obstacleContact = true;
            if (!obstacleContact) return;
            CollisionCount++;
            LastContact = collision.gameObject.name;
            CollisionUntil = Time.unscaledTime + 1.5f;
            requestedSpeed = 0;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            recoveryStart = body.position;
            recoveryElapsed = 0;
            IsRecovering = true;
            body.isKinematic = true;
            hornSource.Stop();
            hornNeedsRelease = true;
            effectsSource.PlayOneShot(collisionSound != null ? collisionSound : bumpClip);
            if (collisionEffect != null)
            {
                collisionEffect.transform.position = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
                collisionEffect.Play();
            }
            Debug.Log($"Player collided with '{LastContact}' (collision #{CollisionCount}).", this);
        }

        public void ReleaseControls()
        {
            goHeld = false;
            brakeHeld = false;
            hornHeld = false;
            hornWasHeld = false;
            hornNeedsRelease = true;
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
            IsRecovering = false;
            body.isKinematic = false;
            requestedSpeed = 0;
            lane = 1;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = startPosition;
            body.rotation = startRotation;
            CollisionCount = HornCount = 0;
            LastContact = "None";
            CollisionUntil = 0;
            hornBlockedUntil = 0;
            hornPresses.Clear();
            hornNeedsRelease = false;
            voiceSource.Stop();
            effectsSource.Stop();
            if (collisionEffect != null) collisionEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            hornSource.Stop();
            Restarted?.Invoke();
        }

        private void OnDestroy()
        {
            if (hornClip != null) Destroy(hornClip);
            if (bumpClip != null) Destroy(bumpClip);
        }
    }
}
