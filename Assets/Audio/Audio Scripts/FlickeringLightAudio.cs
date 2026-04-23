using UnityEngine;
using FMODUnity;

[CreateAssetMenu(menuName = "Scriptable Objects/FlickeringLightAudio")]

public class FlickeringLightAudio : ScriptableObject
{
    [SerializeField]
    private EventReference lightFlickering;

    public void LightFlickeringAudio(Transform lightFlickeringTransform)
    {
        RuntimeManager.PlayOneShotAttached(lightFlickering, lightFlickeringTransform.gameObject);
    }
}
