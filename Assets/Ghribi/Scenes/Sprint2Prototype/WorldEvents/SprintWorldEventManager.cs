using System;
using BehaviorDesigner.Runtime;
using LittleDriver.Prototype;
using UnityEngine;

namespace LittleDriver.WorldEvents
{
    public enum WorldEventKind { None, Police, Crossing, Departure }

    [Serializable]
    public class CrossingPedestrian
    {
        public Transform person;
        public Transform sideA;
        public Transform sideB;
        [Min(0.1f)] public float walkingSpeed = 1.8f;
        [NonSerialized] public bool atSideB;
        [NonSerialized] public Vector3 initialPosition;
        [NonSerialized] public Quaternion initialRotation;
        [NonSerialized] public Animator animator;
    }

    [Serializable]
    public class DepartingVehicle
    {
        public Transform vehicle;
        public Transform roadEnd;
        [Min(0.1f)] public float speedKmh = 18f;
        [NonSerialized] public bool activated;
        [NonSerialized] public bool moving;
        [NonSerialized] public Vector3 initialPosition;
        [NonSerialized] public Quaternion initialRotation;
        [NonSerialized] public Rigidbody body;
    }

    [DefaultExecutionOrder(-90)]
    public sealed class SprintWorldEventManager : MonoBehaviour
    {
        [Header("Scene references")]
        public SprintPrototypeCar player;
        public BehaviorTree eventTree;
        public Transform policeCar;
        public Transform policeVision;
        public Renderer redBeacon;
        public Renderer blueBeacon;
        public Light redLight;
        public Light blueLight;
        public AudioSource policeAudio;
        public AudioClip sirenClip;

        [Header("Speed in km/h")]
        [Min(1)] public float playerMaximumKmh = 55f;
        [Min(1)] public float speedingThresholdKmh = 50f;

        [Header("Police forward vision")]
        [Min(1)] public float policeDetectionDistance = 25f;
        [Range(10, 180)] public float policeVisionAngle = 110f;
        public bool requirePoliceLineOfSight = true;
        public LayerMask policeOcclusionLayers = ~0;
        [Min(0.1f)] public float policeWarningDuration = 1.5f;
        [Min(0)] public float policeCooldown = 5f;
        [Min(0.05f)] public float blinkInterval = 0.22f;
        [Range(0, 1)] public float sirenVolume = 0.25f;
        [Tooltip("Multiplies the police score. Higher = police wins more often.")]
        [Range(0.01f, 1)] public float policeImportance = 1f;

        [Header("Marked crossing only")]
        public Transform crossingCenter;
        public CrossingPedestrian[] pedestrians = new CrossingPedestrian[0];
        [Min(1)] public float crossingApproachDistance = 45f;
        [Min(0)] public float crossingMinimumLeadTime = 0.8f;
        [Min(0.1f)] public float crossingDecisionInterval = 1f;
        [Range(0, 1)] public float crossingChance = 0.65f;
        [Min(0)] public float crossingCooldown = 4f;
        [Tooltip("Multiplies the crossing score. Higher = pedestrians win more often.")]
        [Range(0.01f, 1)] public float crossingImportance = 0.85f;

        [Header("Manually selected parked vehicles")]
        public DepartingVehicle[] parkedVehicles = new DepartingVehicle[0];
        [Min(1)] public float departureDetectionDistance = 20f;
        [Tooltip("Multiplies the departure score. Higher = parked vehicles win more often.")]
        [Range(0.01f, 1)] public float departureImportance = 0.65f;

        public float PlayerSpeedKmh => player != null ? player.Speed * 3.6f : 0;
        public bool PlayerIsSpeeding => PlayerSpeedKmh > speedingThresholdKmh;
        public bool Ready => player != null && player.IsBuckled && !player.IsFinished && !player.IsRecovering;
        public WorldEventKind ActiveEvent { get; private set; }
        public float PoliceUtility => Score(WorldEventKind.Police);
        public float CrossingUtility => Score(WorldEventKind.Crossing);
        public float DepartureUtility => Score(WorldEventKind.Departure);
        public const float CommittedUtility = 1.1f;

