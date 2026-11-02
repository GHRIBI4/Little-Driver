#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;

namespace LittleDriver.WorldEvents
{
    // Attach temporarily in Play mode; no test component is saved in the scene.
    public sealed class WorldEventPlayChecks : MonoBehaviour
    {
        public string Result { get; private set; } = "Running";
        SprintWorldEventManager world;
        Rigidbody playerBody;
        Vector3 samplePosition;
        float sampleSpeed;
        bool driving;

        IEnumerator Start()
        {
            world = GetComponent<SprintWorldEventManager>();
            playerBody = world.player.GetComponent<Rigidbody>();
            var scenarios = Scenarios();
            while (true)
            {
                bool more;
                try { more = scenarios.MoveNext(); }
                catch (Exception e)
                {
                    Result = "FAIL: " + e.Message;
                    Debug.LogError(Result, this);
                    driving = false;
                    yield break;
                }
                if (!more) break;
                yield return scenarios.Current;
            }
            Result = "PASS: speed cap; front/rear/range/occluded police vision; speeding gate; selector priority; siren and alternating lights; crossing commitment and return trip; bus movement/disappearance/one-shot; restart reset.";
            Debug.Log(Result, this);
        }

        void FixedUpdate()
        {
            if (!driving) return;
            playerBody.position = samplePosition;
            playerBody.linearVelocity = Vector3.forward * (sampleSpeed / 3.6f);
        }

        void Pose(Vector3 position, float kmh)
        {
            samplePosition = position; sampleSpeed = kmh;
            playerBody.position = position;
            world.player.transform.position = position;
            playerBody.linearVelocity = Vector3.forward * (kmh / 3.6f);
            Physics.SyncTransforms();
        }

        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        IEnumerator Scenarios()
        {
            CheckUtilityMath();
            Check(Mathf.Abs(world.player.MaximumSpeedKmh - 55) < 0.01f, "Player cap must be 55 km/h");
            float originalDuration = world.policeWarningDuration, originalCooldown = world.policeCooldown;
            float originalRange = world.policeDetectionDistance, originalChance = world.crossingChance;
            float originalCrossCooldown = world.crossingCooldown, originalInterval = world.crossingDecisionInterval;
            float originalAdultSpeed = world.pedestrians[0].walkingSpeed, originalBusSpeed = world.parkedVehicles[0].speedKmh;
            world.player.Restart();
            world.player.Buckle();
            world.player.enabled = false;
            playerBody.detectCollisions = false;
            driving = true;
            world.eventTree.DisableBehavior();
            var front = world.policeVision.position + world.policeVision.forward * 8;
            front.y = 0.45f;
            Pose(front, 55);
            Check(world.PlayerInPoliceVision && world.PoliceUtility > 0, $"Police must detect a speeding player in front (vision={world.PlayerInPoliceVision}, score={world.PoliceUtility}, speed={world.PlayerSpeedKmh}, ready={world.Ready})");
            Pose(world.policeVision.position - world.policeVision.forward * 8, 55);
            Check(!world.PlayerInPoliceVision, "Police must not detect a player behind");
            Pose(world.policeVision.position + world.policeVision.forward * (originalRange + 5), 55);
            Check(!world.PlayerInPoliceVision, "Police must respect detection distance");
            Pose(front, 50);
            Check(world.PoliceUtility == 0 && world.CrossingUtility == 0, "50 km/h must not count as speeding");
            Pose(front, 55);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Temporary vision test wall";
            wall.transform.position = (world.policeVision.position + playerBody.position + Vector3.up * 0.5f) / 2;
            wall.transform.localScale = new Vector3(4, 4, 1);
            wall.transform.rotation = Quaternion.LookRotation(front - world.policeVision.position);
            Physics.SyncTransforms();
            Check(!world.PlayerInPoliceVision, "Police must not see through an obstacle");
            Destroy(wall);
            yield return null;

            world.policeWarningDuration = 0.8f;
            world.policeCooldown = 100;
            world.crossingChance = 1;
            world.crossingDecisionInterval = 0.1f;
            Pose(new Vector3(1.75f, 0.45f, 27), 55);
            yield return new WaitForSeconds(0.2f);
            Check(world.PoliceUtility > world.CrossingUtility && world.PoliceUtility > world.DepartureUtility, "Police must win simultaneous eligible events");
            world.eventTree.EnableBehavior();
            yield return new WaitForSeconds(0.1f);
            Check(world.ActiveEvent == WorldEventKind.Police && world.policeAudio.isPlaying, "Selector must start police sound");
            float red = world.redLight.intensity;
            yield return new WaitForSeconds(world.blinkInterval);
            Check(world.redLight.intensity != red && world.redLight.intensity + world.blueLight.intensity > 0, "Police beacons must alternate");
            Pose(new Vector3(1.75f, 0.45f, -25), 0);
            yield return new WaitForSeconds(0.8f);
            Check(!world.policeAudio.isPlaying && world.redLight.intensity == 0 && world.blueLight.intensity == 0, "Warning must stop sound and lights");

            world.policeDetectionDistance = 1;
            world.parkedVehicles[0].activated = true;
            world.pedestrians[1].person.gameObject.SetActive(false);
            world.pedestrians[0].walkingSpeed = 20;
            world.crossingCooldown = 0.2f;
            Pose(new Vector3(1.75f, 0.45f, 30), 55);
            yield return new WaitForSeconds(0.25f);
            Check(world.ActiveEvent == WorldEventKind.Crossing && world.pedestrians[0].animator.GetBool("Walking"), "Crossing must start with walking animation");
            Pose(new Vector3(1.75f, 0.45f, 30), 0);
            yield return new WaitForSeconds(0.8f);
            float crossingDeadline = Time.time + 2;
            yield return new WaitUntil(() => world.pedestrians[0].atSideB || Time.time > crossingDeadline);
            Check(world.pedestrians[0].atSideB && !world.pedestrians[0].animator.GetBool("Walking"), $"Pedestrian must finish crossing even after braking (sideB={world.pedestrians[0].atSideB}, pos={world.pedestrians[0].person.position}, target={world.pedestrians[0].sideB.position}, walking={world.pedestrians[0].animator.GetBool("Walking")}, event={world.ActiveEvent})");
            Pose(new Vector3(1.75f, 0.45f, 30), 55);
            yield return new WaitForSeconds(0.25f);
            Check(world.ActiveEvent == WorldEventKind.Crossing, "Pedestrian must be able to return");
            Pose(new Vector3(1.75f, 0.45f, 30), 0);
            yield return new WaitForSeconds(0.8f);
            crossingDeadline = Time.time + 2;
            yield return new WaitUntil(() => !world.pedestrians[0].atSideB || Time.time > crossingDeadline);
            Check(!world.pedestrians[0].atSideB && Vector3.Distance(world.pedestrians[0].person.position, world.pedestrians[0].sideA.position) < 0.1f, "Return trip must finish at original sidewalk");

            world.player.Restart(); world.player.Buckle();
            world.parkedVehicles[0].speedKmh = 180;
            Pose(new Vector3(1.75f, 0.45f, 7), 0);
            yield return new WaitForSeconds(0.25f);
            Check(world.parkedVehicles[0].moving && world.parkedVehicles[0].activated, "Proximity must activate selected bus");
            Check(world.parkedVehicles[0].vehicle.position.z > world.parkedVehicles[0].initialPosition.z, "Bus must move forward");
            yield return new WaitForSeconds(1.5f);
            Check(!world.parkedVehicles[0].vehicle.gameObject.activeSelf && world.DepartureUtility == 0, "Bus must disappear and stay unavailable");

            world.policeWarningDuration = originalDuration; world.policeCooldown = originalCooldown;
            world.policeDetectionDistance = originalRange; world.crossingChance = originalChance;
            world.crossingCooldown = originalCrossCooldown; world.crossingDecisionInterval = originalInterval;
            world.pedestrians[0].walkingSpeed = originalAdultSpeed;
            world.pedestrians[1].person.gameObject.SetActive(true);
            world.parkedVehicles[0].speedKmh = originalBusSpeed;
            driving = false;
            world.player.enabled = true;
            playerBody.detectCollisions = true;
            world.player.Restart();
            Check(world.parkedVehicles[0].vehicle.gameObject.activeSelf && !world.parkedVehicles[0].activated, "Restart must rearm bus");
            Check(Vector3.Distance(world.parkedVehicles[0].vehicle.position, world.parkedVehicles[0].initialPosition) < 0.01f, "Restart must restore bus position");
            Check(world.ActiveEvent == WorldEventKind.None && !world.pedestrians[0].atSideB && world.redLight.intensity == 0, "Restart must clear event state");
        }

