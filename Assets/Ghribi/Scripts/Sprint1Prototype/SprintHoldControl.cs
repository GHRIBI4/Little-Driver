using UnityEngine;
using UnityEngine.EventSystems;

namespace LittleDriver.Prototype
{
    public sealed class SprintHoldControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private SprintPrototypeCar car;
        [SerializeField] private bool isBrake;
        [SerializeField] private bool isHorn;
        private int? pointerId;

        public void OnPointerDown(PointerEventData data)
        {
            if (pointerId.HasValue) return;
            pointerId = data.pointerId;
            SetHeld(true);
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (pointerId != data.pointerId) return;
            pointerId = null;
            SetHeld(false);
        }

        private void SetHeld(bool held)
        {
            if (car == null) return;
            if (isHorn) car.SetHornHeld(held);
            else if (isBrake) car.SetBrakeHeld(held);
            else car.SetGoHeld(held);
        }

        private void OnDisable()
        {
            pointerId = null;
            SetHeld(false);
        }
    }
}
