using UnityEngine;

public class TriggerAmbiance : MonoBehaviour
{
    [Header("Trigger Settings")]
    [SerializeField] private Location targetAmbiance;
    [SerializeField] private string playerTag = "Player";
    
    [Header("Exit Behavior")]
    [SerializeField] private bool useOnTriggerExit = false;
    [SerializeField] private Location exitAmbiance = Location.Forest; // Changed to Forest instead of Outside
    
    [Header("Parameters")]
    [SerializeField] private bool setParameterOnEnter = false;
    [SerializeField] private string parameterName = "";
    [SerializeField] private float parameterValue = 0f;
    
    [SerializeField] private bool setParameterOnExit = false;
    [SerializeField] private string exitParameterName = "";
    [SerializeField] private float exitParameterValue = 0f;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            // Change ambiance
            AmbianceManager.Instance.ChangeAmbiance(targetAmbiance);
            
            // Set parameter if enabled
            if (setParameterOnEnter && !string.IsNullOrEmpty(parameterName))
            {
                AmbianceManager.Instance.SetParameter(targetAmbiance, parameterName, parameterValue);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!useOnTriggerExit || !other.CompareTag(playerTag))
            return;

        // Change to exit ambiance
        AmbianceManager.Instance.ChangeAmbiance(exitAmbiance);
        
        // Set exit parameter if enabled
        if (setParameterOnExit && !string.IsNullOrEmpty(exitParameterName))
        {
            AmbianceManager.Instance.SetParameter(exitAmbiance, exitParameterName, exitParameterValue);
        }
    }
}