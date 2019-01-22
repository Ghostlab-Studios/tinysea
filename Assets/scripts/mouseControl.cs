using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public class mouseControl : MonoBehaviour
{
    [SerializeField]
    GameObject nextButton, textBox;
    [SerializeField]
    GameObject[] interactionObjects;
    [SerializeField]
    string[] texts;
    Button[] tankButtons;
    Queue<string> textchecks;
    bool flag = false;
    int i = 0, j = 0;
    float[] value;

    private void Start()
    {
        i = 0; j = 0;
        value = new float[interactionObjects.Length - 1];
        textchecks = new Queue<string>();
        foreach(GameObject interactionObject in interactionObjects)
        {
            if (interactionObject.GetComponentInChildren<Slider>() != null)
            {
                value[i] = interactionObject.GetComponentInChildren<Slider>().value;
                i++;
            }
            else
            {
                tankButtons = interactionObject.GetComponentsInChildren<Button>();
            }
        }

        foreach(string check in texts)
        {
            textchecks.Enqueue(check);
        }
        nextButton.GetComponent<Button>().onClick.AddListener(check_status);
        i = 0;
    }

    private void Update()
    {
        if(Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current.IsPointerOverGameObject())
            {
               // Debug.Log("UI click");
            }
            else if (Input.GetMouseButtonDown(0) && nextButton.GetComponent<Button>().IsInteractable())
            {
                nextButton.GetComponent<Button>().onClick.Invoke();                
            }            
        }

        //if the textbox contains the check sentences then disable continue button and have interaction authenticated
        if (textchecks.Count != 0 && textBox.GetComponent<Text>().text.Contains(textchecks.Peek().ToString()))
        {
            if (textBox.GetComponent<Text>().text.Contains("Simulate") && !tankButtons[j].IsInteractable() && !flag)
            {
                nextButton.GetComponent<Button>().interactable = true;
                textchecks.Dequeue();
                flag = true;
                j++;
            }
            else if (interactionObjects[i].GetComponentInChildren<Slider>() != null && interactionObjects[i].GetComponentInChildren<Slider>().value != value[i] && !flag)
            {
                nextButton.GetComponent<Button>().interactable = true;
                textchecks.Dequeue();
                flag = true;
                i++;
            }
        }
    }

    void check_status()
    {
        if (textchecks.Count != 0 && textBox.GetComponent<Text>().text.Contains(textchecks.Peek().ToString()))
        {
            nextButton.GetComponent<Button>().interactable = false;
            flag = false;
        }
    }
}