        // These inputs are always between 0 and 1, so different actions can compete.
        public float PoliceSpeedUrgency
        {
            get
            {
                float speedAboveLimit = PlayerSpeedKmh - speedingThresholdKmh;
                float availableSpeedRange = Mathf.Max(1, playerMaximumKmh - speedingThresholdKmh);
                return Mathf.Clamp01(speedAboveLimit / availableSpeedRange);
            }
        }

        public float CrossingCloseness
        {
            get
            {
                if (player == null || crossingCenter == null) return 0;
                float fractionOfRangeRemaining = CrossingDistance() / crossingApproachDistance;
                return Mathf.Clamp01(1 - fractionOfRangeRemaining);
            }
        }

        public float DepartureCloseness
        {
            get
            {
                int vehicleIndex = NearestVehicle();
                if (vehicleIndex < 0) return 0;
                float distance = FlatDistance(player.transform.position, parkedVehicles[vehicleIndex].vehicle.position);
                float fractionOfRangeRemaining = distance / departureDetectionDistance;
                return Mathf.Clamp01(1 - fractionOfRangeRemaining);
            }
        }
        public float PoliceDistance => player != null && policeVision != null ? FlatDistance(player.transform.position, policeVision.position) : 0;
        public bool PlayerInPoliceVision => SeesPlayer();
        public bool CrossingRequested => requestedPedestrian >= 0;
        public int ActivePedestrian => activePedestrian;
        public int ActiveVehicle => activeVehicle;

        float policeReadyAt, crossingReadyAt, nextCrossingDecision, eventStartedAt;
        int requestedPedestrian = -1, activePedestrian = -1, activeVehicle = -1;
        AudioClip generatedSiren;
        MaterialPropertyBlock beaconProperties;

        void Awake()
        {
            if (player != null) player.MaximumSpeedKmh = playerMaximumKmh;
            beaconProperties = new MaterialPropertyBlock();
            foreach (var p in pedestrians)
            {
                if (p.person == null) continue;
                p.initialPosition = p.person.position;
                p.initialRotation = p.person.rotation;
                p.animator = p.person.GetComponent<Animator>();
            }
            foreach (var v in parkedVehicles)
            {
                if (v.vehicle == null) continue;
                v.initialPosition = v.vehicle.position;
                v.initialRotation = v.vehicle.rotation;
                v.body = v.vehicle.GetComponent<Rigidbody>();
            }
            if (policeAudio != null)
            {
                if (sirenClip == null)
                {
                    const int rate = 22050;
                    var samples = new float[rate * 2];
                    float phase = 0;
                    for (int i = 0; i < samples.Length; i++)
                    {
                        float t = (float)i / rate;
                        float frequency = 620 + 220 * Mathf.Sin(Mathf.PI * t);
                        phase += 2 * Mathf.PI * frequency / rate;
                        samples[i] = Mathf.Sin(phase) * 0.22f;
                    }
                    generatedSiren = AudioClip.Create("Temporary gentle police siren", samples.Length, 1, rate, false);
                    generatedSiren.SetData(samples, 0);
                }
                policeAudio.clip = sirenClip != null ? sirenClip : generatedSiren;
                policeAudio.loop = true;
                policeAudio.playOnAwake = false;
            }
            if (player != null) player.Restarted += ResetRun;
            SetBeacons(false, false);
        }

