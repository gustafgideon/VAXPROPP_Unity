using UnityEngine;
using UnityEngine.InputSystem.Interactions;

public class Pickup : MonoBehaviour
{
    private bool isHolding = false;

    [SerializeField] 
    private float throwForce = 600f;
    [SerializeField]
    private float maxDistance = 3f;
    private float distance;

    TempParent tempParent;
    Rigidbody rb;

    Vector3 objectPos;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        tempParent = TempParent.Instance;
        
    }

    // Update is called once per frame
    void Update()
    {
        if (isHolding)
            Hold();
    }


    private void OnMouseOver()
    {
        //pickup
        if (tempParent != null)
        {
            if (Input.GetKeyDown(KeyCode.F) && !isHolding)
            {
                
            
                distance = Vector3.Distance(this.transform.position, tempParent.transform.position);
                if (distance >= maxDistance)
                {
                isHolding = true;
                rb.useGravity = false;
                rb.detectCollisions = true;
                           
                this.transform.SetParent(tempParent.transform); 
                }
            }
            
            else if (Input.GetKeyDown(KeyCode.F) && isHolding)
            {
                Drop();
            }
        }
        else
        {
            Debug.Log("Temp Parent item not found in scene");
        }
    }

    private void OnMouseUp()
    {
        Drop();
    }

    private void OnMouseExit()
    {
        Drop();
    }

    private void Hold()
    {
        distance = Vector3.Distance(this.transform.position, tempParent.transform.position);
        if (distance >= maxDistance)
        {
            Drop();
        }
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (Input.GetMouseButton(1))
        {
            rb.AddForce(tempParent.transform.forward * throwForce);
            Drop();
        }
    }

    private void Drop()
    {
        if (isHolding)
        {
            isHolding = false;
            objectPos = this.transform.position;
            this.transform.position = objectPos;
            this.transform.SetParent(null);
            rb.useGravity = true;
        }
    }
}
