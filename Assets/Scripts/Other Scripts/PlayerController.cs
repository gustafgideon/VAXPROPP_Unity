   using UnityEngine;
using UnityEngine.InputSystem;
using System;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    [Header("Camera Setup")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform firstPersonPosition;
    [SerializeField] private Transform thirdPersonPosition;
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private float cameraTransitionSpeed = 10f;
    
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float gravityMultiplier = 2.5f;
    
    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private LayerMask interactionMask;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackCooldown = 0.5f;
    [SerializeField] private int attackDamage = 10;

    [Header("View Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 80f;
    [SerializeField] private float normalFOV = 60f;
    [SerializeField] private float zoomFOV = 30f;
    [SerializeField] private float zoomSpeed = 20f;

    // Private references
    private CharacterController characterController;
    private PlayerInput playerInput;
    private Transform cameraTransform;
    
    // Movement state
    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector3 currentVelocity;
    private float verticalRotation;
    private bool isGrounded;
    private bool isRunning;
    private bool isZooming;
    private bool isFirstPerson = true;
    private float targetFOV;
    private float currentAttackCooldown;

    // Input action references
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction runAction;
    private InputAction zoomAction;
    private InputAction attackAction;
    private InputAction switchViewAction;
    private InputAction interactAction;

    private void Awake()
    {
        InitializeComponents();
        SetupInputActions();
    }

    private void InitializeComponents()
    {
        characterController = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();
        
        if (playerCamera == null)
            playerCamera = Camera.main;
            
        cameraTransform = playerCamera.transform;
        targetFOV = normalFOV;
        
        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void SetupInputActions()
    {
        var actions = playerInput.actions;
        moveAction = actions["Move"];
        lookAction = actions["Look"];
        jumpAction = actions["Jump"];
        runAction = actions["Run"];
        zoomAction = actions["Zoom"];
        attackAction = actions["Attack"];
        switchViewAction = actions["SwitchView"];
        interactAction = actions["Interact"];

        // Subscribe to input events
        moveAction.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        moveAction.canceled += ctx => moveInput = Vector2.zero;
        
        lookAction.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        lookAction.canceled += ctx => lookInput = Vector2.zero;
        
        runAction.performed += ctx => isRunning = true;
        runAction.canceled += ctx => isRunning = false;
        
        zoomAction.performed += ctx => isZooming = true;
        zoomAction.canceled += ctx => isZooming = false;
        
        jumpAction.performed += _ => TryJump();
        attackAction.performed += _ => TryAttack();
        interactAction.performed += _ => TryInteract();
        switchViewAction.performed += _ => ToggleView();
    }

    private void OnEnable()
    {
        EnableAllInputs(true);
    }

    private void OnDisable()
    {
        EnableAllInputs(false);
    }

    private void EnableAllInputs(bool enable)
    {
        var actions = new[] { moveAction, lookAction, jumpAction, runAction, 
                            zoomAction, attackAction, switchViewAction, interactAction };
        foreach (var action in actions)
        {
            if (action != null)
            {
                if (enable) action.Enable();
                else action.Disable();
            }
        }
    }

    private void Update()
    {
        UpdateMovement();
        UpdateCameraRotation();
        UpdateCameraPosition();
        UpdateCameraEffects();
        
        // Update cooldowns
        if (currentAttackCooldown > 0)
            currentAttackCooldown -= Time.deltaTime;
    }

    private void UpdateMovement()
    {
        isGrounded = characterController.isGrounded;
        
        // Calculate movement direction
        var moveDir = transform.right * moveInput.x + transform.forward * moveInput.y;
        float currentSpeed = isRunning ? runSpeed : walkSpeed;
        
        // Apply movement and gravity
        if (isGrounded && currentVelocity.y < 0)
            currentVelocity.y = -2f;
            
        currentVelocity.y += Physics.gravity.y * gravityMultiplier * Time.deltaTime;
        
        Vector3 movement = moveDir * currentSpeed * Time.deltaTime;
        movement.y = currentVelocity.y * Time.deltaTime;
        
        characterController.Move(movement);
    }

    private void UpdateCameraRotation()
    {
        // Horizontal rotation
        transform.Rotate(Vector3.up * lookInput.x * mouseSensitivity);
        
        // Vertical rotation
        verticalRotation -= lookInput.y * mouseSensitivity;
        verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
        cameraHolder.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
    }

    private void UpdateCameraPosition()
    {
        if (!isFirstPerson)
        {
            // Check for obstacles between camera and player in third person
            Vector3 targetPos = thirdPersonPosition.position;
            if (Physics.Raycast(transform.position, (targetPos - transform.position).normalized, 
                out RaycastHit hit, Vector3.Distance(transform.position, targetPos)))
            {
                cameraTransform.position = hit.point;
            }
            else
            {
                cameraTransform.position = Vector3.Lerp(cameraTransform.position, 
                    targetPos, Time.deltaTime * cameraTransitionSpeed);
            }
        }
        else
        {
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, 
                firstPersonPosition.position, Time.deltaTime * cameraTransitionSpeed);
        }
    }

    private void UpdateCameraEffects()
    {
        targetFOV = isZooming ? zoomFOV : normalFOV;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, 
            Time.deltaTime * zoomSpeed);
    }

    private void TryJump()
    {
        if (isGrounded)
        {
            currentVelocity.y = jumpForce;
        }
    }

    private void TryAttack()
    {
        if (currentAttackCooldown <= 0)
        {
            if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, 
                out RaycastHit hit, attackRange))
            {
                if (hit.collider.TryGetComponent(out IDamageable target))
                {
                    target.TakeDamage(attackDamage);
                }
            }
            currentAttackCooldown = attackCooldown;
        }
    }

    private void TryInteract()
    {
        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, 
            out RaycastHit hit, interactionRange, interactionMask))
        {
            if (hit.collider.TryGetComponent(out IInteractable interactable))
            {
                interactable.Interact();
            }
        }
    }

    private void ToggleView()
    {
        isFirstPerson = !isFirstPerson;
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize interaction and attack ranges in editor
        if (playerCamera != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(playerCamera.transform.position, interactionRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(playerCamera.transform.position, attackRange);
        }
    }
}

// Required interfaces
public interface IInteractable
{
    void Interact();
}

public interface IDamageable
{
    void TakeDamage(int amount);
}