using UnityEngine;

public enum LightColor
{
    Red,
    Yellow,
    Green
}

/// <summary>
/// One physical traffic light (the 3D model).
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

    private static readonly Color RedOn = new Color(1f, 0.1f, 0.1f);
    private static readonly Color YellowOn = new Color(1f, 0.8f, 0f);
    private static readonly Color GreenOn = new Color(0.1f, 1f, 0.2f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Material redMat;
    private Material yellowMat;
    private Material greenMat;
    private bool initialized;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        redMat = InitLampMaterial(redLamp);
        yellowMat = InitLampMaterial(yellowLamp);
        greenMat = InitLampMaterial(greenLamp);
    }

    private static Material InitLampMaterial(Renderer lamp)
    {
        if (lamp == null) return null;
        Material mat = lamp.material;
        mat.EnableKeyword("_EMISSION");
        return mat;
    }

    public void SetColor(LightColor color)
    {
        EnsureInitialized();

        SetLamp(redMat, RedOn, color == LightColor.Red);
        SetLamp(yellowMat, YellowOn, color == LightColor.Yellow);
        SetLamp(greenMat, GreenOn, color == LightColor.Green);
    }

    private void SetLamp(Material mat, Color onColor, bool isOn)
    {
        if (mat == null) return;

        Color targetColor = isOn ? onColor : offColor;
        mat.color = targetColor;
        if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, targetColor);
        if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, targetColor);

        mat.SetColor(EmissionColorId, isOn ? onColor * emissionIntensity : Color.black);
    }
}