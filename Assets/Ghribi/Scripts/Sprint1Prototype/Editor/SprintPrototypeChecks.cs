using System;
using System.Threading.Tasks;
using LittleDriver.Prototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public static class SprintPrototypeChecks
{
    [MenuItem("Little Driver/Check Sprint 1 (while playing)")]
    public static async void RunFromMenu()
    {
        try { Debug.Log(await Run()); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    // Runs the real UI handlers and physics loop in the prototype scene, then resets it.
    public static async Task<string> Run()
    {
        Require(EditorApplication.isPlaying, "Enter Play mode first.");
        var car = UnityEngine.Object.FindFirstObjectByType<SprintPrototypeCar>();
        var hud = UnityEngine.Object.FindFirstObjectByType<SprintPrototypeHUD>();
        Require(car != null && hud != null && EventSystem.current != null, "Open Sprint1Prototype first.");
        var body = car.GetComponent<Rigidbody>();
        try
        {
            hud.Restart();
            await Task.Delay(150);
            Vector3 start = body.position;
            car.SetGoHeld(true);
            await Task.Delay(350);
            Require(!car.IsBuckled && Vector3.Distance(body.position, start) < 0.02f, "Unbuckled car moved.");
            car.ReleaseControls();

            var drag = UnityEngine.Object.FindFirstObjectByType<SprintSeatBeltDrag>();
            var handle = (RectTransform)drag.transform;
            var target = (RectTransform)handle.parent.Find("Buckle Target");
            Vector2 home = handle.anchoredPosition;
            Drag(drag, RectTransformUtility.WorldToScreenPoint(null, handle.position) + new Vector2(30, 20));
            Require(!car.IsBuckled && Vector2.Distance(handle.anchoredPosition, home) < 0.1f, "Missed belt drop did not reset.");
            Drag(drag, RectTransformUtility.WorldToScreenPoint(null, target.position));
            Require(car.IsBuckled && Vector2.Distance(handle.anchoredPosition, target.anchoredPosition) < 0.1f, "Belt did not snap and unlock driving.");
            await Task.Delay(800);
            Require(!handle.gameObject.activeInHierarchy, "Belt overlay did not close.");

            var go = GameObject.Find("Accelerate").GetComponent<SprintHoldControl>();
            var stop = GameObject.Find("Brake").GetComponent<SprintHoldControl>();
            var finger = new PointerEventData(EventSystem.current) { pointerId = 42 };
            go.OnPointerDown(finger);
            await Task.Delay(800);
            float acceleratingSpeed = car.Speed;
            Require(acceleratingSpeed > 1 && body.position.z > start.z + 0.2f, "GO did not accelerate.");
            go.OnPointerUp(finger);
            await Task.Delay(300);
            Require(car.Speed < acceleratingSpeed, "Releasing GO did not slow the car.");
            stop.OnPointerDown(finger);
            await Task.Delay(350);
            stop.OnPointerUp(finger);
            Require(car.Speed < 0.15f, "STOP did not brake.");

            GameObject.Find("Left Lane").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            await Task.Delay(850);
            Require(Mathf.Abs(body.position.x + 1.75f) < 0.1f, "Left lane change failed.");
            GameObject.Find("Right Lane").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            await Task.Delay(850);
            Require(Mathf.Abs(body.position.x - 1.75f) < 0.1f, "Right lane change failed.");
            go.OnPointerDown(new PointerEventData(EventSystem.current) { pointerId = 11 });
            stop.OnPointerDown(new PointerEventData(EventSystem.current) { pointerId = 12 });
            await Task.Delay(200);
            Require(car.Speed < 0.15f, "Brake must take priority over GO.");
            go.OnPointerUp(new PointerEventData(EventSystem.current) { pointerId = 11 });
            stop.OnPointerUp(new PointerEventData(EventSystem.current) { pointerId = 12 });

            var horn = GameObject.Find("Horn").GetComponent<SprintHoldControl>();
            horn.OnPointerDown(finger);
            await Task.Delay(700);
            Require(car.HornCount == 1 && car.IsHonking && car.GetComponent<AudioSource>().loop, "Horn did not keep playing while held.");
            horn.OnPointerUp(new PointerEventData(EventSystem.current) { pointerId = 99 });
            Require(car.IsHonking, "Another finger stopped the held horn.");
            horn.OnPointerUp(finger);
            Require(!car.IsHonking, "Horn did not stop on release.");
            horn.OnPointerDown(finger);
            Require(car.IsHonking && car.HornCount == 2, "Horn did not start on the next press.");
            car.ReleaseControls();
            Require(!car.IsHonking, "Releasing controls did not silence the horn.");
            horn.OnPointerUp(finger);

            body.position = new Vector3(1.75f, start.y, 24);
            Physics.SyncTransforms();
            car.SetGoHeld(true);
            await Task.Delay(1800);
            car.ReleaseControls();
            Require(car.CollisionCount >= 1 && car.LastContact.Contains("Obstacle 01"), "Obstacle collision was not detected.");

            body.position = new Vector3(1.75f, start.y, 111);
            Physics.SyncTransforms();
            await Task.Delay(150);
            Require(car.IsFinished && car.Speed < 0.1f, "Course did not finish safely.");
            hud.Restart();
            await Task.Delay(100);
            Require(!car.IsBuckled && !car.IsFinished && car.CollisionCount == 0 && car.HornCount == 0, "Restart did not reset the session.");
            drag = UnityEngine.Object.FindFirstObjectByType<SprintSeatBeltDrag>();
            Require(drag != null && Vector2.Distance(((RectTransform)drag.transform).anchoredPosition, home) < 0.1f, "Restart did not restore belt handle.");
            return "PASS: belt lock, failed drop, successful snap, GO, coasting, brake, both lanes, brake priority, held horn/release, obstacle collision, finish, restart.";
        }
        finally { hud.Restart(); }
    }

    private static void Drag(SprintSeatBeltDrag drag, Vector2 destination)
    {
        Vector2 origin = RectTransformUtility.WorldToScreenPoint(null, drag.transform.position);
        var data = new PointerEventData(EventSystem.current)
        {
            pointerId = -1,
            position = origin,
            pressPosition = origin
        };
        drag.OnBeginDrag(data);
        data.position = destination;
        drag.OnDrag(data);
        drag.OnEndDrag(data);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Sprint prototype check: " + message);
    }
}
