using System.Drawing;
using UnityEngine;

public class EnemyVariation : MonoBehaviour
{
    [Header("Scale")]
    [SerializeField] private Vector2 scaleRange = new(0.9f, 1.2f);

    [Header("Motion")]
    [SerializeField] private Vector2 speedMultiplierRange = new(0.8f, 1.2f);
    [SerializeField] private Vector2 bobSpeedMultiplierRange = new(0.8f, 1.2f);
    [SerializeField] private float maxJitterDegrees = 5f;

    [Header("Color")]
    [Header("Tint")]
    [SerializeField] private UnityEngine.Color[] baseColors;
    [SerializeField] private Vector2 hueRange = new(-0.03f, 0.03f);
    [SerializeField] private Vector2 saturationRange = new(0.9f, 1.1f);
    [SerializeField] private Vector2 valueRange = new(0.8f, 1.15f);
    [Header("Mesh Creation")]
    [SerializeField] private Mesh meshSource;
    [SerializeField] private int meshVariantCount = 40;

    private Mesh[] meshVariants;

    private void OnDestroy()
    {
        if (meshVariants == null || meshVariants.Length == 0) 
            return;

        for (int i = 0; i < meshVariants.Length; i++)
        {
            if (meshVariants[i] != null) Destroy(meshVariants[i]);
        }
    }

    public void Initialize()
    {
        BuildMeshVariants();
    }

    public void Apply(Enemy e)
    {
        if (e == null)
            return;

        ApplyMeshTint(e);
        SetScale(e);
        SetMotion(e);
    }

    public void ApplyMeshTint(Enemy e)
    {
        int v = Random.Range(0, meshVariantCount);
        e.meshFilter.sharedMesh = meshVariants[v];
    }

    private void BuildMeshVariants()
    {
        meshVariants = new Mesh[meshVariantCount];

        for (int i = 0; i < meshVariantCount; i++)
        {
            UnityEngine.Color tint = PickTint().linear;
            meshVariants[i] = CreateTinted(meshSource, tint);
        }
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

    private Mesh CreateTinted(Mesh source, UnityEngine.Color tint)
    {
        Mesh m = Instantiate(source);
        m.name = source.name + "_Tinted";

        Color32[] colors = new Color32[m.vertexCount];
        Color32 c = tint;

        for (int i = 0; i < colors.Length; i++)
            colors[i] = c;

        m.colors32 = colors;
        m.UploadMeshData(false);
        return m;
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

        e.bobPhase = Random.Range(0f, Mathf.PI * 2f);
        e.bobSpeedMul = Utilities.Random(bobSpeedMultiplierRange);
    }
}