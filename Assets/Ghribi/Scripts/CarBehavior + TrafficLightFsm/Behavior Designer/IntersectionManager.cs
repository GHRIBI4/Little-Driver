using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

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

    [Header("Traffic lights")]
    [Tooltip("The TrafficLightFSM object. It is the only source of the light state.")]
    public TrafficLightFSM lights;

    [Header("Layout  (index 0 = North, 1 = East, 2 = South, 3 = West)")]
    public Transform center;
    public ApproachPoints[] approaches = new ApproachPoints[4];

    [Header("Rules (in scene units, measured from the center)")]
    public float claimRadius = 8f;
    public float releaseRadius = 10f;
    public float oncomingDetectRadius = 28f;

    [Header("Turns")]
    public float turnTightness = 6f;
    public int curveSamples = 6;

    public bool IsCenterOccupied => centerHolder != null;
    public int CarCount => cars.Count;

    private readonly List<TrafficCar> cars = new List<TrafficCar>(32);
    private TrafficCar centerHolder;

    private void OnValidate()
    {
        if (approaches == null || approaches.Length != 4)
            System.Array.Resize(ref approaches, 4);
    }

    // ------------------------------------------------------------------ lights

    public LightColor GetLight(Direction approach)
    {
        return lights != null ? lights.GetLight(approach) : LightColor.Red;
    }

    public bool IsGreen(Direction approach)
    {
        return GetLight(approach) == LightColor.Green;
    }

    // ------------------------------------------------------------------ center lock

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

    public bool HasOncomingConflict(TrafficCar car)
    {
        Direction opposite = car.Approach.Opposite();
        bool oppositeGreen = IsGreen(opposite);

        for (int i = 0; i < cars.Count; i++)
        {
            TrafficCar other = cars[i];
            if (other == car) continue;
            if (other.Approach != opposite) continue;
            if (other.Intent == Intent.Left) continue;
            if (other.HasPassedCenter) continue;
            if (other.DistanceToCenter > oncomingDetectRadius) continue;

            // If the opposite light is no longer Green, cars still behind the opposite stop line must stop,
            // so they do not block a left-turner clearing the waiting pocket.
            if (!oppositeGreen && !other.HasCrossedStopLine) continue;

            if (!other.IsMoving && !IsQueueLeader(other)) continue;

            return true;
        }
        return false;
    }

    private bool IsQueueLeader(TrafficCar car)
    {
        for (int i = 0; i < cars.Count; i++)
        {
            TrafficCar other = cars[i];
            if (other == car || other.Approach != car.Approach) continue;
            if (other.IsYieldingInPocket) continue;
            if (other.FrontDistanceToLine <= 0.1f) continue;
            if (other.DistanceToStopLine < car.DistanceToStopLine) return false;
        }
        return true;
    }

    // ------------------------------------------------------------------ routing

    public Transform GetStopLine(Direction from)
    {
        return approaches[(int)from].stopLine;
    }

    public Vector3[] BuildRoute(Direction from, Intent intent)
    {
        Direction to = from.ExitFor(intent);
        Vector3 exitPos = approaches[(int)to].exitPoint.position;
        List<Vector3> points = new List<Vector3>(curveSamples + 2);

        if (intent != Intent.Straight)
        {
            Vector3 inPoint = approaches[(int)from].stopLine.position;
            Vector3 inDir = Flat(approaches[(int)from].stopLine.forward).normalized;
            Vector3 outDir = -Flat(approaches[(int)to].stopLine.forward).normalized;

            float cross = Cross2(inDir, outDir);
            if (Mathf.Abs(cross) > 0.01f)
            {
                float t = Cross2(exitPos - inPoint, outDir) / cross;
                Vector3 corner = inPoint + inDir * t;
                float r = Mathf.Max(1f, Mathf.Min(turnTightness, t - 1f));

                Vector3 start = corner - inDir * r;
                Vector3 end = corner + outDir * r;

                points.Add(start);
                int n = Mathf.Max(2, curveSamples);
                for (int i = 1; i <= n; i++)
                {
                    float u = i / (float)n;
                    points.Add((1f - u) * (1f - u) * start + 2f * (1f - u) * u * corner + u * u * end);
                }
            }
        }

        points.Add(exitPos);

        for (int i = 0; i < points.Count; i++) points[i] = Snap(points[i]);
        return points.ToArray();
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    private static float Cross2(Vector3 a, Vector3 b)
    {
        return a.x * b.z - a.z * b.x;
    }

    private static Vector3 Snap(Vector3 p)
    {
        return NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : p;
    }

    // ------------------------------------------------------------------ car registry

    public void Register(TrafficCar car)
    {
        if (!cars.Contains(car)) cars.Add(car);
    }

    public void Unregister(TrafficCar car)
    {
        cars.Remove(car);
        ReleaseCenter(car);
    }

    // ------------------------------------------------------------------ gizmos

    private void OnDrawGizmos()
    {
        if (approaches == null) return;

        for (int i = 0; i < approaches.Length; i++)
        {
            ApproachPoints a = approaches[i];
            if (a == null || a.stopLine == null) continue;

            Gizmos.color = Color.red;
            if (Application.isPlaying)
            {
                LightColor c = GetLight((Direction)i);
                Gizmos.color = c == LightColor.Green ? Color.green : (c == LightColor.Yellow ? Color.yellow : Color.red);
            }

            Vector3 p = a.stopLine.position;
            Gizmos.DrawLine(p - a.stopLine.right * 4f, p + a.stopLine.right * 4f);
            Gizmos.DrawRay(p, a.stopLine.forward * 3f);
        }
    }

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
        for (int i = 0; i < approaches.Length; i++)
        {
            ApproachPoints a = approaches[i];
            if (a != null && a.exitPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(a.exitPoint.position, 1f);
            }
        }
    }
}