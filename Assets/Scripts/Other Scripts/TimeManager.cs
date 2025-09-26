using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
 
public class TimeManager : MonoBehaviour
{
    [Header("Time Speed Settings")]
    [SerializeField] [Range(0.1f, 100.0f)] private float timeSpeedMultiplier = 1.0f;

    [Header("Time Phase Hours")]
    [SerializeField] [Range(0, 23)] private int sunriseHour = 6;
    [SerializeField] [Range(0, 23)] private int dayHour = 8;
    [SerializeField] [Range(0, 23)] private int sunsetHour = 18;
    [SerializeField] [Range(0, 23)] private int nightHour = 22;

    [Header("Skybox Textures")]
    [SerializeField] private Texture2D skyboxNight;
    [SerializeField] private Texture2D skyboxSunrise;
    [SerializeField] private Texture2D skyboxDay;
    [SerializeField] private Texture2D skyboxSunset;

    [Header("Light Gradients")]
    [SerializeField] private Gradient graddientNightToSunrise;
    [SerializeField] private Gradient graddientSunriseToDay;
    [SerializeField] private Gradient graddientDayToSunset;
    [SerializeField] private Gradient graddientSunsetToNight;

    [Header("Global Light")]
    [SerializeField] private Light globalLight;
 
    private int minutes;
 
    public int Minutes
    { get { return minutes; } set { minutes = value; OnMinutesChange(value); } }
 
    private int hours = 5;
 
    public int Hours
    { get { return hours; } set { hours = value; OnHoursChange(value); } }
 
    private int days;
 
    public int Days
    { get { return days; } set { days = value; } }
 
    private float tempSecond;
 
    public void Update()
    {
        tempSecond += Time.deltaTime * timeSpeedMultiplier;

        if (tempSecond >= 1)
        {
            Minutes += 1;
            tempSecond = 0;
        }
    }
 
    private void OnMinutesChange(int value)
    {
        globalLight.transform.Rotate(Vector3.up, (1f / (1440f / 4f)) * 360f, Space.World);
        if (value >= 60)
        {
            Hours++;
            minutes = 0;
        }
        if (Hours >= 24)
        {
            Hours = 0;
            Days++;
        }
    }
 
    private void OnHoursChange(int value)
    {
        if (value == sunriseHour)
        {
            StartCoroutine(LerpSkybox(skyboxNight, skyboxSunrise, 1.0f));
            StartCoroutine(LerpLight(graddientNightToSunrise, 1.0f));
        }
        else if (value == dayHour)
        {
            StartCoroutine(LerpSkybox(skyboxSunrise, skyboxDay, 1.0f));
            StartCoroutine(LerpLight(graddientSunriseToDay, 1.0f));
        }
        else if (value == sunsetHour)
        {
            StartCoroutine(LerpSkybox(skyboxDay, skyboxSunset, 1.0f));
            StartCoroutine(LerpLight(graddientDayToSunset, 1.0f));
        }
        else if (value == nightHour)
        {
            StartCoroutine(LerpSkybox(skyboxSunset, skyboxNight, 1.0f));
            StartCoroutine(LerpLight(graddientSunsetToNight, 1.0f));
        }
    }
 
    private IEnumerator LerpSkybox(Texture2D a, Texture2D b, float time)
    {
        RenderSettings.skybox.SetTexture("_Texture1", a);
        RenderSettings.skybox.SetTexture("_Texture2", b);
        RenderSettings.skybox.SetFloat("_Blend", 0);
        for (float i = 0; i < time; i += Time.deltaTime)
        {
            RenderSettings.skybox.SetFloat("_Blend", i / time);
            yield return null;
        }
        RenderSettings.skybox.SetTexture("_Texture1", b);
    }
 
    private IEnumerator LerpLight(Gradient lightGradient, float time)
    {
        for (float i = 0; i < time; i += Time.deltaTime)
        {
            globalLight.color = lightGradient.Evaluate(i / time);
            RenderSettings.fogColor = globalLight.color;
            yield return null;
        }
    }
}