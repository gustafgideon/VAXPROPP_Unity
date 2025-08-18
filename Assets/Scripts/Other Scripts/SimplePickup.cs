using UnityEngine;

public class SimplePickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField] private bool canBePickedUp = true;
    
    private void Start()
    {
        // Ensure the object has a Rigidbody
        if (GetComponent<Rigidbody>() == null)
        {
            gameObject.AddComponent<Rigidbody>();
        }
        
        // Ensure the object has a Collider
        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<BoxCollider>();
        }
    }
    
    public bool CanBePickedUp()
    {
        return canBePickedUp;
    }
    
    public void SetPickupable(bool pickupable)
    {
        canBePickedUp = pickupable;
    }
}