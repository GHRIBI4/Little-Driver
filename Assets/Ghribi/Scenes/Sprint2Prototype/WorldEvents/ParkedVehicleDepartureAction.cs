using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace LittleDriver.WorldEvents
{
    [TaskCategory("Little Driver/World Events")]
    public sealed class ParkedVehicleDepartureAction : Action
    {
        public const float StartingPriority = 0.5f;
        public const float MaximumBonus = 0.5f;

        // Called only after the manager checks that this action is allowed.
        // urgency means vehicle closeness (0 = detection edge, 1 = next to vehicle).
        public static float CalculateUtility(float importance, float urgency)
        {
            float bonus = MaximumBonus * Mathf.Clamp01(urgency);
            float priority = StartingPriority + bonus;
            float utility = importance * priority;
            return utility;
        }

        SprintWorldEventManager world;
        bool started, completed;

        public override void OnAwake() => world = GetComponent<SprintWorldEventManager>();
        public override float GetUtility() => world != null ? world.DepartureUtility : 0;

        public override void OnStart()
        {
            completed = false;
            started = world != null && world.BeginEvent(WorldEventKind.Departure);
        }

        public override TaskStatus OnUpdate()
        {
            if (!started) return TaskStatus.Failure;
            completed = world.EventComplete(WorldEventKind.Departure);
            if (completed) world.EndEvent(WorldEventKind.Departure, true);
            return completed ? TaskStatus.Success : TaskStatus.Running;
        }

        public override void OnEnd()
        {
            if (started && world != null) world.EndEvent(WorldEventKind.Departure, completed);
            started = false;
        }
    }
}
