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

    private const float LETHAL_TRANSITION_WIDTH = 2.0f; // Smooth fade width in degrees
    private const int UIMarginBottom = 10;

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

        int gWidth = textureWidth;
        performanceValues = new float[gWidth];
    }

    void UpdateGraph()
    {
        if (graphTexture == null) return;

        // Graph area with bottom margin for temperature labels
        int gBottom = UIMarginBottom;
        int gHeight = textureHeight - UIMarginBottom;
        int gWidth = textureWidth;

        // Clear texture
        Color[] pixels = new Color[textureWidth * textureHeight];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = backgroundColor;

        // Auto-zoom to species range
        UpdateDisplayRange();

        // Calculate performance values with smooth lethal fade
        float ctMinK = ctMinC + 273.15f;
        float ctMaxK = ctMaxC + 273.15f;
        float halfRange = (ctMaxK - ctMinK) / 2f;
        float tw = Mathf.Min(LETHAL_TRANSITION_WIDTH, halfRange);

        if (performanceValues == null || performanceValues.Length != gWidth)
            performanceValues = new float[gWidth];

        for (int x = 0; x < gWidth; x++)
        {
            float t = x / (float)(gWidth - 1);
            float tempCelsius = Mathf.Lerp(tempMinCelsius, tempMaxCelsius, t);
            float tempKelvin = tempCelsius + 273.15f;

            // Smooth lethal fade
            float fadeFactor = 1f;
            if (tempKelvin <= ctMinK)
                fadeFactor = 0f;
            else if (tempKelvin < ctMinK + tw)
                fadeFactor = 0.5f * (1f + Mathf.Cos(Mathf.PI * (ctMinK + tw - tempKelvin) / tw));

            if (tempKelvin >= ctMaxK)
                fadeFactor = 0f;
            else if (tempKelvin > ctMaxK - tw)
                fadeFactor *= 0.5f * (1f + Mathf.Cos(Mathf.PI * (tempKelvin - (ctMaxK - tw)) / tw));

            if (fadeFactor <= 0f)
            {
                performanceValues[x] = 0f;
                continue;
            }

            float performance = (Mathf.Exp(arrhenBreadth / optimalTemp - arrhenBreadth / tempKelvin) *
                    (1 + Mathf.Exp(arrhenLower / optimalTemp - arrhenLower / lowerBound) +
                        Mathf.Exp(arrhenUpper / upperBound - arrhenUpper / optimalTemp))) /
                    (1 + Mathf.Exp(arrhenLower / tempKelvin - arrhenLower / lowerBound) +
                        Mathf.Exp(arrhenUpper / upperBound - arrhenUpper / tempKelvin));

            performanceValues[x] = Mathf.Clamp01(performance) * fadeFactor * pmax;
        }

        // Draw curve (within graph area above the margin)
        for (int x = 0; x < gWidth; x++)
        {
            float paddedHeight = gHeight * (1f - 2f * paddingPercent);
            float paddedBottom = gBottom + gHeight * paddingPercent;

            int y = Mathf.RoundToInt(performanceValues[x] * paddedHeight + paddedBottom);
            y = Mathf.Clamp(y, 0, textureHeight - 1);

            // Draw vertical line for thickness
            for (int dy = -1; dy <= 1; dy++)
            {
                int py = Mathf.Clamp(y + dy, 0, textureHeight - 1);
                pixels[py * textureWidth + x] = curveColor;
            }
        }

        // Draw min/max temperature labels at bottom corners
        Color labelColor = new Color(0.6f, 0.6f, 0.6f, 1f);

        string minLabel = Mathf.RoundToInt(tempMinCelsius).ToString();
        PixelFont.DrawString(pixels, textureWidth, textureHeight,
                             minLabel, 1, 1, labelColor);

        string maxLabel = Mathf.RoundToInt(tempMaxCelsius).ToString();
        int maxLabelWidth = PixelFont.MeasureString(maxLabel);
        PixelFont.DrawString(pixels, textureWidth, textureHeight,
                             maxLabel, textureWidth - maxLabelWidth - 1, 1, labelColor);

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
