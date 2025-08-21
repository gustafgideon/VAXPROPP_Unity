using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class SimplePickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField] private bool canBePickedUp = true;

    private Rigidbody rb;
    private Collider col;

    private void Awake()
    {
        // Cache references instead of adding duplicates
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        // Optional: tweak Rigidbody for pickup objects
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
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