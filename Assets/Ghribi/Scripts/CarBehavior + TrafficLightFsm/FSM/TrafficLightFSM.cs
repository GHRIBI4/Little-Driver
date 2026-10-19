using UnityEngine;

/// <summary>
/// Finite State Machine for the intersection (Scenario 1).
/// </summary>
public class TrafficLightFSM : MonoBehaviour
{
    private enum State
    {
        NS_Green_EW_Red,     // phase 1
        NS_Yellow_EW_Red,    // phase 2
        NS_Red_EW_Green,     // phase 3
        NS_Red_EW_Yellow,    // phase 4
        AllRed               // phase 5
    }

    private const int StateCount = 5;

    [Header("Traffic lights (drag them from the scene)")]
    [SerializeField] private TrafficLight[] northSouthLights;
    [SerializeField] private TrafficLight[] eastWestLights;

    [Header("Durations (seconds)")]
    [SerializeField] private float greenTime = 5f;
    [SerializeField] private float yellowTime = 3f;
    [SerializeField] private float allRedTime = 2f;

    private State currentState;
    private float timeLeft;
    private LightColor northSouthColor = LightColor.Green;
    private LightColor eastWestColor = LightColor.Red;

    private void Start()
    {
        EnterState(State.NS_Green_EW_Red);
    }

    private void Update()
    {
        timeLeft -= Time.deltaTime;

        if (timeLeft <= 0f)
        {
            EnterState((State)(((int)currentState + 1) % StateCount));
        }
    }

    /// <summary>Returns the current light color for the given approach direction.</summary>
    public LightColor GetLight(Direction approach)
    {
        return approach.IsNorthSouth() ? northSouthColor : eastWestColor;
    }

    private void EnterState(State newState)
    {
        currentState = newState;

        switch (newState)
        {
            case State.NS_Green_EW_Red:
                Apply(LightColor.Green, LightColor.Red, greenTime);
                break;

            case State.NS_Yellow_EW_Red:
                Apply(LightColor.Yellow, LightColor.Red, yellowTime);
                break;

            case State.NS_Red_EW_Green:
                Apply(LightColor.Red, LightColor.Green, greenTime);
                break;

            case State.NS_Red_EW_Yellow:
                Apply(LightColor.Red, LightColor.Yellow, yellowTime);
                break;

            case State.AllRed:
                Apply(LightColor.Red, LightColor.Red, allRedTime);
                break;
        }
    }

    private void Apply(LightColor northSouth, LightColor eastWest, float duration)
    {
        northSouthColor = northSouth;
        eastWestColor = eastWest;

        SetAll(northSouthLights, northSouth);
        SetAll(eastWestLights, eastWest);

        timeLeft = duration;

        Debug.Log($"Phase {(int)currentState + 1}/5  |  North-South: {northSouth}  |  East-West: {eastWest}  ({duration}s)");
    }

    private static void SetAll(TrafficLight[] lights, LightColor color)
    {
        if (lights == null) return;

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null) lights[i].SetColor(color);
        }
    }
}