using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FMOD.Studio;
using FMODUnity;

public enum Location  
{  
   Forest, Factory
}

public class AmbianceManager : MonoBehaviour
{
   public static AmbianceManager Instance { get; private set; }

   [Header("Ambiance Emitter")]
   [SerializeField] private StudioEventEmitter forestAmbianceEmitter;
   [SerializeField] private StudioEventEmitter factoryAmbianceEmitter;

   [Header("Fade Settings")]
   [SerializeField] private float fadeTime = 2f; // How long the fade takes

   private StudioEventEmitter emitter;
   private StudioEventEmitter currentlyPlaying; // Keep track of what's playing

   private void Awake()
   {
      if (Instance != null && Instance != this) 
      {
         Destroy(this);
      }
      else
      {
         Instance = this;
      }
      DontDestroyOnLoad(this);
   }

   private void Update()
   {
      transform.position = GameObject.FindWithTag("Player").transform.position;
   }

   private void GetLocation(Location location)
   {
      switch (location)
      {
         case Location.Forest:
            emitter = forestAmbianceEmitter;
            break;
         case Location.Factory:
            emitter = factoryAmbianceEmitter;
            break;
      }
   }

   // New method that smoothly changes between ambiances
   public void ChangeAmbiance(Location newLocation)
   {
      GetLocation(newLocation);
      
      // If something is already playing, fade it out and fade in the new one
      if (currentlyPlaying != null && currentlyPlaying != emitter)
      {
         StartCoroutine(CrossFade(currentlyPlaying, emitter));
      }
      else
      {
         // Nothing playing, just fade in the new one
         StartCoroutine(FadeIn(emitter));
      }
      
      currentlyPlaying = emitter;
   }

   // Fade out old sound and fade in new sound at the same time
   private IEnumerator CrossFade(StudioEventEmitter oldEmitter, StudioEventEmitter newEmitter)
   {
      // Start the new sound
      if (!newEmitter.IsActive)
         newEmitter.Play();

      float timer = 0f;
      
      while (timer < fadeTime)
      {
         timer += Time.deltaTime;
         float progress = timer / fadeTime; // Goes from 0 to 1
         
         // Fade out old sound (volume goes from 1 to 0)
         oldEmitter.EventInstance.setVolume(1f - progress);
         
         // Fade in new sound (volume goes from 0 to 1)
         newEmitter.EventInstance.setVolume(progress);
         
         yield return null; // Wait one frame
      }
      
      // Make sure volumes are exactly right at the end
      oldEmitter.EventInstance.setVolume(0f);
      newEmitter.EventInstance.setVolume(1f);
      
      // Stop the old sound
      oldEmitter.Stop();
   }

   // Just fade in a sound from silence
   private IEnumerator FadeIn(StudioEventEmitter targetEmitter)
   {
      if (!targetEmitter.IsActive)
         targetEmitter.Play();

      float timer = 0f;
      
      while (timer < fadeTime)
      {
         timer += Time.deltaTime;
         float progress = timer / fadeTime; // Goes from 0 to 1
         
         targetEmitter.EventInstance.setVolume(progress);
         
         yield return null;
      }
      
      targetEmitter.EventInstance.setVolume(1f);
   }

   // Your old methods still work the same way
   public void PlayAudio(Location location)
   {
      GetLocation(location);
      if (!emitter.IsActive)
           emitter.Play();
   }

   public void StopAudio(Location location)
   {
      GetLocation(location);
      if (emitter.IsActive)
          emitter.Stop();
   }

   public void SetParameter(Location location, string parameterName, float parameterValue)
   {
      GetLocation(location);
      if (emitter.IsActive)
          emitter.SetParameter(parameterName, parameterValue);
   }
}