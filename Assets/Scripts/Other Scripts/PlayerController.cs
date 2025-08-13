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

    [Header("Third Person Camera Settings")]
    [SerializeField] private float thirdPersonDistance = 5f;
    [SerializeField] private float minVerticalAngle = -30f;
    [SerializeField] private float maxVerticalAngle = 60f;
    [SerializeField] private float cameraCollisionOffset = 0.2f;
    [SerializeField] private Vector3 thirdPersonOffset = new Vector3(0f, 1.5f, 0f);
    
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float gravityMultiplier = 2.5f;
    [SerializeField] private float airControl = 0.8f;
    [SerializeField] private float crouchSpeed = 3f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float standingHeight = 2f;
    
    [Header("Head Bob Settings")]
    [SerializeField] private float bobFrequency = 2f;
    [SerializeField] private float bobAmount = 0.05f;
    
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
    private bool isFirstPerson = false;
    private float targetFOV;
    private float currentAttackCooldown;
    private bool isCrouching;
    private Vector3 originalCameraPosition;
    private float bobTimer;

    // Input action references
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction runAction;
    private InputAction zoomAction;
    private InputAction attackAction;
    private InputAction switchViewAction;
    private InputAction interactAction;
    private InputAction crouchAction;

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
        originalCameraPosition = cameraHolder.localPosition;
        targetFOV = normalFOV;
        
        characterController.height = standingHeight;
        
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
        crouchAction = actions["Crouch"];

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
        crouchAction.performed += _ => ToggleCrouch();
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
                            zoomAction, attackAction, switchViewAction, interactAction, crouchAction };
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
        if (isFirstPerson) UpdateHeadBob();
        
        if (currentAttackCooldown > 0)
            currentAttackCooldown -= Time.deltaTime;
    }

    private void UpdateMovement()
    {
        isGrounded = characterController.isGrounded;
        
        Vector3 moveDir = transform.right * moveInput.x + transform.forward * moveInput.y;
        float currentSpeed = isCrouching ? crouchSpeed : (isRunning ? runSpeed : walkSpeed);
        
        if (!isGrounded)
        {
            moveDir *= airControl;
            currentVelocity.y += Physics.gravity.y * gravityMultiplier * Time.deltaTime;
        }
        else
        {
            if (currentVelocity.y < 0)
                currentVelocity.y = -2f;
        }
        
        Vector3 movement = moveDir * currentSpeed * Time.deltaTime;
        movement.y = currentVelocity.y * Time.deltaTime;
        
        characterController.Move(movement);
    }

    private void UpdateCameraRotation()
    {
        transform.Rotate(Vector3.up * lookInput.x * mouseSensitivity);
        
        verticalRotation -= lookInput.y * mouseSensitivity;
        
        if (isFirstPerson)
        {
            verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
            cameraHolder.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
        }
        else
        {
            verticalRotation = Mathf.Clamp(verticalRotation, minVerticalAngle, maxVerticalAngle);
        }
    }

    private void UpdateCameraPosition()
    {
        if (!isFirstPerson)
        {
            Vector3 targetPosition = transform.position + thirdPersonOffset;
            Vector3 directionToCamera = Quaternion.Euler(verticalRotation, transform.eulerAngles.y, 0f) 
                * -Vector3.forward;
            Vector3 desiredCameraPos = targetPosition + directionToCamera * thirdPersonDistance;

            if (Physics.Raycast(targetPosition, directionToCamera, out RaycastHit hit, 
                thirdPersonDistance))
            {
                desiredCameraPos = hit.point + directionToCamera * cameraCollisionOffset;
            }

            cameraTransform.position = Vector3.Lerp(cameraTransform.position, 
                desiredCameraPos, Time.deltaTime * cameraTransitionSpeed);

            Vector3 lookAtPosition = transform.position + thirdPersonOffset;
            Vector3 smoothedLookAt = Vector3.Lerp(cameraTransform.position + 
                cameraTransform.forward, lookAtPosition, Time.deltaTime * cameraTransitionSpeed);
            cameraTransform.LookAt(smoothedLookAt);
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

    private void UpdateHeadBob()
    {
        if (isGrounded && moveInput.magnitude > 0.1f)
        {
            float speed = isRunning ? runSpeed : walkSpeed;
            bobTimer += Time.deltaTime * bobFrequency * (speed / walkSpeed);
            
            Vector3 bobOffset = new Vector3(
                Mathf.Cos(bobTimer) * bobAmount,
                Mathf.Sin(bobTimer * 2) * bobAmount,
                0
            );
            
            cameraHolder.localPosition = originalCameraPosition + bobOffset;
        }
        else
        {
            bobTimer = 0;
            cameraHolder.localPosition = Vector3.Lerp(
                cameraHolder.localPosition,
                originalCameraPosition,
                Time.deltaTime * 5f
            );
        }
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
        
        if (isFirstPerson)
        {
            verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
        }
        else
        {
            verticalRotation = 0f;
        }
    }

    private void ToggleCrouch()
    {
        isCrouching = !isCrouching;
        characterController.height = isCrouching ? crouchHeight : standingHeight;
        
        if (isFirstPerson)
        {
            float heightDifference = standingHeight - crouchHeight;
            Vector3 newCameraPosition = originalCameraPosition;
            newCameraPosition.y -= isCrouching ? heightDifference * 0.5f : -heightDifference * 0.5f;
            cameraHolder.localPosition = Vector3.Lerp(
                cameraHolder.localPosition,
                newCameraPosition,
                Time.deltaTime * 10f
            );
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (playerCamera != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(playerCamera.transform.position, interactionRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(playerCamera.transform.position, attackRange);
        }
    }
}

public interface IInteractable
{
    void Interact();
}

public interface IDamageable
{
    void TakeDamage(int amount);
}
