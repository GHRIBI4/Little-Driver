using BehaviorDesigner.Runtime.Tasks;

namespace LittleDriver.WorldEvents
{
    [TaskCategory("Little Driver/World Events")]
    public sealed class WaitForWorldEvent : Action
    {
        public override float GetUtility() => 0.001f;
        public override TaskStatus OnUpdate() => TaskStatus.Running;
    }
}
