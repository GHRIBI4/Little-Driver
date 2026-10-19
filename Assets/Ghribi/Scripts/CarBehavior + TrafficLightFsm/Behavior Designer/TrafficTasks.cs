using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

[TaskCategory("Traffic")]
[TaskDescription("Success if another car is too close ahead.")]
public class IsCarAhead : Conditional
{
    public SharedFloat gap = 2f;
    private TrafficCar car;

    public override void OnAwake()
    {
        car = GetComponent<TrafficCar>();
    }

    public override TaskStatus OnUpdate()
    {
        return car.IsCarAhead(gap.Value) ? TaskStatus.Success : TaskStatus.Failure;
    }
}

[TaskCategory("Traffic")]
[TaskDescription("Success while the car must stay behind its stop line.")]
public class MustWaitAtStopLine : Conditional
{
    private TrafficCar car;

    public override void OnAwake()
    {
        car = GetComponent<TrafficCar>();
    }

    public override TaskStatus OnUpdate()
    {
        return car.MustWaitAtStopLine() ? TaskStatus.Success : TaskStatus.Failure;
    }
}

[TaskCategory("Traffic")]
[TaskDescription("Success if this left-turner is inside the claim radius and another car holds the center lock.")]
public class IsCenterBlocked : Conditional
{
    private TrafficCar car;

    public override void OnAwake()
    {
        car = GetComponent<TrafficCar>();
    }

    public override TaskStatus OnUpdate()
    {
        if (car.IsForcingUnstuck) return TaskStatus.Failure;
        if (car.Intent != Intent.Left || car.CenterLockFinished) return TaskStatus.Failure;
        if (car.HoldsCenterLock || car.HasPassedCenter) return TaskStatus.Failure;
        if (car.DistanceToCenter > car.Intersection.claimRadius) return TaskStatus.Failure;

        return car.Intersection.IsCenterLockedByOther(car) ? TaskStatus.Success : TaskStatus.Failure;
    }
}

[TaskCategory("Traffic")]
[TaskDescription("Brakes and keeps the car stopped.")]
public class StopAgent : Action
{
    private TrafficCar car;

    public override void OnAwake()
    {
        car = GetComponent<TrafficCar>();
    }

    public override void OnStart()
    {
        car.Stop();
    }

    public override TaskStatus OnUpdate()
    {
        return TaskStatus.Running;
    }
}

[TaskCategory("Traffic")]
[TaskDescription("Drives to the stop line and waits.")]
public class HoldAtStopLine : Action
{
    private TrafficCar car;

    public override void OnAwake()
    {
        car = GetComponent<TrafficCar>();
    }

    public override void OnStart()
    {
        car.BeginHoldAtStopLine();
    }

    public override TaskStatus OnUpdate()
    {
        car.UpdateHoldAtStopLine();
        return TaskStatus.Running;
    }
}

[TaskCategory("Traffic")]
[TaskDescription("Drives along the NavMesh route waypoint by waypoint.")]
public class DriveRoute : Action
{
    public SharedFloat waypointTolerance = 1f;
    private TrafficCar car;

    public override void OnAwake()
    {
        car = GetComponent<TrafficCar>();
    }

    public override void OnStart()
    {
        car.Resume();
        car.GoToCurrentWaypoint();
    }

    public override TaskStatus OnUpdate()
    {
        if (!car.HasReachedCurrentWaypoint(waypointTolerance.Value))
            return TaskStatus.Running;

        if (!car.AdvanceWaypoint())
            return TaskStatus.Success;

        car.GoToCurrentWaypoint();
        return TaskStatus.Running;
    }
}

[TaskCategory("Traffic")]
[TaskDescription("Claims and releases the center lock for left-turners.")]
public class ManageCenterLock : Action
{
    private TrafficCar car;

    public override void OnAwake()
    {
        car = GetComponent<TrafficCar>();
    }

    public override TaskStatus OnUpdate()
    {
        if (car.Intent != Intent.Left || car.CenterLockFinished)
            return TaskStatus.Success;

        IntersectionManager manager = car.Intersection;
        float distance = car.DistanceToCenter;

        if (!car.HoldsCenterLock)
        {
            if (distance <= manager.claimRadius) manager.TryClaimCenter(car);
        }
        else if (distance >= manager.releaseRadius || car.HasPassedCenter)
        {
            manager.ReleaseCenter(car);
            car.CenterLockFinished = true;
            return TaskStatus.Success;
        }

        return TaskStatus.Running;
    }
}

[TaskCategory("Traffic")]
[TaskDescription("Destroys the car.")]
public class DestroyCar : Action
{
    public override TaskStatus OnUpdate()
    {
        Object.Destroy(gameObject);
        return TaskStatus.Success;
    }
}