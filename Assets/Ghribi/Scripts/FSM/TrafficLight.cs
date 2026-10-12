using UnityEngine;

public enum LightColor
{
    Red,
    Yellow,
    Green
}

/// <summary>
/// One physical traffic light (the 3D model).
/// It only knows how to show a color. It does NOT decide when to change.
/// </summary>
public class TrafficLight : MonoBehaviour
{
    [Header("The 3 lamps of your 3D model")]
    [SerializeField] private Renderer redLamp;
    [SerializeField] private Renderer yellowLamp;
    [SerializeField] private Renderer greenLamp;

    [Header("Look")]
    [SerializeField] private Color offColor = new Color(0.15f, 0.15f, 0.15f);
    [SerializeField] private float emissionIntensity = 2f;

    private static readonly Color RedOn    = new Color(1f, 0.1f, 0.1f);
    private static readonly Color YellowOn = new Color(1f, 0.8f, 0f);
    private static readonly Color GreenOn  = new Color(0.1f, 1f, 0.2f);

    /// <summary>Turns on the lamp matching 'color' and turns the other two off.</summary>
    public void SetColor(LightColor color)
    {
        SetLamp(redLamp,    RedOn,    color == LightColor.Red);
        SetLamp(yellowLamp, YellowOn, color == LightColor.Yellow);
        SetLamp(greenLamp,  GreenOn,  color == LightColor.Green);
    }

    private void SetLamp(Renderer lamp, Color onColor, bool isOn)
    {
        Material material = lamp.material; // creates a per-lamp copy, safe to edit

        material.color = isOn ? onColor : offColor;

        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", isOn ? onColor * emissionIntensity : Color.black);
    }
}
