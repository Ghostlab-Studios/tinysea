using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class Temperature : MonoBehaviour
{

    //private LineRenderer historyLine;

    private List<LineRenderer> predictionLines;
    private List<LineRenderer> historicalLineRenders;
    private List<TempDot> dots; 

    public float maxTemp = 40;
    public float minTemp = 0;

    public float maxTempVariance = 10; //per day
    public float minTempVariance = 3;

    public float maxChanceDots = 1;
    public float dotSpreadChance = .5f;

    public int numPredictions = 1;

    public Color hotColor = Color.red;
    public Color neutralColor = Color.yellow;
    public Color coldColor = Color.blue;

    public GameObject linePrefab;
    public GameObject dotPrefab;
    public GameObject selectorDot;

    public float dotSpacing = 3;
    public float segmentSpacing = 3;
    public int historicalSegments = 6;

    public float animationSeconds = 4; //how long the animation plays
    public float pickSeconds = 1;
    public float animationFlipMaxSpeed = .1f; //how fast it flips between dots.
    public float animationFlipMinSpeed = .3f; //how slow the flipping gets at the end.
    private float animationTimer = 0;
    private float pickTimer = 0;
    private float pickFlicker = 0;
    private float flipTimer = 0;
    private TempDot currentDot;
    public bool animating = false;

    private Queue<float> historicalTemperatures;

    private List<float> possibleTemperatures;
    private List<int> possibleLikelihoods;

    private Vector3 homePos;

    public float temperature = 20;
    public TemperatureTrend trend;

    public float currentDay = 1;

    public Text tempText;
    private float tempRound;
    public float tempVisual;

    public Image bar40;
    public Image bar35;
    public Image bar30;
    public Image bar25;
    public Image bar20;
    public Image bar15;
    public Image bar10;
    public Image bar5;
    public Image bar0;

    private Image currentBar;

    // Use this for initialization
    void Start()
    {
        homePos = transform.position;
        //create our historical lines
        //historyLine = GetComponent<LineRenderer>();
        historicalTemperatures = new Queue<float>();
        for (int i = 0; i < historicalSegments; i++)
        {
            historicalTemperatures.Enqueue(5);
        }
        historicalLineRenders = new List<LineRenderer>();
        for (int i = 0; i < historicalSegments - 1; i++)
        {
#if UNITY_EDITOR
            GameObject lineObj = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(linePrefab);
#else
            GameObject lineObj = GameObject.Instantiate(linePrefab);
#endif
            lineObj.transform.parent = this.transform;
            lineObj.transform.position = transform.position + new Vector3(0, 0, -1);
            lineObj.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            historicalLineRenders.Add(lineObj.GetComponent<LineRenderer>());
        }

        //create our branching lines
        predictionLines = new List<LineRenderer>();
        for (int i = 0; i < numPredictions; i++)
        {
            //instantiate
#if UNITY_EDITOR
            GameObject lineObj = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(linePrefab);
#else
            GameObject lineObj = GameObject.Instantiate(linePrefab);
#endif
            //make the lines children of this object
            lineObj.transform.parent = this.transform;
            lineObj.transform.position = transform.position + new Vector3(0, 0, -1);
            lineObj.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            //and put them in a list
            predictionLines.Add(lineObj.GetComponent<LineRenderer>());
        }

        //create some dots
        dots = new List<TempDot>();
        for (int i = 0; i < numPredictions + maxChanceDots; i++)
        {
#if UNITY_EDITOR
            GameObject dotObj = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(dotPrefab);
#else
            GameObject dotObj = GameObject.Instantiate(dotPrefab);
#endif
            dotObj.transform.parent = this.transform;
            //dotObj.transform.localScale *= transform.localScale;
            dots.Add(new TempDot(dotObj.GetComponent<SpriteRenderer>(), 0, null, i));
        }
        currentDot = dots[0];

        //disable our selector dot
        selectorDot.GetComponent<SpriteRenderer>().enabled = false;

        //GenerateTemperature(currentDay);
        generatePossibilities();
        //GenerateTemperature(currentDay);
        drawLines();
        tempText.text = temperature.ToString();
        tempRound = temperature;
        tempVisual = temperature;
        currentBar = bar5;
    }

    // Update is called once per frame
    void Update()
    {
        if (animating)
        {
            Animate();
        }
    }

    void OnDrawGizmos()
    {
        /*float segL = (historicalSegments - 1) * segmentSpacing;
        float dotL = dotSpacing * (maxChanceDots + 1);
        Vector3 tPos = transform.position;
        Gizmos.color = neutralColor;
        Gizmos.DrawLine(tPos + new Vector3(0, minTemp + (maxTemp - minTemp) / 2, 0),
            tPos + new Vector3(segL, minTemp + (maxTemp - minTemp) / 2, 0));
        Gizmos.color = hotColor;
        Gizmos.DrawLine(tPos + new Vector3(segL + segmentSpacing, maxTemp, 0),
            tPos + new Vector3(segL + segmentSpacing + dotL, maxTemp, 0));
        Gizmos.color = coldColor;
        Gizmos.DrawLine(tPos + new Vector3(segL + segmentSpacing, minTemp, 0),
            tPos + new Vector3(segL + segmentSpacing + dotL, minTemp, 0));*/
    }

    public void updateTemperature()
    {
        animating = true;
        animationTimer = 0;
        pickTimer = 0;
    }

    void Animate()
    {
        if (animationTimer > animationSeconds && pickTimer > pickSeconds)
        {
            animating = false;
            animationTimer = 0;
            flipTimer = 0;
            PickTemperature();
            selectorDot.GetComponent<SpriteRenderer>().enabled = false;
            transform.position = homePos;
            return;
        }

        if (animationTimer <= animationSeconds)
        {
            float currentFlipLength = animationFlipMinSpeed +
                (1 - animationTimer / animationSeconds) *
                (animationFlipMaxSpeed - animationFlipMinSpeed);
            if (flipTimer > currentFlipLength)
            {
                //pick new dot and try to make sure it's not the same as the first one
                int lastIndex = currentDot.index;
                int newIndex = lastIndex;
                for (int i = 0; i < 10; i++)
                {
                    if (lastIndex == newIndex)
                        newIndex = Random.Range(0, dots.Count);
                }
                currentDot = dots[newIndex];
                //color new dot
                selectorDot.GetComponent<SpriteRenderer>().enabled = true;
                selectorDot.transform.position = currentDot.spriteRender.transform.position;
                //increment
                flipTimer = 0;
            }

            animationTimer += Time.deltaTime;
            flipTimer += Time.deltaTime;
        }
        else
        {
            pickFlicker += Time.deltaTime;
            if (pickFlicker > pickSeconds / 10)
            {
                selectorDot.GetComponent<SpriteRenderer>().enabled = !selectorDot.GetComponent<SpriteRenderer>().enabled;
                pickFlicker = 0;
            }
            pickTimer += Time.deltaTime;
            transform.position = Vector3.Lerp(homePos, homePos + new Vector3(-segmentSpacing, 0, 0), pickTimer / pickSeconds);
            //currentDot.lineRender.SetColors(Color.white, Color.white);
            //currentDot.lineRender.startColor = Color.white;
            //currentDot.lineRender.endColor = Color.white;

        }
    }

    void PickTemperature()
    {
        //GenerateTemperature(currentDay);
        temperature = trend.GetTemperature(currentDay);
        UpdateTempRound();
        UpdateTemperatureText();
        UpdateTempVisual();
        UpdateTempBar();
        currentDot.temperature = tempVisual;
        //temperature = trend.GetTemperature(currentDay);
        historicalTemperatures.Dequeue();
        historicalTemperatures.Enqueue(temperature);
        generatePossibilities();
        drawLines();
    }

    private void UpdateTempRound()
    {
        float x = Mathf.Round(temperature);
        if (x > 40)
        {
            x = 40;
        }
        else if (x < 0)
        {
            x = 0;
        }
        tempRound = x;
    }

    private void UpdateTemperatureText()
    {
        tempText.text = tempRound.ToString();
    }

    private void UpdateTempVisual()
    {
        tempVisual = Mathf.Round(tempRound / 5) * 5;
    }

    private void UpdateTempBar()
    {
        switch ((int)tempVisual)
        {
            case 40:
                currentBar.gameObject.SetActive(false);
                currentBar = bar40;
                currentBar.gameObject.SetActive(true);
                break;
            case 35:
                currentBar.gameObject.SetActive(false);
                currentBar = bar35;
                currentBar.gameObject.SetActive(true);
                break;
            case 30:
                currentBar.gameObject.SetActive(false);
                currentBar = bar30;
                currentBar.gameObject.SetActive(true);
                break;
            case 25:
                currentBar.gameObject.SetActive(false);
                currentBar = bar25;
                currentBar.gameObject.SetActive(true);
                break;
            case 20:
                currentBar.gameObject.SetActive(false);
                currentBar = bar20;
                currentBar.gameObject.SetActive(true);
                break;
            case 15:
                currentBar.gameObject.SetActive(false);
                currentBar = bar15;
                currentBar.gameObject.SetActive(true);
                break;
            case 10:
                currentBar.gameObject.SetActive(false);
                currentBar = bar10;
                currentBar.gameObject.SetActive(true);
                break;
            case 5:
                currentBar.gameObject.SetActive(false);
                currentBar = bar5;
                currentBar.gameObject.SetActive(true);
                break;
            default:
                currentBar.gameObject.SetActive(false);
                currentBar = bar0;
                currentBar.gameObject.SetActive(true);
                break;
        }
    }

    float GenerateTemperature(float day)
    {
        possibleTemperatures = new List<float>();
        possibleLikelihoods = new List<int>();

        possibleTemperatures.Add(temperature);
        possibleLikelihoods.Add(1);

        //randomly start either up or down
        bool goUp = Random.value < .5;
        float lastUp = temperature;
        float lastDown = temperature;

        bool toohigh = false;
        bool toolow = false;
        for (int i = 1; i < numPredictions; i++)
        {
            if (goUp)
            {
                if (lastUp + minTempVariance < maxTemp)
                {
                    float newTemp = lastUp + minTempVariance + (maxTempVariance - minTempVariance) * Random.value;
                    newTemp = Mathf.Min(newTemp, maxTemp);
                    lastUp = newTemp;
                    possibleTemperatures.Add(newTemp);
                    possibleLikelihoods.Add(1);
                }
                else
                {
                    i--;
                    toohigh = true;
                }
            }
            else
            {
                if (lastDown - minTempVariance > minTemp)
                {
                    float newTemp = lastDown - minTempVariance - (maxTempVariance - minTempVariance) * Random.value;
                    newTemp = Mathf.Max(newTemp, minTemp);
                    lastDown = newTemp;
                    possibleTemperatures.Add(newTemp);
                    possibleLikelihoods.Add(1);
                }
                else
                {
                    i--;
                    toolow = true;
                }
            }

            if (toohigh && toolow)
            {
                Debug.LogError("Ran out of space for predictions! Decrease the number of predictions, or decrease the maxTempVariance.");
                //return;
            }

            goUp = !goUp;
        }

        //add the likihood "dots" randomly.
        for (int i = 0; i < maxChanceDots; i++)
        {
            int index = Random.Range(0, possibleLikelihoods.Count);
            possibleLikelihoods[index] = possibleLikelihoods[index] + 1;
        }
        return trend.GetTemperature(day);
    }

    //creates possible temperatures
    void generatePossibilities()
    {
        possibleTemperatures = new List<float>();
        possibleLikelihoods = new List<int>();

        //the first possibility is always "no change"
        possibleTemperatures.Add(temperature);
        possibleLikelihoods.Add(1);

        //randomly start either up or down
        bool goUp = Random.value < .5;
        float lastUp = temperature;
        float lastDown = temperature;

        //either add a new temperature above or below our current temperature
        //unless it's outside of our max/min temperature
        //also make sure to keep the slightly-randomized temperatures inside of our max/min
        bool toohigh = false;
        bool toolow = false;
        for (int i = 1; i < numPredictions; i++)
        {
            if (goUp)
            {
                if (lastUp + minTempVariance < maxTemp)
                {
                    float newTemp = lastUp + minTempVariance + (maxTempVariance - minTempVariance) * Random.value;
                    newTemp = Mathf.Min(newTemp, maxTemp);
                    lastUp = newTemp;
                    possibleTemperatures.Add(newTemp);
                    possibleLikelihoods.Add(1);
                }
                else
                {
                    i--;
                    toohigh = true;  
                }
            }
            else
            {
                if (lastDown - minTempVariance > minTemp)
                {
                    float newTemp = lastDown - minTempVariance - (maxTempVariance - minTempVariance) * Random.value;
                    newTemp = Mathf.Max(newTemp, minTemp);
                    lastDown = newTemp;
                    possibleTemperatures.Add(newTemp);
                    possibleLikelihoods.Add(1);
                }
                else
                {
                    i--;
                    toolow = true;
                }
            }

            if (toohigh && toolow)
            {
                Debug.LogError("Ran out of space for predictions! Decrease the number of predictions, or decrease the maxTempVariance.");
                return;
            }

            goUp = !goUp;
        }

        //add the likihood "dots" randomly.
        for (int i = 0; i < maxChanceDots; i++)
        {
            int index = Random.Range(0, possibleLikelihoods.Count);
            possibleLikelihoods[index] = possibleLikelihoods[index] + 1;
        }
    }

    //draw the lines (and dots)
    void drawLines()
    {
        float[] tempArr = historicalTemperatures.ToArray();
        for (int i = 0; i < historicalSegments - 1; i++)
        {
            LineRenderer linRend = historicalLineRenders[i];
            //linRend.SetVertexCount(2);
            linRend.positionCount = 2;
            linRend.SetPosition(0, new Vector3(i * segmentSpacing, tempArr[i], 0));
            linRend.SetPosition(1, new Vector3((i + 1) * segmentSpacing, tempArr[i + 1], 0));
            //linRend.SetColors(getColorTemperature(tempArr[i]), getColorTemperature(tempArr[i + 1]));
            linRend.startColor = getColorTemperature(tempArr[i]);
            linRend.endColor = getColorTemperature(tempArr[i + 1]);
        }

        for (int i = 0; i < predictionLines.Count; i++)
        {
            if (i >= possibleTemperatures.Count)
            {
                //we don't need this line, so hide it.
                predictionLines[i].enabled = false;
            }
            else
            {
                LineRenderer lin = predictionLines[i];
                lin.enabled = true;
                //lin.SetVertexCount(2);
                lin.positionCount = 2;
                lin.SetPosition(0, new Vector3(segmentSpacing * (historicalSegments - 1), temperature, -1 - i));
                lin.SetPosition(1, new Vector3(segmentSpacing * (historicalSegments), temperature, -1 - i));
                //lin.SetColors(getColorTemperature(temperature), getColorTemperature(possibleTemperatures[i]));
                lin.startColor = getColorTemperature(temperature);
                lin.endColor = getColorTemperature(possibleTemperatures[i]);
            }
        }

        int d = 0;
        //Debug.Log("dots " + dots.Count);
        for (int i = 0; i < possibleLikelihoods.Count; i++)
        {
            int nDots = possibleLikelihoods[i];
            Color cTemp = getColorTemperature(possibleTemperatures[i]);
            float xOrigin = segmentSpacing * historicalSegments + dotSpacing;
            float yPos = possibleTemperatures[i];
            for (int j = 0; j < nDots; j++)
            {
                //Debug.Log("d" + d);
                dots[d].index = i;
                dots[d].temperature = currentDot.temperature;
                dots[d].lineRender = predictionLines[i];
                SpriteRenderer dot = dots[d].spriteRender;
                dot.color = cTemp;
                //dot.transform.position = Vector3.Scale(new Vector3(xOrigin + dotSpacing * j, yPos, -1), transform.localScale) + transform.position;
                dot.transform.position = new Vector3(-99999, 99999, -1);
                d++;
            }
        }
    }

    Color getColorTemperature(float temp)
    {
        float mid = (minTemp + maxTemp) / 2;
        float div = maxTemp - mid;
        if (temp == mid)
        {
            return neutralColor;
        }
        else if (temp > mid)
        {
            return Color.Lerp(neutralColor, hotColor, (temp - mid) / div);
        }
        else
        {
            return Color.Lerp(coldColor, neutralColor, (temp - minTemp) / div);
        }
    }
}

class TempDot
{
    public SpriteRenderer spriteRender;
    public float temperature;
    public LineRenderer lineRender;
    public int index;

    public TempDot(SpriteRenderer sprender, float temp, LineRenderer liner, int arrIndex)
    {
        spriteRender = sprender;
        temperature = temp;
        lineRender = liner;
        index = arrIndex;
    }
}
