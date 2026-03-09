using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(RawImage))]
public class ThermalGraphUI : MonoBehaviour
{
    [Header("Thermal Parameters")]
    public float optimalTemp = 295.15f;
    public float arrhenBreadth = 4258f;
    public float arrhenLower = 7457f;
    public float arrhenUpper = 19664f;
    public float lowerBound = 286f;
    public float upperBound = 298f;
    public float pmax = 1.0f;
    public float ctMinC = -5.0f;
    public float ctMaxC = 50.0f;

    [Header("Graph Settings")]
    public int textureWidth = 256;
    public int textureHeight = 128;
    public Color curveColor = Color.green;
    public Color backgroundColor = Color.black;
    [Range(0.05f, 0.3f)]
    public float paddingPercent = 0.1f; // 10% padding top and bottom

    private float tempMinCelsius = 0f;
    private float tempMaxCelsius = 40f;

    private RawImage rawImage;
    private Texture2D graphTexture;
    private float[] performanceValues;

    void Start()
    {
        InitializeGraph();
    }

    void OnEnable()
    {
        InitializeGraph();
    }

    void InitializeGraph()
    {
        rawImage = GetComponent<RawImage>();
        if (rawImage != null && (graphTexture == null || graphTexture.width != textureWidth || graphTexture.height != textureHeight))
        {
            CreateTexture();
            UpdateGraph();
        }
    }

    public void OnValidate()
    {
#if UNITY_EDITOR
        // Only update in editor when values change
        if (!Application.isPlaying)
        {
            if (rawImage == null)
                rawImage = GetComponent<RawImage>();

            if (rawImage != null)
            {
                // Delay the update to avoid issues with Unity's editor
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null)
                    {
                        CreateTexture();
                        UpdateGraph();
                    }
                };
            }
        }
        else
        {
            // In play mode, update immediately
            if (rawImage != null)
                UpdateGraph();
        }
#endif
    }

    void CreateTexture()
    {
        graphTexture = new Texture2D(textureWidth, textureHeight);
        graphTexture.filterMode = FilterMode.Bilinear;
        rawImage.texture = graphTexture;
        performanceValues = new float[textureWidth];
    }

    void UpdateGraph()
    {
        if (graphTexture == null) return;

        // Clear texture
        Color[] pixels = new Color[textureWidth * textureHeight];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = backgroundColor;

        // Auto-zoom to species range
        UpdateDisplayRange();

        // Calculate performance values
        float ctMinK = ctMinC + 273.15f;
        float ctMaxK = ctMaxC + 273.15f;
        for (int x = 0; x < textureWidth; x++)
        {
            float t = x / (float)(textureWidth - 1);
            float tempCelsius = Mathf.Lerp(tempMinCelsius, tempMaxCelsius, t);
            float tempKelvin = tempCelsius + 273.15f;

            if (tempKelvin < ctMinK || tempKelvin > ctMaxK)
            {
                performanceValues[x] = 0f;
                continue;
            }

            float performance = (Mathf.Exp(arrhenBreadth / optimalTemp - arrhenBreadth / tempKelvin) *
                    (1 + Mathf.Exp(arrhenLower / optimalTemp - arrhenLower / lowerBound) +
                        Mathf.Exp(arrhenUpper / upperBound - arrhenUpper / optimalTemp))) /
                    (1 + Mathf.Exp(arrhenLower / tempKelvin - arrhenLower / lowerBound) +
                        Mathf.Exp(arrhenUpper / upperBound - arrhenUpper / tempKelvin));

            performanceValues[x] = Mathf.Clamp01(performance) * pmax;
        }

        // Draw curve
        for (int x = 0; x < textureWidth; x++)
        {
            // Apply padding - map performance [0,1] to padded range
            float paddedHeight = textureHeight * (1f - 2f * paddingPercent);
            float paddedBottom = textureHeight * paddingPercent;

            int y = Mathf.RoundToInt(performanceValues[x] * paddedHeight + paddedBottom);
            y = Mathf.Clamp(y, 0, textureHeight - 1);

            // Draw vertical line for thickness
            for (int dy = -1; dy <= 1; dy++)
            {
                int py = Mathf.Clamp(y + dy, 0, textureHeight - 1);
                pixels[py * textureWidth + x] = curveColor;
            }
        }

        graphTexture.SetPixels(pixels);
        graphTexture.Apply();
    }

    void UpdateDisplayRange()
    {
        float padding = 5f;
        float minWidth = 20f;

        float rangeMin = ctMinC - padding;
        float rangeMax = ctMaxC + padding;

        float width = rangeMax - rangeMin;
        if (width < minWidth)
        {
            float center = (rangeMin + rangeMax) / 2f;
            rangeMin = center - minWidth / 2f;
            rangeMax = center + minWidth / 2f;
        }

        tempMinCelsius = rangeMin;
        tempMaxCelsius = rangeMax;
    }
}