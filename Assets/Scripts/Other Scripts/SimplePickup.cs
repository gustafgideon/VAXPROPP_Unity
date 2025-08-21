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
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

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