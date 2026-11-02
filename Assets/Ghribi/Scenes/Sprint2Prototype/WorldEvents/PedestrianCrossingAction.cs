using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace LittleDriver.WorldEvents
{
    [TaskCategory("Little Driver/World Events")]
    public sealed class PedestrianCrossingAction : Action
    {
        public const float StartingPriority = 0.6f;
        public const float MaximumBonus = 0.4f;

        // Called only after the manager checks that this action is allowed.
        // urgency means crossing closeness (0 = detection edge, 1 = at crossing).
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
        public override float GetUtility() => world != null ? world.CrossingUtility : 0;

        public override void OnStart()
        {
            completed = false;
            started = world != null && world.BeginEvent(WorldEventKind.Crossing);
        }

        public override TaskStatus OnUpdate()
        {
            if (!started) return TaskStatus.Failure;
            completed = world.EventComplete(WorldEventKind.Crossing);
            if (completed) world.EndEvent(WorldEventKind.Crossing, true);
            return completed ? TaskStatus.Success : TaskStatus.Running;
        }

        public override void OnEnd()
        {
            if (started && world != null) world.EndEvent(WorldEventKind.Crossing, completed);
            started = false;
        }
    }
}