        void Update()
        {
            if (player != null) player.MaximumSpeedKmh = playerMaximumKmh;
            if (Time.time >= nextCrossingDecision)
            {
                nextCrossingDecision = Time.time + Mathf.Max(0.1f, crossingDecisionInterval);
                if (requestedPedestrian < 0 && ActiveEvent != WorldEventKind.Crossing && CrossingEligible() && UnityEngine.Random.value < crossingChance)
                {
                    int start = UnityEngine.Random.Range(0, pedestrians.Length);
                    for (int i = 0; i < pedestrians.Length; i++)
                    {
                        int index = (start + i) % pedestrians.Length;
                        var p = pedestrians[index];
                        if (p.person != null && p.person.gameObject.activeInHierarchy && p.sideA != null && p.sideB != null)
                        { requestedPedestrian = index; break; }
                    }
                }
            }
            if (!CrossingEligible() && ActiveEvent != WorldEventKind.Crossing) requestedPedestrian = -1;
            if (ActiveEvent == WorldEventKind.Police)
            {
                bool red = (int)((Time.time - eventStartedAt) / Mathf.Max(0.05f, blinkInterval)) % 2 == 0;
                SetBeacons(red, !red);
                if (policeAudio != null) policeAudio.volume = sirenVolume;
            }
        }

        void FixedUpdate()
        {
            if (ActiveEvent == WorldEventKind.Crossing && activePedestrian >= 0)
            {
                var p = pedestrians[activePedestrian];
                Vector3 target = (p.atSideB ? p.sideA : p.sideB).position;
                Vector3 direction = target - p.person.position;
                direction.y = 0;
                if (direction.sqrMagnitude > 0.001f) p.person.rotation = Quaternion.LookRotation(direction);
                p.person.position = Vector3.MoveTowards(p.person.position, target, p.walkingSpeed * Time.fixedDeltaTime);
            }
            foreach (var v in parkedVehicles)
            {
                if (!v.moving || v.vehicle == null || v.roadEnd == null) continue;
                Vector3 target = v.roadEnd.position;
                Vector3 next = Vector3.MoveTowards(v.vehicle.position, target, v.speedKmh / 3.6f * Time.fixedDeltaTime);
                if (v.body != null) v.body.MovePosition(next);
                else v.vehicle.position = next;
                if (Vector3.Distance(next, target) <= 0.1f)
                {
                    v.moving = false;
                    v.vehicle.gameObject.SetActive(false);
                }
            }
        }

        float Score(WorldEventKind kind)
        {
            // 1. Finish the current action before selecting a new one.
            if (ActiveEvent != WorldEventKind.None)
            {
                if (ActiveEvent == kind) return CommittedUtility;
                return 0;
            }

            // 2. If an action is not allowed, it cannot compete.
            if (GetBlockingReason(kind) != null) return 0;

            // 3. Each action owns its own clearly named calculation.
            switch (kind)
            {
                case WorldEventKind.Police:
                    return PoliceWarningAction.CalculateUtility(policeImportance, PoliceSpeedUrgency);
                case WorldEventKind.Crossing:
                    return PedestrianCrossingAction.CalculateUtility(crossingImportance, CrossingCloseness);
                case WorldEventKind.Departure:
                    return ParkedVehicleDepartureAction.CalculateUtility(departureImportance, DepartureCloseness);
                default:
                    return 0;
            }
        }

        // null means allowed; otherwise the Inspector shows why the score is zero.
        public string GetBlockingReason(WorldEventKind kind)
        {
            if (!Ready) return "Player must buckle up and be driving (not finished or recovering).";
            switch (kind)
            {
                case WorldEventKind.Police:
                    if (Time.time < policeReadyAt) return "Police warning is cooling down.";
                    if (!PlayerIsSpeeding) return "Player is not above the speed limit.";
                    if (!SeesPlayer()) return "Player is outside police vision or hidden by an obstacle.";
                    return null;
                case WorldEventKind.Crossing:
                    string crossingReason = GetCrossingConditionsBlockReason();
                    if (crossingReason != null) return crossingReason;
                    if (requestedPedestrian < 0) return "Waiting for the random crossing request.";
                    return null;
                case WorldEventKind.Departure:
                    if (NearestVehicle() < 0) return "No unused selected vehicle is close enough.";
                    return null;
                default:
                    return "No action selected.";
            }
        }


        float CrossingDistance() => Vector3.Dot(crossingCenter.position - player.transform.position, Vector3.forward);
        bool CrossingEligible() => GetCrossingConditionsBlockReason() == null;

