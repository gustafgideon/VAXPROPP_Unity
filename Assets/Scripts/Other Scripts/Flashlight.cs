using UnityEngine;
using UnityEngine.InputSystem;

public class Flashlight : MonoBehaviour
{
    [Header("Flashlight Settings")]
    public Light flashlight;
    public Key toggleKey = Key.E;

    [Header("Audio")]
    public PlayerAudio playerAudio;
    

    void Start()
    {
        if (flashlight == null)
            flashlight = GetComponent<Light>();

        flashlight.enabled = false;
    }

    void Update()
    {
        if (Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            flashlight.enabled = !flashlight.enabled;
            playerAudio.PlayerFlashlightButtonAudio(gameObject);
        }
    }
}