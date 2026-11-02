using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace LittleDriver.WorldEvents
{
    [TaskCategory("Little Driver/World Events")]
    public sealed class PoliceWarningAction : Action
    {
        public const float StartingPriority = 0.6f;
        public const float MaximumBonus = 0.4f;

        // Called only after the manager checks that this action is allowed.
        // urgency means speed urgency (0 = barely speeding, 1 = maximum speeding).
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
        public override float GetUtility() => world != null ? world.PoliceUtility : 0;

        public override void OnStart()
        {
            completed = false;
            started = world != null && world.BeginEvent(WorldEventKind.Police);
        }

        public override TaskStatus OnUpdate()
        {
            if (!started) return TaskStatus.Failure;
            completed = world.EventComplete(WorldEventKind.Police);
            if (completed) world.EndEvent(WorldEventKind.Police, true);
            return completed ? TaskStatus.Success : TaskStatus.Running;
        }

        public override void OnEnd()
        {
            if (started && world != null) world.EndEvent(WorldEventKind.Police, completed);
            started = false;
        }
    }
}
