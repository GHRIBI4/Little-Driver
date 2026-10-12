using BehaviorDesigner.Runtime;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Data + small helpers for one car. It makes NO decisions:
/// the Behavior Designer tree decides, this class just answers questions and moves the NavMeshAgent.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class TrafficCar : MonoBehaviour
{
    public IntersectionManager Intersection { get; private set; }
    public Direction Approach { get; private set; }
    public Intent Intent { get; private set; }

    /// <summary>Set to true once this left-turner has claimed AND released the center lock.</summary>
    public bool CenterLockFinished { get; set; }

    private NavMeshAgent agent;
    private Transform stopLine;
    private Transform[] route;
    private int routeIndex;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.autoBraking = false; // do not slow down at intermediate waypoints
    }

    /// <summary>Called once by the spawner right after Instantiate.</summary>
    public void Init(IntersectionManager intersection, Direction approach, Intent intent)
    {
        Intersection = intersection;
        Approach = approach;
        Intent = intent;

        stopLine = intersection.GetStopLine(approach);
        route = intersection.BuildRoute(approach, intent);
        routeIndex = 0;

        intersection.Register(this);

        // Start the behavior tree only now, so its tasks never run before the car has its data.
        // (On the prefab's Behavior Tree component, untick "Start When Enabled".)
        BehaviorTree tree = GetComponent<BehaviorTree>();
        if (tree != null) tree.EnableBehavior();
    }

    private void OnDestroy()
    {
        if (Intersection != null) Intersection.Unregister(this);
    }

    // ------------------------------------------------------------------ questions

    /// <summary>Positive = stop line is still ahead, negative = already crossed it.</summary>
    public float DistanceToStopLine
    {
        get { return Vector3.Dot(stopLine.position - transform.position, stopLine.forward); }
    }

    public float DistanceToCenter
    {
        get { return Vector3.Distance(Flat(transform.position), Flat(Intersection.center.position)); }
    }

    /// <summary>True once the center is behind the car (measured along the direction it entered).</summary>
    public bool HasPassedCenter
    {
        get { return Vector3.Dot(Intersection.center.position - transform.position, stopLine.forward) < -1f; }
    }

    public bool HoldsCenterLock
    {
        get { return Intersection.IsCenterHeldBy(this); }
    }

    /// <summary>
    /// True while the car is close enough to the stop line that it must start braking
    /// (distance needed to brake from full speed + a small margin), and has not crossed it yet.
    /// </summary>
    public bool IsInStopZone(float margin)
    {
        float brakingDistance = (agent.speed * agent.speed) / (2f * agent.acceleration);
        float d = DistanceToStopLine;
        return d > -0.5f && d <= brakingDistance + margin;
    }

    /// <summary>Is a car driving the same way directly in front of us?</summary>
    public bool IsCarAhead(float distance)
    {
        // Ray starts inside the car's own body (so it ignores its own collider) and above the ground.
        Vector3 origin = transform.position + Vector3.up * 0.3f;

        RaycastHit hit;
        if (!Physics.Raycast(origin, transform.forward, out hit, distance, ~0, QueryTriggerInteraction.Ignore))
            return false;

        TrafficCar other = hit.collider.GetComponentInParent<TrafficCar>();
        if (other == null || other == this) return false;

        // only cars going the same way count (crossing cars are handled by the traffic rules)
        return Vector3.Dot(other.transform.forward, transform.forward) > 0.5f;
    }

    // ------------------------------------------------------------------ movement (NavMesh)

    public void Stop()   { agent.isStopped = true; }
    public void Resume() { agent.isStopped = false; }

    public void GoToCurrentWaypoint()
    {
        agent.SetDestination(route[routeIndex].position);
    }

    public bool HasReachedCurrentWaypoint(float tolerance)
    {
        return Vector3.Distance(Flat(transform.position), Flat(route[routeIndex].position)) <= tolerance;
    }

    /// <summary>Moves to the next waypoint. Returns false when the route is finished.</summary>
    public bool AdvanceWaypoint()
    {
        routeIndex++;
        return routeIndex < route.Length;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}
