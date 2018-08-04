using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TemperatureTrend : MonoBehaviour
{

    /// <summary>
    /// This script is designed to calculate a temperature based on the current day
    /// The temperature is based on three conditions: The warming trend of the ocean [var trend], 
    /// 												the changing seasons [var season], 
    /// 												and the growing instability of the temperature [var sd] 
    /// The first two conditions are based on simple linear functions, and the sum of which represent the desired average temperature [var mean]
    /// The instability/standard deviation grows linearly from .3 - 8 (These two values will be represented by [var sdMin, sdMax])
    /// The final temperature is derived from a gaussian random sampling, using the expected average temperature and the standard deviation
    /// ---------------------------------------------------------------------------------------------------------------------------------------
    /// 	: Notes on this code:
    /// 		- There isn't rounding of the final value to a desired decimal point
    /// 		- The math here uses floats, which have memory restrictions: may not create the minutia that is desired
    /// 		- Because the season equation has no horizontal translation, the game will start with temperatures around 25 degrees, rather than
    /// 				20 degrees right down the middle 
    /// 		- This code uses the Box-Muller method of randomization. The Ziggurat Algorithm is technically cheaper, 
    /// 				but it shouldn't make a difference
    /// 		- The name of this class is really awful I'm sorry
    /// </summary>


    // the number of cycles or days it will take to get to the end of the desired temperature plot
    public float cycleTurnCount;
    // the current modifier signifying the trend of warming oceans
    private float trend;
    // the current modifier signifying the season 
    private float season;
    // the temperature mean without randomness
    private float mean;
    // the standard deviation for Gaussian sampling on this day 
    private float sd;
    // the temperature corresponding to the current day
    private float finalTemperature;


    // amplitude and frequency for season signal 
    private float a = 5f;
    private float f = 2f;

    // minimum and maximum for standard deviation
    private float sdMin = 0.3f;
    private float sdMax = 8f; // This may be a bit too high, at least with this code

    // boolean that flips every time Gaussian is called to add randomness: determines which equation is used to derive the random number
    // this process may be unnecessary, and is usually used when deriving larger data sets with the same standard deviation
    private bool gaussFlipper; //This is a bad name for this variable but honestly, what would you call it?


    // gets random temperature given the current day
    public float GetTemperature(float day)
    {

        SetTrend(day);
        SetSeason(day);
        SetSigma(day);

        mean = trend + season;

        finalTemperature = Mathf.Clamp(GetGaussian(mean, sd), 0f, 40f);

        return finalTemperature;

    }

    // sets warming trend for the given day
    void SetTrend(float day)
    {
        trend = 10f * day / cycleTurnCount;
    }

    // sets seasonal temperature modifier for the given day 
    void SetSeason(float day)
    {
        season = a * Mathf.Cos(2f * Mathf.PI * f * (day / cycleTurnCount));
    }

    // sets standard deviation for the given day
    void SetSigma(float day)
    {
        sd = sdMin + ((sdMax - sdMin) / cycleTurnCount) * day;

    }

    // returns a random sampling from a Gaussian distribution of the given mean and standard deviation
    // this function uses the Box-muller algorithm. The Ziggurat Algorithm is cheaper, but it shouldn't make a difference 
    public float GetGaussian(float mu, float sigma)
    {
        gaussFlipper = !gaussFlipper;
        // the two random numbers used to create the gaussian sampling
        float u1, u2;
        // the final normal random value
        float z;

        do
        {
            u1 = Random.value;
            u2 = Random.value;
        } while (u1 <= Mathf.Epsilon); // This makes sure that u1 isn't absurdly small or 0, which will make the following ln function sad

        if (gaussFlipper)
        {
            z = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        }
        else
        {
            z = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Sin(2f * Mathf.PI * u2);
        }

        return z * sigma + mu;

    }

}