        string GetCrossingConditionsBlockReason()
        {
            if (!Ready) return "Player is not ready to drive.";
            if (!PlayerIsSpeeding) return "Player is not above the speed limit.";
            if (crossingCenter == null || pedestrians.Length == 0) return "Assign a marked crossing and pedestrians.";
            if (Time.time < crossingReadyAt) return "Crossing is cooling down.";

            float distanceAhead = CrossingDistance();
            if (distanceAhead <= 0) return "Player has already passed the crossing.";
            if (distanceAhead > crossingApproachDistance) return "Crossing is too far ahead.";

            float secondsUntilPlayerArrives = distanceAhead / Mathf.Max(0.1f, player.Speed);
            if (secondsUntilPlayerArrives < crossingMinimumLeadTime) return "Player is already too close to start a crossing.";
            return null;
        }

        bool SeesPlayer()
        {
            if (player == null || policeVision == null) return false;
            Vector3 delta = player.transform.position - policeVision.position;
            delta.y = 0;
            if (delta.magnitude > policeDetectionDistance || Vector3.Angle(Vector3.ProjectOnPlane(policeVision.forward, Vector3.up), delta) > policeVisionAngle * 0.5f) return false;
            if (!requirePoliceLineOfSight) return true;
            Vector3 eye = policeVision.position;
            Vector3 target = player.transform.position + Vector3.up * 0.5f;
            var hits = Physics.RaycastAll(eye, (target - eye).normalized, Vector3.Distance(eye, target), policeOcclusionLayers, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(player.transform)) continue;
                if (policeCar != null && hit.transform.IsChildOf(policeCar)) continue;
                return false;
            }
            return true;
        }

        int NearestVehicle()
        {
            if (player == null) return -1;
            int best = -1;
            float nearest = departureDetectionDistance;
            for (int i = 0; i < parkedVehicles.Length; i++)
            {
                var v = parkedVehicles[i];
                if (v.activated || v.vehicle == null || v.roadEnd == null || !v.vehicle.gameObject.activeInHierarchy) continue;
                float distance = FlatDistance(player.transform.position, v.vehicle.position);
                if (distance <= nearest) { nearest = distance; best = i; }
            }
            return best;
        }

        public bool BeginEvent(WorldEventKind kind)
        {
            if (ActiveEvent == kind && kind != WorldEventKind.None) return true;
            if (Score(kind) <= 0 || ActiveEvent != WorldEventKind.None) return false;
            if (kind == WorldEventKind.Crossing)
            {
                activePedestrian = requestedPedestrian;
                requestedPedestrian = -1;
                if (activePedestrian < 0) return false;
                SetWalking(pedestrians[activePedestrian], true);
            }
            if (kind == WorldEventKind.Departure)
            {
                activeVehicle = NearestVehicle();
                if (activeVehicle < 0) return false;
                parkedVehicles[activeVehicle].activated = true;
                parkedVehicles[activeVehicle].moving = true;
            }
            ActiveEvent = kind;
            eventStartedAt = Time.time;
            if (kind == WorldEventKind.Police && policeAudio != null) policeAudio.Play();
            Debug.Log($"World event: {kind}", this);
            return true;
        }

        public bool EventComplete(WorldEventKind kind)
        {
            if (ActiveEvent != kind) return true;
            if (kind == WorldEventKind.Police) return Time.time - eventStartedAt >= policeWarningDuration;
            if (kind == WorldEventKind.Crossing)
            {
                var p = pedestrians[activePedestrian];
                return Vector3.Distance(p.person.position, (p.atSideB ? p.sideA : p.sideB).position) <= 0.03f;
            }
            // Departure is a one-time command. Movement continues independently.
            return true;
        }

