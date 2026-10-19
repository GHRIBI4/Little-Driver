using BehaviorDesigner.Runtime;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class TrafficCar : MonoBehaviour
{
    [Tooltip("How far the car's FRONT stops before the stop line on a red light.")]
    public float stopMargin = 0.5f;

    [Header("Left-Turn Waiting Pocket (Solution 2)")]
    [Tooltip("How far past the stop line a left-turner pulls forward while yielding on green.")]
    public float pocketForwardOffset = 2.2f;
    [Tooltip("How far to the inner-left side a left-turner shifts so Straight/Right cars can pass. Never less than car width + 0.4 (enforced in Init).")]
    public float pocketLeftOffset = 2.4f;

    public IntersectionManager Intersection { get; private set; }
    public Direction Approach { get; private set; }
    public Intent Intent { get; private set; }
    public Direction ExitDirection { get; private set; }

    public bool CenterLockFinished { get; set; }
    // True once the front bumper is past the line. Computed from geometry so other cars see it
    // even if the behavior tree is not re-evaluating MustWaitAtStopLine.
    public bool HasCrossedStopLine => hasCrossedStopLine || (stopLine != null && FrontDistanceToLine <= 0.1f);
    public bool IsYieldingInPocket => inWaitingPocket;
    public bool IsForcingUnstuck => forceUnstuckTimer > 0f;

    private static readonly Collider[] OverlapBuffer = new Collider[32];

    private NavMeshAgent agent;
    private Transform stopLine;
    private Vector3 stopLineForward;
    private Vector3 stopLineRight;

    private Vector3 stopHoldPoint;
    private Vector3 snappedStopHoldPoint;
    private Vector3 pocketHoldPoint;
    private Vector3 snappedPocketHoldPoint;

    private Vector3[] route;
    private int routeIndex;

    private float halfLength = 2f;
    private float halfWidth = 1f;
    private int baseAvoidancePriority;

    private bool holding;
    private bool inWaitingPocket;
    private bool hasCrossedStopLine;

    private float stuckTimer;
    private float forceUnstuckTimer;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.autoBraking = false;
        baseAvoidancePriority = agent.avoidancePriority;

        BoxCollider box = GetComponentInChildren<BoxCollider>();
        if (box != null)
        {
            Vector3 s = box.transform.lossyScale;
            halfLength = box.size.z * 0.5f * Mathf.Abs(s.z);
            halfWidth = box.size.x * 0.5f * Mathf.Abs(s.x);
        }
    }

    public void Init(IntersectionManager intersection, Direction approach, Intent intent)
    {
        Intersection = intersection;
        Approach = approach;
        Intent = intent;
        ExitDirection = approach.ExitFor(intent);

        stopLine = intersection.GetStopLine(approach);
        stopLineForward = Flat(stopLine.forward).normalized;
        stopLineRight = Flat(stopLine.right).normalized;

        // Standard stop position behind the line (Red / Yellow light)
        stopHoldPoint = stopLine.position - stopLineForward * (halfLength + stopMargin);
        snappedStopHoldPoint = Snap(stopHoldPoint);

        // Waiting pocket position past the line and shifted left (Green light + oncoming traffic)
        float leftOffset = Mathf.Max(pocketLeftOffset, halfWidth * 2f + 0.4f);
        pocketHoldPoint = stopLine.position
                        + stopLineForward * pocketForwardOffset
                        - stopLineRight * leftOffset;
        snappedPocketHoldPoint = Snap(pocketHoldPoint);

        route = intersection.BuildRoute(approach, intent);
        routeIndex = 0;

        intersection.Register(this);

        BehaviorTree tree = GetComponent<BehaviorTree>();
        if (tree != null) tree.EnableBehavior();
    }

    private void Update()
    {
        if (Intersection == null) return;

        if (forceUnstuckTimer > 0f)
        {
            forceUnstuckTimer -= Time.deltaTime;
        }

        // Watchdog only applies to cars that have fully committed past the stop line/pocket.
        if (hasCrossedStopLine && !inWaitingPocket && !IsMoving)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= 1.5f)
            {
                Debug.LogWarning($"{name}: stuck inside the intersection for 1.5s, forcing it through (from {Approach}, intent {Intent}). Collision checks are off for 1.5s.", this);
                forceUnstuckTimer = 1.5f;
                stuckTimer = 0f;
                Resume();
                GoToCurrentWaypoint();
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    private void OnDestroy()
    {
        if (Intersection != null) Intersection.Unregister(this);
    }

    // ------------------------------------------------------------------ questions

    public bool IsMoving => agent.velocity.sqrMagnitude > 0.2f;

    public float BrakingDistance
    {
        get
        {
            float vSq = agent.velocity.sqrMagnitude;
            return vSq / (2f * Mathf.Max(1f, agent.acceleration));
        }
    }

    public Vector3 TravelDirection
    {
        get
        {
            Vector3 v = Flat(agent.velocity);
            return v.sqrMagnitude > 0.05f ? v.normalized : Flat(transform.forward).normalized;
        }
    }

    public float DistanceToStopLine => Vector3.Dot(stopLine.position - transform.position, stopLineForward);

    public float FrontDistanceToLine => DistanceToStopLine - halfLength;

    public float DistanceToCenter => Vector3.Distance(Flat(transform.position), Flat(Intersection.center.position));

    public bool HasPassedCenter => Vector3.Dot(Intersection.center.position - transform.position, stopLineForward) < -1f;

    public bool HoldsCenterLock => Intersection.IsCenterHeldBy(this);

    public bool MustWaitAtStopLine()
    {
        if (hasCrossedStopLine) return false;

        bool isGreen = Intersection.IsGreen(Approach);
        bool oncomingConflict = Intent == Intent.Left && Intersection.HasOncomingConflict(this);

        // 1. Already inside the Left-Turn Waiting Pocket:
        // Wait only as long as oncoming traffic still has conflict.
        // Once oncoming traffic clears (or light turns yellow/red stopping oncoming queues), go immediately.
        if (inWaitingPocket)
        {
            if (oncomingConflict)
            {
                holding = true;
                return true;
            }

            inWaitingPocket = false;
            holding = false;
            hasCrossedStopLine = true;
            agent.avoidancePriority = baseAvoidancePriority;
            return false;
        }

        float frontDist = FrontDistanceToLine;

        // Crossed the stop line without entering a pocket -> committed.
        if (frontDist <= (holding ? -0.5f : 0.1f))
        {
            holding = false;
            hasCrossedStopLine = true;
            return false;
        }

        // 2. Green light, but this Left-turner has oncoming traffic -> pull into the Waiting Pocket.
        if (isGreen && oncomingConflict)
        {
            inWaitingPocket = true;
            holding = true;
            // Make stationary/yielding pocket car higher priority (lower number) so passing agents steer around it cleanly
            agent.avoidancePriority = 20;
            return true;
        }

        // 3. Green light with no conflict -> go!
        if (isGreen)
        {
            holding = false;
            return false;
        }

        // 4. Red or Yellow light -> hold behind the stop line if able to brake in time.
        if (!holding && (!IsMoving || frontDist >= BrakingDistance + 0.1f))
            holding = true;

        if (!holding)
            hasCrossedStopLine = true;

        return holding;
    }

    public bool IsCarAhead(float gap)
    {
        if (forceUnstuckTimer > 0f) return false;

        Vector3 dir = TravelDirection;
        float look = gap + BrakingDistance;

        Vector3 center = transform.position + dir * (halfLength + look * 0.5f);
        Vector3 halfExtents = new Vector3(halfWidth * 0.85f, 0.5f, look * 0.5f);
        Quaternion rotation = Quaternion.LookRotation(dir, Vector3.up);

        int hits = Physics.OverlapBoxNonAlloc(center, halfExtents, OverlapBuffer, rotation, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
        {
            TrafficCar other = OverlapBuffer[i].GetComponentInParent<TrafficCar>();
            if (other == null || other == this) continue;

            // SOLUTION 2 BYPASS: Straight and Right cars ignore Left-turners yielding in the waiting pocket.
            if (other.Approach == Approach && other.IsYieldingInPocket && Intent != Intent.Left)
                continue;

            // Ignore cars waiting behind another road's stop line.
            if (other.Approach != Approach && !other.HasCrossedStopLine && !other.IsYieldingInPocket)
                continue;

            Vector3 toOther = Flat(other.transform.position - transform.position);
            float dist = toOther.magnitude;
            if (dist < 0.01f) continue;

            Vector3 toOtherDir = toOther / dist;
            if (Vector3.Dot(toOtherDir, dir) <= 0.25f) continue;

            // Ignore oncoming cars in opposite lanes unless turning into the exact same exit.
            float headingAlignment = Vector3.Dot(dir, other.TravelDirection);
            if (headingAlignment < -0.25f && other.ExitDirection != ExitDirection) continue;

            if (HoldsCenterLock && !other.HoldsCenterLock && other.ExitDirection != ExitDirection) continue;

            // Tie-break only for cars that are really coming toward each other.
            // A stopped car (or a car driving the same way) ahead of us is NEVER ignored.
            bool mutual = other.IsMoving && Vector3.Dot(-toOtherDir, other.TravelDirection) > 0.1f;
            if (mutual)
            {
                if (HoldsCenterLock && !other.HoldsCenterLock) continue;
                if (!other.HoldsCenterLock && GetInstanceID() < other.GetInstanceID()) continue;
            }

            return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ movement (NavMesh)

    public void Stop()
    {
        if (HoldsCenterLock && !HasPassedCenter && !hasCrossedStopLine)
        {
            Intersection.ReleaseCenter(this);
        }

        agent.isStopped = true;
    }

    public void BeginHoldAtStopLine()
    {
        ApplyHoldDestination();
    }

    public void UpdateHoldAtStopLine()
    {
        ApplyHoldDestination();
    }

    private void ApplyHoldDestination()
    {
        agent.autoBraking = true;

        Vector3 targetPoint = inWaitingPocket ? pocketHoldPoint : stopHoldPoint;
        Vector3 snappedTarget = inWaitingPocket ? snappedPocketHoldPoint : snappedStopHoldPoint;

        // Stop once the car reaches (or is slightly past) the active hold target.
        if (Vector3.Dot(targetPoint - transform.position, stopLineForward) < 0.25f)
        {
            agent.isStopped = true;
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(snappedTarget);
    }

    public void Resume()
    {
        agent.avoidancePriority = baseAvoidancePriority;
        agent.isStopped = false;
        agent.autoBraking = false;
    }

    public void GoToCurrentWaypoint()
    {
        if (route == null || route.Length == 0) return;
        routeIndex = Mathf.Clamp(routeIndex, 0, route.Length - 1);
        agent.SetDestination(route[routeIndex]);
    }

    public bool HasReachedCurrentWaypoint(float tolerance)
    {
        Vector3 d = Flat(transform.position) - Flat(route[routeIndex]);
        return d.sqrMagnitude <= tolerance * tolerance;
    }

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

    private static Vector3 Snap(Vector3 p)
    {
        return NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : p;
    }
}