using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The ONE central brain of an intersection:
///  - runs the light phases (A: North/South green, B: East/West green)
///  - owns the center lock used by opposing left-turners
///  - answers "is there oncoming traffic?" for left-turners
/// Cars never talk to each other, they only ask this manager.
/// </summary>
public class IntersectionManager : MonoBehaviour
{
    [System.Serializable]
    public class ApproachPoints
    {
        [Tooltip("Its blue (Z) arrow must point TOWARD the intersection center.")]
        public Transform stopLine;

        [Tooltip("End of the road leaving the intersection. Cars are destroyed when they reach it.")]
        public Transform exitPoint;
    }

    [Header("Layout  (index 0 = North, 1 = East, 2 = South, 3 = West)")]
    public Transform center;
    public ApproachPoints[] approaches = new ApproachPoints[4];

    [Header("Phases")]
    public float greenTime = 10f;
    [Tooltip("Both directions red between phases so cars can clear the intersection. Set 0 to disable.")]
    public float allRedTime = 2f;

    [Header("Rules (in scene units, measured from the center)")]
    [Tooltip("A left-turner tries to claim the center lock inside this radius. Must be bigger than the braking distance + half a car.")]
    public float claimRadius = 8f;
    [Tooltip("The lock is released once the car is farther than this. Must be > claimRadius and < distance to the exit points.")]
    public float releaseRadius = 10f;
    [Tooltip("Oncoming Straight/Right cars closer than this to the center make a left-turner yield.")]
    public float oncomingDetectRadius = 20f;

    public Phase CurrentPhase { get; private set; }
    public bool IsAllRed { get; private set; }
    public bool IsCenterOccupied { get { return centerHolder != null; } }
    public int CarCount { get { return cars.Count; } }

    /// <summary>Fired on every change. Use it to drive your 3D traffic light models.</summary>
    public event System.Action<Phase, bool> PhaseChanged; // (phase, isAllRed)

    private readonly List<TrafficCar> cars = new List<TrafficCar>();
    private TrafficCar centerHolder;

    private void OnValidate()
    {
        if (approaches == null || approaches.Length != 4)
            System.Array.Resize(ref approaches, 4);
    }

    private void Start()
    {
        StartCoroutine(PhaseLoop());
    }

    // ------------------------------------------------------------------ phases

    private IEnumerator PhaseLoop()
    {
        Phase phase = Phase.A;

        while (true)
        {
            SetPhase(phase, false);
            yield return new WaitForSeconds(greenTime);

            if (allRedTime > 0f)
            {
                SetPhase(phase, true);
                yield return new WaitForSeconds(allRedTime);
            }

            phase = (phase == Phase.A) ? Phase.B : Phase.A;
        }
    }

    private void SetPhase(Phase phase, bool allRed)
    {
        CurrentPhase = phase;
        IsAllRed = allRed;

        if (PhaseChanged != null) PhaseChanged(phase, allRed);
        Debug.Log(allRed ? "All red" : "Phase " + phase + " green");
    }

    /// <summary>Is the light green for cars entering from this side?</summary>
    public bool IsGreen(Direction approach)
    {
        if (IsAllRed) return false;
        return (CurrentPhase == Phase.A) == approach.IsNorthSouth();
    }

    // ------------------------------------------------------------------ center lock

    /// <summary>First caller wins. Returns true if this car now holds (or already held) the lock.</summary>
    public bool TryClaimCenter(TrafficCar car)
    {
        if (centerHolder == null || centerHolder == car)
        {
            centerHolder = car;
            return true;
        }
        return false;
    }

    public void ReleaseCenter(TrafficCar car)
    {
        if (centerHolder == car) centerHolder = null;
    }

    public bool IsCenterHeldBy(TrafficCar car)
    {
        return centerHolder == car;
    }

    public bool IsCenterLockedByOther(TrafficCar car)
    {
        return centerHolder != null && centerHolder != car;
    }

    // ------------------------------------------------------------------ yielding

    /// <summary>
    /// True if a left-turning car must wait: an opposing car going Straight or Right
    /// is close to the center and has not passed it yet.
    /// (Opposing Left-turners are NOT counted here, the center lock handles them.)
    /// </summary>
    public bool HasOncomingConflict(TrafficCar car)
    {
        Direction opposite = car.Approach.Opposite();

        foreach (TrafficCar other in cars)
        {
            if (other == car) continue;
            if (other.Approach != opposite) continue;
            if (other.Intent == Intent.Left) continue;
            if (other.HasPassedCenter) continue;
            if (other.DistanceToCenter > oncomingDetectRadius) continue;

            return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ routing

    public Transform GetStopLine(Direction from)
    {
        return approaches[(int)from].stopLine;
    }

    /// <summary>
    /// Waypoints the car feeds to the NavMeshAgent, in order.
    /// Left turns go through the center point (that is where the lock matters).
    /// </summary>
    public Transform[] BuildRoute(Direction from, Intent intent)
    {
        Transform exit = approaches[(int)from.ExitFor(intent)].exitPoint;

        if (intent == Intent.Left) return new[] { center, exit };
        return new[] { exit };
    }

    // ------------------------------------------------------------------ car registry

    public void Register(TrafficCar car)
    {
        if (!cars.Contains(car)) cars.Add(car);
    }

    public void Unregister(TrafficCar car)
    {
        cars.Remove(car);
        ReleaseCenter(car); // a destroyed car must never keep the lock
    }

    // ------------------------------------------------------------------ gizmos

    private void OnDrawGizmosSelected()
    {
        if (center != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(center.position, claimRadius);
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(center.position, releaseRadius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(center.position, oncomingDetectRadius);
        }

        if (approaches == null) return;
        foreach (ApproachPoints a in approaches)
        {
            if (a == null) continue;

            if (a.stopLine != null)
            {
                Gizmos.color = Color.red;
                Vector3 p = a.stopLine.position;
                Gizmos.DrawLine(p - a.stopLine.right * 4f, p + a.stopLine.right * 4f);
                Gizmos.DrawRay(p, a.stopLine.forward * 3f);
            }
            if (a.exitPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(a.exitPoint.position, 1f);
            }
        }
    }
}