        public void EndEvent(WorldEventKind kind, bool completed)
        {
            if (ActiveEvent != kind) return;
            if (kind == WorldEventKind.Police)
            {
                if (policeAudio != null) policeAudio.Stop();
                SetBeacons(false, false);
                policeReadyAt = Time.time + policeCooldown;
            }
            if (kind == WorldEventKind.Crossing && activePedestrian >= 0)
            {
                var p = pedestrians[activePedestrian];
                if (completed) p.atSideB = !p.atSideB;
                SetWalking(p, false);
                crossingReadyAt = Time.time + crossingCooldown;
                activePedestrian = -1;
            }
            if (kind == WorldEventKind.Departure && activeVehicle >= 0)
            {
                activeVehicle = -1;
            }
            ActiveEvent = WorldEventKind.None;
        }

        static void SetWalking(CrossingPedestrian p, bool walking)
        {
            if (p.animator != null) p.animator.SetBool("Walking", walking);
        }

        void SetBeacons(bool red, bool blue)
        {
            SetBeacon(redBeacon, redLight, Color.red, red);
            SetBeacon(blueBeacon, blueLight, new Color(0.05f, 0.25f, 1), blue);
        }

        void SetBeacon(Renderer beacon, Light light, Color color, bool on)
        {
            if (light != null) light.intensity = on ? 3 : 0;
            if (beacon == null || beaconProperties == null) return;
            beacon.GetPropertyBlock(beaconProperties);
            beaconProperties.SetColor("_BaseColor", color * (on ? 1 : 0.12f));
            beaconProperties.SetColor("_Color", color * (on ? 1 : 0.12f));
            beaconProperties.SetColor("_EmissionColor", on ? color * 4 : Color.black);
            beacon.SetPropertyBlock(beaconProperties);
        }

        public void ResetRun()
        {
            if (eventTree != null) eventTree.DisableBehavior();
            EndEvent(ActiveEvent, false);
            ActiveEvent = WorldEventKind.None;
            policeReadyAt = crossingReadyAt = nextCrossingDecision = 0;
            requestedPedestrian = activePedestrian = activeVehicle = -1;
            foreach (var p in pedestrians)
            {
                if (p.person == null) continue;
                p.person.SetPositionAndRotation(p.initialPosition, p.initialRotation);
                p.atSideB = false;
                SetWalking(p, false);
            }
            foreach (var v in parkedVehicles)
            {
                if (v.vehicle == null) continue;
                v.vehicle.gameObject.SetActive(true);
                v.vehicle.SetPositionAndRotation(v.initialPosition, v.initialRotation);
                if (v.body != null) { v.body.position = v.initialPosition; v.body.rotation = v.initialRotation; }
                v.activated = false;
                v.moving = false;
            }
            if (eventTree != null && isActiveAndEnabled) eventTree.EnableBehavior();
        }

        static float FlatDistance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }
        void OnDisable() { EndEvent(ActiveEvent, false); }
        void OnDestroy()
        {
            if (player != null) player.Restarted -= ResetRun;
            if (generatedSiren != null) Destroy(generatedSiren);
        }

        void OnDrawGizmosSelected()
        {
            if (policeVision != null)
            {
                Gizmos.color = Color.cyan;
                Vector3 origin = policeVision.position;
                Vector3 forward = Vector3.ProjectOnPlane(policeVision.forward, Vector3.up).normalized;
                Vector3 previous = origin + Quaternion.AngleAxis(-policeVisionAngle * 0.5f, Vector3.up) * forward * policeDetectionDistance;
                Gizmos.DrawLine(origin, previous);
                for (int i = 1; i <= 24; i++)
                {
                    Vector3 next = origin + Quaternion.AngleAxis(-policeVisionAngle * 0.5f + policeVisionAngle * i / 24, Vector3.up) * forward * policeDetectionDistance;
                    Gizmos.DrawLine(previous, next); previous = next;
                }
                Gizmos.DrawLine(origin, previous);
            }
            Gizmos.color = Color.yellow;
            foreach (var p in pedestrians) if (p.sideA != null && p.sideB != null) Gizmos.DrawLine(p.sideA.position, p.sideB.position);
            Gizmos.color = Color.green;
            foreach (var v in parkedVehicles) if (v.vehicle != null && v.roadEnd != null) Gizmos.DrawLine(v.vehicle.position, v.roadEnd.position);
        }
    }
}
