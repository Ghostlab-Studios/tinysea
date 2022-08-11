using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrowAnimator : MonoBehaviour
{
    public float speed;
    public Vector2 changeInDistance;

    new private RectTransform transform;
    private Vector2 originalPosition;
    private Vector2 endPosition;
    private Vector2 targetPosition;
    private float epsilon = 0.1f;

	void Start()
    {
        transform = GetComponent<RectTransform>();
        originalPosition = transform.anchoredPosition;
        endPosition = originalPosition + new Vector2(changeInDistance.x, changeInDistance.y);
        targetPosition = endPosition;
	}
	
	void Update()
    {
        float newPosX = Mathf.Lerp(transform.anchoredPosition.x, targetPosition.x, speed);
        float newPosY = Mathf.Lerp(transform.anchoredPosition.y, targetPosition.y, speed);
        Vector2 newPos = new Vector2(newPosX, newPosY);
        transform.anchoredPosition = newPos;
        if (Mathf.Abs(transform.anchoredPosition.x - targetPosition.x) <= epsilon &&
            Mathf.Abs(transform.anchoredPosition.y - targetPosition.y) <= epsilon)
        {
            targetPosition = targetPosition == originalPosition ? endPosition : originalPosition;
        }
	}
}
