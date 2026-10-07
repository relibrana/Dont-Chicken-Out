using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class StartGame : MonoBehaviour
{
    public GameObject player1;
    public GameObject player2;
    public Canvas startCanvas;
    public Canvas timer;
    public TextMeshProUGUI player1Text;
    public TextMeshProUGUI player2Text;
    private bool player1text;
    private bool player2text;
    private void Start(){
        player1text=false;
        player2text=false;
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F) && player1text==false){
            AudioManager.Instance.PlaySound("select");
            player1Text.SetText("Ready");
            player1text=true;
            player1.SetActive(true);

        }
            
        

        if (Input.GetKeyDown(KeyCode.RightShift) && player2text==false){
            AudioManager.Instance.PlaySound("select");
            player2Text.SetText("Ready");
            player2text=true;
            player2.SetActive(true);

        }

        if (player1text && player2text){
            startCanvas.gameObject.SetActive(false);
            timer.gameObject.SetActive(true);

            
            
        }
        
    }

    private void LowerOpacity(TextMeshProUGUI textMeshPro)
    {
        Color currentColor = textMeshPro.color;
        currentColor.a = Mathf.Max(0f, currentColor.a * 0.5f); // Reducir la opacidad a la mitad
        textMeshPro.color = currentColor;
    }   
}
