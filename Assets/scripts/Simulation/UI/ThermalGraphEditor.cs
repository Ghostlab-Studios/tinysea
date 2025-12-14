using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Enhanced thermal graph for editing with visual feedback.
/// Shows which parameter is being edited with highlights, markers, and indicators.
/// Higher quality rendering than the display-only ThermalGraphUI.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RawImage))]
public class ThermalGraphEditor : MonoBehaviour
{
    [Header("Thermal Parameters (Kelvin)")]
    public float optimalTemp = 293.15f;
    public float arrhenBreadth = 5273.15f;
    public float arrhenLower = 10273.15f;
    public float arrhenUpper = 21273.15f;
    public float lowerBound = 285.15f;
    public float upperBound = 295.15f;

    [Header("Graph Settings")]
    public int textureWidth = 512;  // Higher resolution
    public int textureHeight = 256;

    [Header("Colors")]
    public Color backgroundColor = new Color(0.1f, 0.1f, 0.15f, 1f);  // Dark blue-gray
    public Color gridColor = new Color(0.2f, 0.2f, 0.25f, 1f);        // Subtle grid
    public Color curveColor = new Color(0.2f, 0.8f, 0.4f, 1f);        // Green curve
    public Color curveGlowColor = new Color(0.2f, 0.8f, 0.4f, 0.3f);  // Glow effect
    public Color highlightColor = new Color(1f, 0.8f, 0.2f, 1f);      // Yellow highlight
    public Color markerColor = new Color(1f, 0.4f, 0.4f, 1f);         // Red markers
    public Color boundLineColor = new Color(0.5f, 0.5f, 0.8f, 0.6f);  // Blue for bounds

    [Header("Display Range")]
    public float tempMinCelsius = 0f;
    public float tempMaxCelsius = 40f;

    [Header("Active Editing")]
    [Tooltip("Which parameter is currently being edited (-1 = none)")]
    public EditingParameter activeParameter = EditingParameter.None;

    public enum EditingParameter
    {
        None = -1,
        OptimalTemp = 0,
        LowerBound = 1,
        UpperBound = 2,
        ArrhenBreadth = 3,
        ArrhenLower = 4,
        ArrhenUpper = 5
    }

    // Constants
    private const float KELVIN_OFFSET = 273.15f;

    // Internal
    private RawImage rawImage;
    private Texture2D graphTexture;
    private float[] performanceValues;
    private Color[] pixels;

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
        if (rawImage != null && (graphTexture == null ||
            graphTexture.width != textureWidth ||
            graphTexture.height != textureHeight))
        {
            CreateTexture();
            UpdateGraph();
        }
    }

    void CreateTexture()
    {
        graphTexture = new Texture2D(textureWidth, textureHeight);
        graphTexture.filterMode = FilterMode.Bilinear;
        graphTexture.wrapMode = TextureWrapMode.Clamp;
        rawImage.texture = graphTexture;
        performanceValues = new float[textureWidth];
        pixels = new Color[textureWidth * textureHeight];
    }

    /// <summary>
    /// Set all thermal parameters at once and refresh the graph.
    /// </summary>
    public void SetParameters(float optTemp, float breadth, float lower, float upper, float lowerB, float upperB)
    {
        optimalTemp = optTemp;
        arrhenBreadth = breadth;
        arrhenLower = lower;
        arrhenUpper = upper;
        lowerBound = lowerB;
        upperBound = upperB;
        UpdateGraph();
    }

    /// <summary>
    /// Set which parameter is being actively edited (for visual feedback).
    /// </summary>
    public void SetActiveParameter(EditingParameter param)
    {
        activeParameter = param;
        UpdateGraph();
    }

    /// <summary>
    /// Clear active parameter highlight.
    /// </summary>
    public void ClearActiveParameter()
    {
        activeParameter = EditingParameter.None;
        UpdateGraph();
    }

    /// <summary>
    /// Force a graph update. Call this after changing any parameter.
    /// </summary>
    public void Refresh()
    {
        UpdateGraph();
    }

    public void OnValidate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (rawImage == null)
                rawImage = GetComponent<RawImage>();

            if (rawImage != null)
            {
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
            if (rawImage != null)
                UpdateGraph();
        }