        static void CheckUtilityMath()
        {
            // Compare the clearer calculations to the original formulas.
            foreach (float importance in new[] { 0.01f, 0.65f, 0.85f, 1f })
            {
                for (int i = 0; i <= 10; i++)
                {
                    float urgency = i / 10f;
                    float oldPoliceAndCrossing = importance * (0.6f + 0.4f * urgency);
                    float oldDeparture = importance * (0.5f + 0.5f * urgency);
                    Check(Mathf.Abs(PoliceWarningAction.CalculateUtility(importance, urgency) - oldPoliceAndCrossing) < 0.000001f, "Police calculation changed");
                    Check(Mathf.Abs(PedestrianCrossingAction.CalculateUtility(importance, urgency) - oldPoliceAndCrossing) < 0.000001f, "Crossing calculation changed");
                    Check(Mathf.Abs(ParkedVehicleDepartureAction.CalculateUtility(importance, urgency) - oldDeparture) < 0.000001f, "Departure calculation changed");
                }
            }
            Check(Mathf.Abs(PoliceWarningAction.CalculateUtility(1, 0.5f) - 0.8f) < 0.000001f, "Police example should equal 0.8");
            Check(Mathf.Abs(PedestrianCrossingAction.CalculateUtility(0.85f, 0.5f) - 0.68f) < 0.000001f, "Crossing example should equal 0.68");
            Check(Mathf.Abs(ParkedVehicleDepartureAction.CalculateUtility(0.65f, 0.5f) - 0.4875f) < 0.000001f, "Departure example should equal 0.4875");
        }
    }
}
#endif
