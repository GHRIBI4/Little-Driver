using UnityEngine;

/// <summary>
/// Finite State Machine for the intersection (Scenario 1).
///
/// Phase | North-South | East-West
///   1   |   Green     |   Red
///   2   |   Yellow    |   Red
///   3   |   Red       |   Green
///   4   |   Red       |   Yellow
///   5   |   Red       |   Red
///   then it starts again at phase 1.
/// </summary>
public class TrafficLightFSM : MonoBehaviour
{
    // The 5 states of the machine.
    private enum State
    {
        NS_Green_EW_Red,     // phase 1
        NS_Yellow_EW_Red,    // phase 2
        NS_Red_EW_Green,     // phase 3
        NS_Red_EW_Yellow,    // phase 4
        AllRed               // phase 5
    }

    [Header("Traffic lights (drag them from the scene)")]
    [SerializeField] private TrafficLight[] northSouthLights; // the North and South lights
    [SerializeField] private TrafficLight[] eastWestLights;   // the East and West lights

    [Header("Durations (seconds)")]
    [SerializeField] private float greenTime  = 5f;
    [SerializeField] private float yellowTime = 2f;
    [SerializeField] private float allRedTime = 2f;

    private State currentState;
    private float timeLeft;

    private void Start()
    {
        EnterState(State.NS_Green_EW_Red);
    }

    private void Update()
    {
        timeLeft -= Time.deltaTime;

        if (timeLeft <= 0f)
        {
            EnterState(GetNextState(currentState));
        }
    }

    // Transitions: each state leads to the next one, the last one loops back to the first.
    private State GetNextState(State state)
    {
        int stateCount = System.Enum.GetValues(typeof(State)).Length;
        return (State)(((int)state + 1) % stateCount);
    }

    // Called once each time we switch to a new state.
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

    // Updates the lights in the scene, restarts the timer and prints to the console.
    private void Apply(LightColor northSouth, LightColor eastWest, float duration)
    {
        SetAll(northSouthLights, northSouth);
        SetAll(eastWestLights, eastWest);

        timeLeft = duration;

        Debug.Log($"Phase {(int)currentState + 1}/5  |  North-South: {northSouth}  |  East-West: {eastWest}  ({duration}s)");
    }

    private void SetAll(TrafficLight[] lights, LightColor color)
    {
        foreach (TrafficLight light in lights)
        {
            light.SetColor(color);
        }
    }
}