#endif
    }

    void UpdateGraph()
    {
        if (graphTexture == null || pixels == null) return;

        // Clear to background
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = backgroundColor;

        // Draw grid
        DrawGrid();

        // Calculate performance values
        CalculatePerformanceValues();

        // Draw the curve with glow effect
        DrawCurveWithGlow();

        // Draw bound lines (always visible, dimmer when not active)
        DrawBoundLines();

        // Draw active parameter highlight
        DrawActiveParameterHighlight();

        // Apply to texture
        graphTexture.SetPixels(pixels);
        graphTexture.Apply();
    }

    void DrawGrid()
    {
        // Horizontal grid lines (performance levels)
        float[] perfLevels = { 0.25f, 0.5f, 0.75f, 1.0f };
        foreach (float perf in perfLevels)
        {
            int y = PerformanceToY(perf);
            for (int x = 0; x < textureWidth; x += 4) // Dashed line
            {
                if (x % 8 < 4)
                    SetPixelSafe(x, y, gridColor);
            }
        }

        // Vertical grid lines (temperature every 10°C)
        for (float tempC = 0; tempC <= tempMaxCelsius; tempC += 10f)
        {
            int x = TempToX(tempC);
            for (int y = 0; y < textureHeight; y += 4)
            {
                if (y % 8 < 4)
                    SetPixelSafe(x, y, gridColor);
            }
        }
    }

    void CalculatePerformanceValues()
    {
        for (int x = 0; x < textureWidth; x++)
        {
            float t = x / (float)(textureWidth - 1);
            float tempCelsius = Mathf.Lerp(tempMinCelsius, tempMaxCelsius, t);
            float tempKelvin = tempCelsius + KELVIN_OFFSET;

            performanceValues[x] = CalculatePerformance(tempKelvin);
        }
    }

    float CalculatePerformance(float tempKelvin)
    {
        float T = tempKelvin;
        float OT = optimalTemp;
        float B = arrhenBreadth;
        float L = arrhenLower;
        float U = arrhenUpper;
        float LB = lowerBound;
        float UB = upperBound;

        // Prevent division issues
        if (T <= 0 || OT <= 0 || LB <= 0 || UB <= 0) return 0f;

        float numerator = Mathf.Exp(B / OT - B / T) *
                         (1 + Mathf.Exp(L / OT - L / LB) + Mathf.Exp(U / UB - U / OT));

        float denominator = 1 + Mathf.Exp(L / T - L / LB) + Mathf.Exp(U / UB - U / T);

        if (denominator == 0) return 0f;

        return Mathf.Clamp01(numerator / denominator);
    }

    void DrawCurveWithGlow()
    {
        // Draw glow (thicker, semi-transparent)
        for (int x = 0; x < textureWidth; x++)
        {
            int y = PerformanceToY(performanceValues[x]);

            // Glow radius
            for (int dy = -4; dy <= 4; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist <= 4)
                    {
                        float alpha = (1f - dist / 4f) * 0.3f;
                        Color glowColor = curveGlowColor;
                        glowColor.a = alpha;
                        BlendPixelSafe(x + dx, y + dy, glowColor);
                    }
                }
            }
        }

        // Draw main curve (solid, thicker)
        for (int x = 0; x < textureWidth; x++)
        {
            int y = PerformanceToY(performanceValues[x]);

            // Draw thick line (3 pixels)
            for (int dy = -1; dy <= 1; dy++)
            {
                SetPixelSafe(x, y + dy, curveColor);
            }

            // Connect to next point for smooth line
            if (x < textureWidth - 1)
            {
                int nextY = PerformanceToY(performanceValues[x + 1]);
                DrawLineVertical(x, y, nextY, curveColor);
            }
        }
    }

    void DrawBoundLines()
    {
        // Optimal temperature line
        float optCelsius = optimalTemp - KELVIN_OFFSET;
        int optX = TempToX(optCelsius);
        Color optColor = (activeParameter == EditingParameter.OptimalTemp) ? highlightColor : boundLineColor;
        DrawVerticalLine(optX, optColor, true);

        // Lower bound line
        float lbCelsius = lowerBound - KELVIN_OFFSET;
        int lbX = TempToX(lbCelsius);
        Color lbColor = (activeParameter == EditingParameter.LowerBound) ? highlightColor : boundLineColor;
        lbColor.a *= 0.7f;
        DrawVerticalLine(lbX, lbColor, false);

        // Upper bound line
        float ubCelsius = upperBound - KELVIN_OFFSET;
        int ubX = TempToX(ubCelsius);
        Color ubColor = (activeParameter == EditingParameter.UpperBound) ? highlightColor : boundLineColor;
        ubColor.a *= 0.7f;
        DrawVerticalLine(ubX, ubColor, false);
    }

    void DrawActiveParameterHighlight()
    {
        switch (activeParameter)
        {
            case EditingParameter.OptimalTemp:
                DrawPeakMarker();
                break;
            case EditingParameter.LowerBound:
                DrawBoundMarker(lowerBound, true);
                break;
            case EditingParameter.UpperBound:
                DrawBoundMarker(upperBound, false);
                break;
            case EditingParameter.ArrhenBreadth:
                DrawBreadthIndicator();
                break;
            case EditingParameter.ArrhenLower:
                HighlightLeftSlope();
                break;
            case EditingParameter.ArrhenUpper:
                HighlightRightSlope();
                break;
        }
    }

    void DrawPeakMarker()
    {
        // Find peak position
        float optCelsius = optimalTemp - KELVIN_OFFSET;
        int peakX = TempToX(optCelsius);

        // Get performance at peak
        float perf = CalculatePerformance(optimalTemp);
        int peakY = PerformanceToY(perf);

        // Draw circle at peak
        DrawCircle(peakX, peakY, 6, highlightColor);
        DrawCircle(peakX, peakY, 4, markerColor);
    }

    void DrawBoundMarker(float boundK, bool isLower)
    {
        float boundCelsius = boundK - KELVIN_OFFSET;
        int x = TempToX(boundCelsius);

        // Get performance at this temperature
        float perf = CalculatePerformance(boundK);
        int y = PerformanceToY(perf);

        // Draw diamond marker
        DrawDiamond(x, y, 5, highlightColor);
    }

    void DrawBreadthIndicator()
    {
        // Draw horizontal arrows at 50% performance level showing width
        float optCelsius = optimalTemp - KELVIN_OFFSET;
        int centerX = TempToX(optCelsius);
        int y = PerformanceToY(0.5f);

        // Find where curve crosses 50%
        int leftX = centerX, rightX = centerX;
        for (int x = centerX; x >= 0; x--)
        {
            if (performanceValues[x] < 0.5f) { leftX = x; break; }
        }
        for (int x = centerX; x < textureWidth; x++)
        {
            if (performanceValues[x] < 0.5f) { rightX = x; break; }
        }

        // Draw horizontal line with arrows
        for (int x = leftX; x <= rightX; x++)
        {
            SetPixelSafe(x, y, highlightColor);
            SetPixelSafe(x, y + 1, highlightColor);
        }

        // Arrow heads
        for (int i = 0; i < 5; i++)
        {
            SetPixelSafe(leftX + i, y - i, highlightColor);
            SetPixelSafe(leftX + i, y + i, highlightColor);
            SetPixelSafe(rightX - i, y - i, highlightColor);
            SetPixelSafe(rightX - i, y + i, highlightColor);
        }
    }

    void HighlightLeftSlope()
    {
        // Highlight the left portion of the curve
        float lbCelsius = lowerBound - KELVIN_OFFSET;
        int boundX = TempToX(lbCelsius);

        for (int x = 0; x < boundX && x < textureWidth; x++)
        {
            int y = PerformanceToY(performanceValues[x]);
            for (int dy = -2; dy <= 2; dy++)
            {
                BlendPixelSafe(x, y + dy, new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0.5f));
            }
        }
    }

    void HighlightRightSlope()
    {
        // Highlight the right portion of the curve
        float ubCelsius = upperBound - KELVIN_OFFSET;
        int boundX = TempToX(ubCelsius);

        for (int x = boundX; x < textureWidth; x++)
        {
            int y = PerformanceToY(performanceValues[x]);
            for (int dy = -2; dy <= 2; dy++)
            {
                BlendPixelSafe(x, y + dy, new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0.5f));
            }
        }
    }

    // ==================== Drawing Utilities ====================

    int TempToX(float tempCelsius)
    {
        float t = (tempCelsius - tempMinCelsius) / (tempMaxCelsius - tempMinCelsius);
        return Mathf.Clamp(Mathf.RoundToInt(t * (textureWidth - 1)), 0, textureWidth - 1);
    }

    int PerformanceToY(float performance)
    {
        // 10% padding top and bottom
        float paddedHeight = textureHeight * 0.8f;
        float paddedBottom = textureHeight * 0.1f;
        return Mathf.Clamp(Mathf.RoundToInt(performance * paddedHeight + paddedBottom), 0, textureHeight - 1);
    }

    void SetPixelSafe(int x, int y, Color color)
    {
        if (x >= 0 && x < textureWidth && y >= 0 && y < textureHeight)
        {
            pixels[y * textureWidth + x] = color;
        }
    }

    void BlendPixelSafe(int x, int y, Color color)
    {
        if (x >= 0 && x < textureWidth && y >= 0 && y < textureHeight)
        {
            int idx = y * textureWidth + x;
            Color existing = pixels[idx];
            pixels[idx] = Color.Lerp(existing, color, color.a);
        }
    }

    void DrawVerticalLine(int x, Color color, bool solid)
    {
        for (int y = 0; y < textureHeight; y++)
        {
            if (solid || y % 4 < 2)
            {
                BlendPixelSafe(x, y, color);
                BlendPixelSafe(x - 1, y, color * 0.5f);
                BlendPixelSafe(x + 1, y, color * 0.5f);
            }
        }
    }

    void DrawLineVertical(int x, int y1, int y2, Color color)
    {
        int minY = Mathf.Min(y1, y2);
        int maxY = Mathf.Max(y1, y2);
        for (int y = minY; y <= maxY; y++)
        {
            SetPixelSafe(x, y, color);
        }
    }

    void DrawCircle(int cx, int cy, int radius, Color color)
    {
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= radius * radius)
                {
                    SetPixelSafe(cx + x, cy + y, color);
                }
            }
        }
    }

    void DrawDiamond(int cx, int cy, int size, Color color)
    {
        for (int i = 0; i <= size; i++)
        {
            SetPixelSafe(cx - size + i, cy - i, color);
            SetPixelSafe(cx - size + i, cy + i, color);
            SetPixelSafe(cx + size - i, cy - i, color);
            SetPixelSafe(cx + size - i, cy + i, color);
        }
    }

    // ==================== Public Utilities ====================

    /// <summary>
    /// Calculate the area under the curve (for comparing specialists vs generalists).
    /// </summary>
    public float CalculateAreaUnderCurve()
    {
        float area = 0f;
        float step = (tempMaxCelsius - tempMinCelsius) / textureWidth;

        for (int i = 0; i < textureWidth - 1; i++)
        {
            // Trapezoid rule
            area += step * (performanceValues[i] + performanceValues[i + 1]) / 2f;
        }

        return area;
    }

    /// <summary>
    /// Get the temperature (Celsius) where performance peaks.
    /// </summary>
    public float GetPeakTemperatureCelsius()
    {
        return optimalTemp - KELVIN_OFFSET;
    }

    /// <summary>
    /// Get the maximum performance value.
    /// </summary>
    public float GetPeakPerformance()
    {
        float maxPerf = 0f;
        foreach (float p in performanceValues)
        {
            if (p > maxPerf) maxPerf = p;
        }
        return maxPerf;
    }
}