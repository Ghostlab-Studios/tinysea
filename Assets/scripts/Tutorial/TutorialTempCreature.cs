using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TutorialTempCreature : MonoBehaviour {

	public Image render;
	public RectTransform transform;
	public Sprite deadSprite;
	public Sprite sadSprite;
	public Sprite happySprite;

	public float xScale = 4.7f;
	public float yScale;
	private float xStart;
	private float yStart;

	void Start() {
		xStart = transform.anchoredPosition.x;
		yStart = transform.anchoredPosition.y;


	}

	public void UpdatePosition(float val) {
		// Change the sprite based on Quality of Life
		if (val <= 7 || val >= 38) {
			render.sprite = deadSprite;
		} else if (val <= 15 || val >= 30) {
			render.sprite = sadSprite;
		} else {
			render.sprite = happySprite;
		}

		//Update Position along graph
		float x = xStart + val*xScale;

		float y = yStart +
		          yScale - yScale * Mathf.Cos (val * 2 * Mathf.PI / 45);


		transform.anchoredPosition = new Vector2(x, y);
			
	}


}
