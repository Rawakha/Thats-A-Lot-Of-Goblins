using System.Drawing;
using UnityEngine;

public class EnemyVariation : MonoBehaviour
{
    [Header("Scale")]
    [SerializeField] private Vector2 scaleRange = new(0.9f, 1.2f);

    [Header("Motion")]
    [SerializeField] private Vector2 speedMultiplierRange = new(0.8f, 1.2f);
    [SerializeField] private float maxJitterDegrees = 5f;

    [Header("Color")]
    [Header("Tint")]
    [SerializeField] private UnityEngine.Color[] baseColors;
    [SerializeField] private Vector2 hueRange = new(-0.03f, 0.03f);
    [SerializeField] private Vector2 saturationRange = new(0.9f, 1.1f);
    [SerializeField] private Vector2 valueRange = new(0.8f, 1.15f);

    public void Apply(Enemy e)
    {
        if (e == null)
            return;

        ApplyTint(e);
        SetScale(e);
        SetMotion(e);
    }

    public void ApplyTint(Enemy e)
    {
        e.renderColor = PickTint().linear;
    }

    private UnityEngine.Color PickTint()
    {
        UnityEngine.Color baseColor = Utilities.Random<UnityEngine.Color>(baseColors);
        UnityEngine.Color.RGBToHSV(baseColor, out float h, out float s, out float v);

        h += Utilities.Random(hueRange);
        s *= Utilities.Random(saturationRange);
        v *= Utilities.Random(valueRange);

        return UnityEngine.Color.HSVToRGB(h, s, v);
    }

    private void SetScale(Enemy e)
    {
        float scaleMultiplier = Utilities.Random(scaleRange);
        e.transform.localScale *= scaleMultiplier;

        // Store the visuals base scale
        e.visualBaseScale = e.visual.localScale;
    }
    private void SetMotion(Enemy e)
    {
        e.speedMultiplier = Utilities.Random(speedMultiplierRange);

        float angle = Random.Range(-maxJitterDegrees, maxJitterDegrees) * Mathf.Deg2Rad;
        e.steerCos = Mathf.Cos(angle);
        e.steerSin = Mathf.Sin(angle);
    }
}