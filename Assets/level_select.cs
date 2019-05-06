using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class level_select : MonoBehaviour
{
    public Sprite[] env_sprites;
    public GameObject select;

    private Vector3 normal_scale, normal_scale2;
    private GameObject env_image;
    private int current; //current image index in the sprite array
    //private Queue<Sprite> env;

    public void Start()
    {
        current = 0;
        PlayerPrefs.SetInt("env", current); //set default level

        env_image = new GameObject();
        foreach (Transform child in transform)
            if (child.gameObject.name == "level")
            {
                env_image = child.gameObject;
                env_image.GetComponent<Image>().sprite = env_sprites[0];
                break;
            }

        normal_scale = gameObject.transform.localScale;
        normal_scale2 = select.transform.localScale;
    }

    public void act_select()
    {
        hover(false);
        Image select = gameObject.GetComponent<Image>();
        Button butt = gameObject.GetComponent<Button>();

        select.enabled = false;
        butt.enabled = false;

        foreach(Transform child in transform)
        {
            child.gameObject.SetActive(true);
        }
    }

    public void deact_select()
    {
        PlayerPrefs.SetInt("env", current); //select current environment then close menu

        Image select = gameObject.GetComponent<Image>();
        Button butt = gameObject.GetComponent<Button>();

        select.enabled = true;
        butt.enabled = true;

        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(false);
        }

        hover(true);
    }

    public void press_arrow(bool up)
    {
        shuffle_env(up);
    }

    private void shuffle_env(bool move)
    {
        Sprite next_sprite;

        if (move)
            if(current < (env_sprites.Length - 1) )
            {
                next_sprite = env_sprites[++current];
            }
            else
            {
                current = 0;
                next_sprite = env_sprites[current];
            }
        else
            if(current > 0)
            {
                next_sprite = env_sprites[--current];
            }
            else
            {
                current = env_sprites.Length - 1;
                next_sprite = env_sprites[current];
            }

        env_image.GetComponent<Image>().sprite = next_sprite;
    }

    public void hover(bool on)
    {

        if(on)
        {
            if (gameObject.GetComponent<Image>().IsActive())
            {
                gameObject.transform.localScale = normal_scale * 1.2f;
                gameObject.GetComponent<Image>().color = Color.gray;
            }
            else if(select.GetComponent<Image>().IsActive())
            {
                select.transform.localScale = normal_scale2 * 1.2f;
                select.GetComponent<Image>().color = Color.gray;
            }
        }
        else
        {
            if (gameObject.GetComponent<Image>().IsActive())
            {
                gameObject.transform.localScale = normal_scale;
                gameObject.GetComponent<Image>().color = Color.white;
            }
            else if (select.GetComponent<Image>().IsActive())
            {
                select.transform.localScale = normal_scale2;
                select.GetComponent<Image>().color = Color.white;
            }
        }
    }
}
