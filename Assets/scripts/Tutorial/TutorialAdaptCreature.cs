using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TutorialAdaptCreature : MonoBehaviour {

	public Image render;
	public RectTransform transform;
	public Sprite deadSprite;
	public Sprite sadSprite;
	public Sprite happySprite;
	public float minTemp;
	public float xScale = 4.7f;
	public float yScale;

	private float xStart;
	private float yStart;
	private float maxTemp;
	private float width = 20f;

	void Start() {
		xStart = transform.anchoredPosition.x;
		yStart = transform.anchoredPosition.y;

		maxTemp = minTemp + width;

	}

	public void UpdatePosition(float val) {
		// Change the sprite based on Quality of Life
		if (val <= minTemp + 3 || val >= maxTemp - 3) {
			render.sprite = deadSprite;
		} else if (val <= minTemp + 6.5 || val >= maxTemp - 6.5) {
			render.sprite = sadSprite;
		} else {
			render.sprite = happySprite;
		}

		//Update Position along graph
		float clampVal = Mathf.Clamp(val, minTemp, maxTemp);

		float x = xStart + val*xScale;

		float y = yStart +
			yScale - yScale * Mathf.Cos ((clampVal- minTemp) * 2 * Mathf.PI / width );


		transform.anchoredPosition = new Vector2(x, y);

	}


}
