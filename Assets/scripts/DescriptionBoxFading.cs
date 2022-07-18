using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fades the UI element over a given duration. Intended to be used with the description box
/// shop UI element.
/// </summary>
public class DescriptionBoxFading : MonoBehaviour {

    public CanvasGroup canvasGroup;
    public float time;

    private bool isVisible = false;
    private bool isAnimating = false;

    private void Update()
    {
        if (isAnimating)
        {
            if (isVisible && canvasGroup.alpha < 1)
            {
                canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 1, time);
            }
            else if (!isVisible && canvasGroup.alpha > 0)
            {
                canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 0, time);
            }

            if (canvasGroup.alpha == 0 || canvasGroup.alpha == 1)
            {
                isAnimating = false;
            }
        }
    }

    public void SetIsVisible(bool isVisible)
    {
        this.isVisible = isVisible;
        isAnimating = true;
    }
}
