using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// =====================================================================================
//  Behavior Designer tasks for the traffic cars.
//  They appear in the Tasks panel under "Traffic".
//
//  Written exactly like the examples in the Behavior Designer documentation:
//   - Conditional / Action are derived directly (no extra base classes)
//   - OnAwake caches the component, OnStart resets, OnUpdate returns the TaskStatus
//   - tunable numbers are SharedFloat so they can be linked to a Variable
// =====================================================================================

// =====================================================================================
//  CONDITIONALS   (Success = "yes, I must stop for this reason")
// =====================================================================================

[TaskCategory("Traffic")]
[TaskDescription("Success if a car driving the same way is right in front of this car. Distance is measured from the car's center (about half a car length + the gap you want).")]
public class IsCarAhead : Conditional
{
    public SharedFloat followDistance = 5f;

    private TrafficCar car;

    public override void OnAwake()
    {
        car = transform.GetComponent<TrafficCar>();
    }

    public override TaskStatus OnUpdate()
    {
        return car.IsCarAhead(followDistance.Value) ? TaskStatus.Success : TaskStatus.Failure;
    }
}

[TaskCategory("Traffic")]
[TaskDescription("Success if the car is about to reach its stop line and the light for its road is red (or all-red).")]
public class IsRedAtStopLine : Conditional
{
    public SharedFloat brakeMargin = 0.5f;

    private TrafficCar car;

    public override void OnAwake()
    {
        car = transform.GetComponent<TrafficCar>();
    }

    public override TaskStatus OnUpdate()
    {
        bool inStopZone = car.IsInStopZone(brakeMargin.Value);
        bool isRed = !car.Intersection.IsGreen(car.Approach);

        return (inStopZone && isRed) ? TaskStatus.Success : TaskStatus.Failure;
    }
}

[TaskCategory("Traffic")]
[TaskDescription("Success if this car turns Left and an opposing car going Straight or Right is coming. Only checked before the stop line (after that the car is committed).")]
public class MustYieldToOncoming : Conditional
{
    public SharedFloat brakeMargin = 0.5f;

    private TrafficCar car;

    public override void OnAwake()
    {
        car = transform.GetComponent<TrafficCar>();
    }

    public override TaskStatus OnUpdate()
    {
        // Straight and Right never yield.
        if (car.Intent != Intent.Left) return TaskStatus.Failure;

        // Already committed (past the line) or still far away.
        if (!car.IsInStopZone(brakeMargin.Value)) return TaskStatus.Failure;

        return car.Intersection.HasOncomingConflict(car) ? TaskStatus.Success : TaskStatus.Failure;
    }
}

[TaskCategory("Traffic")]
[TaskDescription("Success if this left-turner is inside the claim radius and ANOTHER car holds the center lock.")]
public class IsCenterBlocked : Conditional
{
    private TrafficCar car;

    public override void OnAwake()
    {
        car = transform.GetComponent<TrafficCar>();
    }

    public override TaskStatus OnUpdate()
    {
        if (car.Intent != Intent.Left) return TaskStatus.Failure;
        if (car.HoldsCenterLock) return TaskStatus.Failure;
        if (car.DistanceToCenter > car.Intersection.claimRadius) return TaskStatus.Failure;

        return car.Intersection.IsCenterLockedByOther(car) ? TaskStatus.Success : TaskStatus.Failure;
    }
}

// =====================================================================================
//  ACTIONS
// =====================================================================================

[TaskCategory("Traffic")]
[TaskDescription("Brakes the car and keeps it stopped for as long as this task is running.")]
public class StopAgent : Action
{
    private TrafficCar car;

    public override void OnAwake()
    {
        car = transform.GetComponent<TrafficCar>();
    }

    public override void OnStart()
    {
        car.Stop();
    }

    public override TaskStatus OnUpdate()
    {
        return TaskStatus.Running; // stays running until a conditional abort removes it
    }
}

[TaskCategory("Traffic")]
[TaskDescription("Drives along the NavMesh route waypoint by waypoint. Returns Success at the end of the road.")]
public class DriveRoute : Action
{
    public SharedFloat waypointTolerance = 1.5f;

    private TrafficCar car;

    public override void OnAwake()
    {
        car = transform.GetComponent<TrafficCar>();
    }

    public override void OnStart()
    {
        car.Resume();               // undo any earlier StopAgent
        car.GoToCurrentWaypoint();
    }

    public override TaskStatus OnUpdate()
    {
        if (!car.HasReachedCurrentWaypoint(waypointTolerance.Value))
            return TaskStatus.Running;

        if (!car.AdvanceWaypoint())
            return TaskStatus.Success;  // reached the end of the road

        car.GoToCurrentWaypoint();
        return TaskStatus.Running;
    }
}

[TaskCategory("Traffic")]
[TaskDescription("Left-turners only. Claims the center lock when entering the claim radius, releases it after leaving the release radius. Put it in a Parallel next to DriveRoute.")]
public class ManageCenterLock : Action
{
    private TrafficCar car;

    public override void OnAwake()
    {
        car = transform.GetComponent<TrafficCar>();
    }

    public override TaskStatus OnUpdate()
    {
        // Straight / Right cars never use the lock, and a finished left-turner is done for good.
        if (car.Intent != Intent.Left || car.CenterLockFinished)
            return TaskStatus.Success;

        IntersectionManager manager = car.Intersection;
        float distance = car.DistanceToCenter;

        if (!car.HoldsCenterLock)
        {
            // Every car calls this. The first one in range wins; the other one is stopped by IsCenterBlocked.
            if (distance <= manager.claimRadius) manager.TryClaimCenter(car);
        }
        else if (distance >= manager.releaseRadius)
        {
            manager.ReleaseCenter(car);
            car.CenterLockFinished = true;
            return TaskStatus.Success;
        }

        return TaskStatus.Running;
    }
}

[TaskCategory("Traffic")]
[TaskDescription("Destroys the car (it reached the end of the road).")]
public class DestroyCar : Action
{
    public override TaskStatus OnUpdate()
    {
        UnityEngine.Object.Destroy(transform.gameObject);
        return TaskStatus.Success;
    }
}
