using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CloudScript : MonoBehaviour
{
	public float initialSpeed = 3;
	public float variationSeed = 1;


    void Start()
    {
        initialSpeed = initialSpeed + Random.Range (-variationSeed, variationSeed);
    }

    void Update()
    {
        transform.Translate (new Vector3 (-1f * initialSpeed * Time.deltaTime, 0, 0));
    }
}