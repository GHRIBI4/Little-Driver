using TMPro;
using UnityEngine;

namespace LittleDriver.Prototype
{
    public sealed class SprintPrototypeHUD : MonoBehaviour
    {
        [SerializeField] private SprintPrototypeCar car;
        [SerializeField] private GameObject seatBeltPanel;
        [SerializeField] private GameObject drivingControls;
        [SerializeField] private SprintSeatBeltDrag beltDrag;
        [SerializeField] private TMP_Text speedLabel;
        [SerializeField] private TMP_Text beltLabel;
        [SerializeField] private TMP_Text feedbackLabel;
        [SerializeField] private TMP_Text collisionLabel;
        [SerializeField] private TMP_Text seatBeltInstruction;
        private float buckledAt = -1;

        private void OnEnable() => car.Restarted += ResetUI;
        private void OnDisable() => car.Restarted -= ResetUI;

        private void Update()
        {
            if (car.IsBuckled && buckledAt < 0) buckledAt = Time.unscaledTime;
            bool showBelt = !car.IsBuckled || Time.unscaledTime - buckledAt < 0.65f;
            seatBeltPanel.SetActive(showBelt);
            drivingControls.SetActive(!showBelt && !car.IsFinished);
            speedLabel.text = $"{car.Speed * 3.6f:00} km/h  |  {(car.Lane == 0 ? "LEFT" : "RIGHT")} LANE";
            beltLabel.text = car.IsBuckled ? "BELT: BUCKLED" : "BELT: NOT BUCKLED";
            collisionLabel.text = $"CONTACTS: {car.CollisionCount}  |  HORN: {car.HornCount}";
            seatBeltInstruction.text = car.IsBuckled ? "CLICK! Belt buckled. Ready to drive." : "Drag the belt handle into the buckle, then release.";
            if (car.IsFinished) feedbackLabel.text = "COURSE COMPLETE - press RESTART to try again";
            else if (!car.IsBuckled) feedbackLabel.text = "Buckle up before driving";
            else if (Time.unscaledTime < car.CollisionUntil) feedbackLabel.text = $"CONTACT DETECTED: {car.LastContact}";
            else if (car.IsHonking) feedbackLabel.text = "BEEP!";
            else feedbackLabel.text = "Hold GO to drive. Release to slow down. STOP to brake.";
        }

        public void Restart()
        {
            car.Restart();
        }

        private void ResetUI()
        {
            buckledAt = -1;
            seatBeltPanel.SetActive(true);
            beltDrag.ResetBelt();
        }
    }
}
