using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System;
using System.Collections;

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

    [Header("Animation")]
    [SerializeField] private Animator animator;
    
    [Header("WoW-Style Camera Settings")]
    [SerializeField] private bool useWoWCameraStyle = true;
    [SerializeField] private float freeLookSensitivity = 2f;
    [SerializeField] private float mouseLookSensitivity = 2f;
    [SerializeField] private bool useRightMouseButton = true;
    [SerializeField] private bool enableLeftMouseCamera = true;
    [SerializeField] private float leftMouseCameraSensitivity = 1.5f;
    
    [Header("Camera Follow Settings")]
    [SerializeField] private bool enableCameraFollow = true;
    [SerializeField] private float cameraFollowSpeed = 2f;
    [SerializeField] private float cameraFollowDelay = 0.5f;
    [SerializeField] private float cameraFollowThreshold = 15f;
    [SerializeField] private bool onlyFollowWhenMoving = true;
    [SerializeField] private float movementFollowDelay = 0.3f;
    [SerializeField] private bool alwaysFollowBehindPlayer = true;
    [SerializeField] private float behindPlayerFollowSpeed = 3f;
    [SerializeField] private float minMovementForFollow = 0.1f;
    
    [Header("Third Person Camera Settings")]
    [SerializeField] private float thirdPersonDistance = 5f;
    [SerializeField] private float minVerticalAngle = -30f;
    [SerializeField] private float maxVerticalAngle = 60f;
    [SerializeField] private float cameraCollisionOffset = 0.2f;
    [SerializeField] private Vector3 thirdPersonOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private float minCameraDistance = 1f;
    [SerializeField] private float maxCameraDistance = 10f;
    [SerializeField] private float defaultCameraDistance = 5f;
    [SerializeField] private float zoomSensitivity = 1f;
    [SerializeField] private float zoomSpeed = 8f;
    [SerializeField] private bool invertScrollDirection = false;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float runSpeed = 7f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float jumpAnimationDelay = 0.08f;
    [SerializeField] private float jumpForwardForce = 3f;
    [SerializeField] private bool maintainJumpMomentum = true;
    [SerializeField] private float jumpMomentumMultiplier = 1.0f;
    [SerializeField] private float gravityMultiplier = 2.5f;
    [SerializeField] private float airControl = 0.8f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float standingHeight = 2f;
    [SerializeField] private float crouchTransitionSpeed = 8f;
    [SerializeField] private float movementAcceleration = 12f;
    [SerializeField] private float movementDeceleration = 15f;
    [SerializeField] private float backwardSpeedMultiplier = 0.6f;
    [SerializeField] private float strafeSpeedMultiplier = 0.8f;
    [SerializeField] private float backwardStrafeSpeedMultiplier = 0.5f;
    [SerializeField] private float pivotThreshold = 0.1f;

    [Header("Animation Settings")]
    [SerializeField] private float animationSmoothTime = 0.1f;
    [SerializeField] private float walkSpeedThreshold = 0.5f;
    [SerializeField] private float runSpeedThreshold = 0.8f;
    [SerializeField] private float crouchSpeedThreshold = 0.3f;
    [SerializeField] private float idleThreshold = 0.1f;
    [SerializeField] private float freeLookRotationSpeed = 2.4f;
    [SerializeField] private float rotationAngleThreshold = 5f;

    [Header("Head Bob Settings")]
    [SerializeField] private float bobFrequency = 2f;
    [SerializeField] private float bobAmount = 0.05f;
    [SerializeField] private float crouchBobFrequency = 1.5f;
    [SerializeField] private float crouchBobAmount = 0.03f;
    [SerializeField] private bool enableCrouchHeadBob = true;

    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private LayerMask interactionMask;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackCooldown = 0.5f;
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float pickupRange = 3f;
    [SerializeField] private float throwForce = 600f;
    [SerializeField] private LayerMask pickupMask = -1;
    [SerializeField] private float holdDistance = 1.5f;
    [SerializeField] private float holdPositionSpeed = 10f;
    [SerializeField] private float minThrowForce = 200f;
    [SerializeField] private float maxThrowForce = 1200f;
    [SerializeField] private float maxHoldTime = 1.5f;

    [Header("UI")]
    [SerializeField] private Image throwPowerBar;
    [SerializeField] private float eyeCloseTransitionDuration = 0.6f;
    [SerializeField] private Color eyeCloseColor = Color.black;

    [Header("Debug")]
    [SerializeField] private bool debugAnimationStates = false;
    [SerializeField] private bool debugMovementValues = false;
    [SerializeField] private bool debugCameraFollow = false;

    // Cached animation parameter hashes
    private static readonly int AnimParamSpeed = Animator.StringToHash("Speed");
    private static readonly int AnimParamIsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int AnimParamIsRunning = Animator.StringToHash("IsRunning");
    private static readonly int AnimParamIsCrouching = Animator.StringToHash("IsCrouching");
    private static readonly int AnimParamJump = Animator.StringToHash("Jump");
    private static readonly int AnimParamPunch = Animator.StringToHash("Punch");
    private static readonly int AnimParamKick = Animator.StringToHash("Kick");
    private static readonly int AnimParamPickup = Animator.StringToHash("Pickup");
    private static readonly int AnimParamDrop = Animator.StringToHash("Drop");
    private static readonly int AnimParamThrow = Animator.StringToHash("Throw");
    private static readonly int AnimParamInteract = Animator.StringToHash("Interact");
    private static readonly int AnimParamHorizontal = Animator.StringToHash("Horizontal");
    private static readonly int AnimParamVertical = Animator.StringToHash("Vertical");
    private static readonly int AnimParamIsStrafing = Animator.StringToHash("IsStrafing");
    private static readonly int AnimParamIsBackwardStrafing = Animator.StringToHash("IsBackwardStrafing");

    // Core components (cached once)
    private CharacterController characterController;
    private PlayerInput playerInput;
    private Transform cameraTransform;

    // UI Components
    private GameObject eyeCloseOverlay;
    private Image eyeCloseImage;

    // Input state
    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector2 smoothedLookInput;
    private Vector2 lookInputVelocity;

    // Movement state
    private Vector3 currentVelocity;
    private Vector3 targetVelocity;
    private float currentMovementSpeed;
    private bool isGrounded;
    private bool isRunning;
    private bool isMoving;
    private Vector3 jumpMomentumDirection;
    private float jumpMomentumSpeed;

    // Camera state
    private bool isFirstPerson = false;
    private float verticalRotation;
    private float horizontalRotation;
    private Vector2 freeLookRotation;
    private bool isMouseLookMode = false;
    private bool leftMouseCameraActive = false;
    private float currentCameraDistance;
    private float targetCameraDistance;

    // Animation state
    private float smoothedHorizontal;
    private float smoothedVertical;
    private float smoothedSpeed;
    private float horizontalVelocity;
    private float verticalVelocityAnim;
    private float speedVelocity;
    private bool isStrafing;
    private bool isBackwardStrafing;

    // Crouch state
    private bool isCrouching;
    private float targetHeight;
    private float currentCameraYOffset;
    private float targetCameraYOffset;
    private float originalControllerCenterY;
    private float targetControllerCenterY;

    // Jump state
    private bool isJumpQueued = false;
    private float jumpTimer = 0f;

    // Head bob state
    private float bobTimer;
    private Vector3 originalCameraPosition;

    // Interaction state
    private GameObject heldObject;
    private Rigidbody heldObjectRb;
    private bool isHoldingObject = false;
    private bool isChargingThrow = false;
    private float throwChargeTimer = 0f;
    private float currentAttackCooldown;

    // Camera follow state
    private float cameraFollowTimer = 0f;
    private float movementTimer = 0f;
    private bool wasMovingLastFrame = false;
    private float lastCharacterAngle = 0f;
    private bool shouldFollowCamera = false;
    private float currentFollowSpeed = 2f;

    // Transition state
    private bool isTransitioning = false;

    // Cached directions (updated only when needed)
    private Vector3 cachedCharacterForward;
    private Vector3 cachedCharacterRight;
    private Vector3 cachedCameraForward;
    private Vector3 cachedCameraRight;
    private bool directionsNeedUpdate = true;

    // Mouse state (cached)
    private UnityEngine.InputSystem.Mouse cachedMouse;
    private bool rightMousePressed = false;
    private bool leftMousePressed = false;

    // Input actions (cached)
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction runAction;
    private InputAction attackAction;
    private InputAction switchViewAction;
    private InputAction interactAction;
    private InputAction crouchAction;
    private InputAction zoomAction;
    private InputAction pickupAction;
    private InputAction throwAction;

    private void Awake()
    {
        InitializeComponents();
        SetupInputActions();
        CreateEyeCloseOverlay();
        InitializeState();
    }

    private void InitializeComponents()
    {
        // Cache components once
        characterController = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();
        
        if (playerCamera == null)
            playerCamera = Camera.main;
        
        cameraTransform = playerCamera.transform;
        
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        
        // Cache mouse reference
        cachedMouse = UnityEngine.InputSystem.Mouse.current;
    }

    private void InitializeState()
    {
        // Initialize cached values
        originalCameraPosition = cameraHolder.localPosition;
        originalControllerCenterY = characterController.center.y;
        targetControllerCenterY = originalControllerCenterY;
        
        characterController.height = standingHeight;
        targetHeight = standingHeight;
        
        horizontalRotation = transform.eulerAngles.y;
        verticalRotation = 0f;
        currentCameraDistance = defaultCameraDistance;
        targetCameraDistance = defaultCameraDistance;
        thirdPersonDistance = defaultCameraDistance;
        
        // WoW camera initialization
        if (useWoWCameraStyle && !isFirstPerson)
        {
            freeLookRotation = new Vector2(transform.eulerAngles.y, 0f);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void SetupInputActions()
    {
        var actions = playerInput.actions;
        moveAction = actions["Move"];
        lookAction = actions["Look"];
        jumpAction = actions["Jump"];
        runAction = actions["Run"];
        attackAction = actions["Attack"];
        switchViewAction = actions["SwitchView"];
        interactAction = actions["Interact"];
        crouchAction = actions["Crouch"];
        zoomAction = actions["Zoom"];
        pickupAction = actions["Pickup"];
        throwAction = actions["Throw"];

        // Bind input events
        moveAction.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        moveAction.canceled += ctx => moveInput = Vector2.zero;
        
        lookAction.performed += ctx => {
            if (!isTransitioning)
                lookInput = ctx.ReadValue<Vector2>();
        };
        lookAction.canceled += ctx => lookInput = Vector2.zero;
        
        runAction.performed += ctx => isRunning = true;
        runAction.canceled += ctx => isRunning = false;
        
        zoomAction.performed += ctx => HandleZoom(ctx.ReadValue<float>());
        jumpAction.performed += _ => TryJump();
        attackAction.performed += _ => TryAttack();
        interactAction.performed += _ => TryInteract();
        pickupAction.performed += _ => TryPickup();
        switchViewAction.performed += _ => {
            if (!isTransitioning) ToggleView();
        };
        
        throwAction.started += ctx => {
            if (isHoldingObject) StartChargingThrow();
        };
        throwAction.canceled += ctx => {
            if (isHoldingObject) ReleaseThrow();
        };
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
                            attackAction, switchViewAction, interactAction, crouchAction, 
                            zoomAction, pickupAction, throwAction };
        
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
        float deltaTime = Time.deltaTime;
        
        // Update in optimal order to minimize cache misses
        UpdateInput(deltaTime);
        UpdateMovement(deltaTime);
        
        if (!isTransitioning)
        {
            UpdateCamera(deltaTime);
        }
        
        UpdateAnimations(deltaTime);
        UpdateInteractions(deltaTime);
        UpdateUI(deltaTime);
        
        // Update cached directions only when needed
        if (directionsNeedUpdate)
        {
            UpdateCachedDirections();
        }
    }

    private void UpdateInput(float deltaTime)
    {
        // Smooth look input for better camera feel
        smoothedLookInput = Vector2.SmoothDamp(smoothedLookInput, lookInput, 
            ref lookInputVelocity, 1f / 15f);
    }

    private void UpdateMovement(float deltaTime)
    {
        // Ground check
        bool wasGrounded = isGrounded;
        isGrounded = characterController.isGrounded;
        
        if (isGrounded && !wasGrounded)
        {
            currentVelocity.y = -2f; // Stick to ground
        }
        
        // Crouch handling
        UpdateCrouchState();
        UpdateCrouchTransition(deltaTime);
        
        // Jump handling
        UpdateJumpState(deltaTime);
        
        // Movement calculation
        CalculateMovement(deltaTime);
        
        // Apply movement
        characterController.Move(currentVelocity * deltaTime);
        
        // Update movement state
        isMoving = moveInput.magnitude > idleThreshold;
        currentMovementSpeed = new Vector3(currentVelocity.x, 0, currentVelocity.z).magnitude;
    }

    private void CalculateMovement(float deltaTime)
{
    Vector3 moveDirection = Vector3.zero;
    float inputMagnitude = moveInput.magnitude;
    
    if (inputMagnitude > pivotThreshold)
    {
        // Calculate movement direction based on camera mode
        if (isFirstPerson)
        {
            moveDirection = cachedCharacterRight * moveInput.x + cachedCharacterForward * moveInput.y;
        }
        else if (useWoWCameraStyle)
        {
            if (isMouseLookMode)
            {
                moveDirection = cachedCameraRight * moveInput.x + cachedCameraForward * moveInput.y;
            }
            else
            {
                moveDirection = cachedCharacterRight * moveInput.x + cachedCharacterForward * moveInput.y;
                HandleFreeLookRotation(deltaTime);
            }
        }
        else
        {
            moveDirection = cachedCameraRight * moveInput.x + cachedCameraForward * moveInput.y;
        }
        
        moveDirection = Vector3.ClampMagnitude(moveDirection, 1f);
    }
    
    // Calculate target speed
    float targetSpeed = CalculateTargetSpeed(moveDirection);
    targetVelocity = moveDirection * targetSpeed;
    
    if (isGrounded)
    {
        // GROUNDED MOVEMENT
        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        Vector3 targetHorizontal = new Vector3(targetVelocity.x, 0f, targetVelocity.z);
        
        float acceleration = targetHorizontal.magnitude > horizontalVelocity.magnitude ? 
            movementAcceleration : movementDeceleration;
        
        horizontalVelocity = Vector3.Lerp(horizontalVelocity, targetHorizontal, acceleration * deltaTime);
        
        currentVelocity.x = horizontalVelocity.x;
        currentVelocity.z = horizontalVelocity.z;
        
        // Stick to ground
        if (currentVelocity.y < 0)
            currentVelocity.y = -2f;
    }
    else
    {
        // AIRBORNE MOVEMENT
        currentVelocity.y += Physics.gravity.y * gravityMultiplier * deltaTime;
        
        // Air control - only apply if there's input
        if (inputMagnitude > 0.1f)
        {
            Vector3 airTargetVelocity = moveDirection * targetSpeed * airControl;
            
            // Apply air control more gently to preserve jump momentum
            float airControlStrength = 2f; // How quickly you can change direction in air
            
            currentVelocity.x = Mathf.Lerp(currentVelocity.x, airTargetVelocity.x, airControlStrength * deltaTime);
            currentVelocity.z = Mathf.Lerp(currentVelocity.z, airTargetVelocity.z, airControlStrength * deltaTime);
            
            if (debugMovementValues && Time.frameCount % 10 == 0) // Reduce spam
            {
                Debug.Log($"Air control - Target: {airTargetVelocity}, Current: {new Vector3(currentVelocity.x, 0, currentVelocity.z)}");
            }
        }
    }
}

    private float CalculateTargetSpeed(Vector3 moveDirection)
    {
        float baseSpeed = isCrouching ? crouchSpeed : (isRunning ? runSpeed : walkSpeed);
        
        // Apply directional modifiers in third person
        if (!isFirstPerson && moveDirection.magnitude > 0.1f)
        {
            float forwardDot = Vector3.Dot(moveDirection.normalized, cachedCharacterForward);
            float rightDot = Vector3.Dot(moveDirection.normalized, cachedCharacterRight);
            
            // Pure backward movement
            if (forwardDot < -0.7f && Mathf.Abs(rightDot) < 0.3f)
                return baseSpeed * backwardSpeedMultiplier;
            
            // Pure strafe movement
            if (Mathf.Abs(rightDot) > 0.7f && Mathf.Abs(forwardDot) < 0.3f)
                return baseSpeed * strafeSpeedMultiplier;
            
            // Backward strafe movement
            if (forwardDot < -0.3f && Mathf.Abs(rightDot) > 0.3f)
                return baseSpeed * backwardStrafeSpeedMultiplier;
        }
        
        return baseSpeed;
    }

    private void HandleFreeLookRotation(float deltaTime)
    {
        if (moveInput.magnitude > 0.1f)
        {
            // Calculate desired movement direction
            Vector3 inputDirection = cachedCharacterRight * moveInput.x + cachedCharacterForward * moveInput.y;
            inputDirection.y = 0f;
            inputDirection = inputDirection.normalized;
            
            bool isMovingForward = moveInput.y > 0.1f;  // W key
            bool isMovingBackward = moveInput.y < -0.1f; // S key
            bool hasSideInput = Mathf.Abs(moveInput.x) > 0.3f; // A or D key
            
            // Rotate character when moving forward OR backward with side input
            if ((isMovingForward || isMovingBackward) && hasSideInput)
            {
                float targetAngle;
                
                if (isMovingForward)
                {
                    // Forward movement: normal rotation calculation
                    targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg;
                }
                else
                {
                    // Backward movement: calculate rotation but account for backing up
                    targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg;
                    targetAngle = targetAngle + 180f; // Flip the direction
                    
                    // Normalize angle
                    if (targetAngle > 180f) targetAngle -= 360f;
                    if (targetAngle < -180f) targetAngle += 360f;
                }
                
                float currentAngle = transform.eulerAngles.y;
                float angleDifference = Mathf.DeltaAngle(currentAngle, targetAngle);
                
                // Only rotate if difference is significant
                if (Mathf.Abs(angleDifference) > rotationAngleThreshold)
                {
                    float rotationSpeed = isMovingForward ? freeLookRotationSpeed : freeLookRotationSpeed * 0.7f;
                    float rotationStep = rotationSpeed * deltaTime;
                    float newAngle = Mathf.LerpAngle(currentAngle, targetAngle, rotationStep);
                    transform.rotation = Quaternion.Euler(0, newAngle, 0);
                    
                    // Mark directions as needing update
                    directionsNeedUpdate = true;
                }
            }
        }
    }

    private void UpdateCamera(float deltaTime)
    {
        // Update mouse state
        UpdateMouseState();
        
        // Update camera mode
        UpdateCameraMode();
        
        // Handle camera rotation
        UpdateCameraRotation(deltaTime);
        
        // Update camera position
        UpdateCameraPosition(deltaTime);
        
        // Update zoom
        UpdateZoom(deltaTime);
        
        // Update head bob for first person
        if (isFirstPerson)
        {
            UpdateHeadBob(deltaTime);
        }
    }

    private void UpdateMouseState()
    {
        if (cachedMouse != null)
        {
            rightMousePressed = cachedMouse.rightButton.isPressed;
            leftMousePressed = cachedMouse.leftButton.isPressed;
        }
    }

    private void UpdateCameraMode()
    {
        // Determine camera control modes
        isMouseLookMode = useRightMouseButton && rightMousePressed;
        leftMouseCameraActive = enableLeftMouseCamera && leftMousePressed && 
                               (!isHoldingObject || isChargingThrow);
        
        // Handle cursor state
        if (isMouseLookMode || leftMouseCameraActive)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            shouldFollowCamera = false;
            cameraFollowTimer = 0f;
        }
        else if (useWoWCameraStyle)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void UpdateCameraRotation(float deltaTime)
    {
        if (isFirstPerson)
        {
            // First person camera
            transform.Rotate(Vector3.up * smoothedLookInput.x * mouseLookSensitivity);
            verticalRotation -= smoothedLookInput.y * mouseLookSensitivity;
            verticalRotation = Mathf.Clamp(verticalRotation, -80f, 80f);
            cameraHolder.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
            directionsNeedUpdate = true;
        }
        else if (useWoWCameraStyle)
        {
            UpdateWoWCameraRotation(deltaTime);
        }
        else
        {
            // Original camera system
            horizontalRotation += smoothedLookInput.x * mouseLookSensitivity;
            verticalRotation -= smoothedLookInput.y * mouseLookSensitivity;
            verticalRotation = Mathf.Clamp(verticalRotation, minVerticalAngle, maxVerticalAngle);
        }
    }

    private void UpdateWoWCameraRotation(float deltaTime)
    {
        if (isMouseLookMode)
        {
            // Right mouse mode: character rotates with camera
            float sensitivity = mouseLookSensitivity;
            transform.Rotate(Vector3.up * smoothedLookInput.x * sensitivity);
            verticalRotation -= smoothedLookInput.y * sensitivity;
            verticalRotation = Mathf.Clamp(verticalRotation, minVerticalAngle, maxVerticalAngle);
            
            horizontalRotation = transform.eulerAngles.y;
            freeLookRotation = new Vector2(horizontalRotation, verticalRotation);
            directionsNeedUpdate = true;
        }
        else if (leftMouseCameraActive)
        {
            // Left mouse mode: only camera rotates
            float sensitivity = leftMouseCameraSensitivity;
            freeLookRotation.x += smoothedLookInput.x * sensitivity;
            freeLookRotation.y -= smoothedLookInput.y * sensitivity;
            freeLookRotation.y = Mathf.Clamp(freeLookRotation.y, minVerticalAngle, maxVerticalAngle);
            
            horizontalRotation = freeLookRotation.x;
            verticalRotation = freeLookRotation.y;
        }
        else
        {
            // Free look mode with automatic following
            UpdateCameraFollowing(deltaTime);
        }
        
        // Handle object holding camera behavior
        if (isHoldingObject && !isChargingThrow)
        {
            float targetHorizontalRotation = transform.eulerAngles.y;
            horizontalRotation = Mathf.LerpAngle(horizontalRotation, targetHorizontalRotation, 
                deltaTime * cameraFollowSpeed * 2f);
            freeLookRotation.x = horizontalRotation;
            
            verticalRotation = Mathf.Lerp(verticalRotation, 5f, deltaTime * 2f);
            freeLookRotation.y = verticalRotation;
        }
    }

    private void UpdateCameraFollowing(float deltaTime)
    {
        bool isCurrentlyMoving = moveInput.magnitude > minMovementForFollow;
        float currentCharacterAngle = transform.eulerAngles.y;
        
        // Track movement state
        if (isCurrentlyMoving && !wasMovingLastFrame)
        {
            movementTimer = 0f;
            if (debugCameraFollow)
                Debug.Log("Started moving - camera should follow behind player");
        }
        else if (isCurrentlyMoving)
        {
            movementTimer += deltaTime;
        }
        else if (!isCurrentlyMoving && wasMovingLastFrame)
        {
            cameraFollowTimer = 0f;
            shouldFollowCamera = false;
            if (debugCameraFollow)
                Debug.Log("Stopped moving - stopping camera follow");
        }
        
        // Always follow behind player logic
        if (alwaysFollowBehindPlayer && 
            enableCameraFollow && 
            !isMouseLookMode && 
            !leftMouseCameraActive && 
            !isHoldingObject)
        {
            if (isCurrentlyMoving && movementTimer > movementFollowDelay)
            {
                // Always make camera follow behind character when moving
                shouldFollowCamera = true;
                currentFollowSpeed = behindPlayerFollowSpeed;
                
                if (debugCameraFollow && Time.frameCount % 60 == 0) // Log every 60 frames to avoid spam
                {
                    Debug.Log($"Always following behind player - Character facing: {currentCharacterAngle:F1}°, Camera: {horizontalRotation:F1}°");
                }
            }
            else if (!isCurrentlyMoving)
            {
                shouldFollowCamera = false;
            }
        }
        else
        {
            // Original angle-difference based following logic
            float angleDifference = Mathf.Abs(Mathf.DeltaAngle(lastCharacterAngle, currentCharacterAngle));
            
            if (enableCameraFollow && !isMouseLookMode && !leftMouseCameraActive)
            {
                bool shouldTriggerFollow = false;
                float followSpeed = cameraFollowSpeed;
                float followThreshold = cameraFollowThreshold;
                float followDelay = cameraFollowDelay;
                
                if (onlyFollowWhenMoving)
                {
                    if (isCurrentlyMoving && movementTimer > followDelay)
                    {
                        float cameraCharacterAngleDiff = Mathf.Abs(Mathf.DeltaAngle(horizontalRotation, currentCharacterAngle));
                        
                        if (cameraCharacterAngleDiff > followThreshold)
                        {
                            cameraFollowTimer += deltaTime;
                            
                            if (cameraFollowTimer > followDelay)
                            {
                                shouldTriggerFollow = true;
                                if (debugCameraFollow)
                                    Debug.Log($"Angle-based follow triggered - diff: {cameraCharacterAngleDiff:F1}°");
                            }
                        }
                        else
                        {
                            cameraFollowTimer = 0f;
                            shouldFollowCamera = false;
                        }
                    }
                }
                else
                {
                    if (angleDifference > followThreshold)
                    {
                        cameraFollowTimer += deltaTime;
                        
                        if (cameraFollowTimer > followDelay)
                        {
                            shouldTriggerFollow = true;
                        }
                    }
                    else
                    {
                        cameraFollowTimer = 0f;
                        shouldFollowCamera = false;
                    }
                }
                
                if (shouldTriggerFollow)
                {
                    shouldFollowCamera = true;
                    currentFollowSpeed = followSpeed;
                }
            }
            else
            {
                shouldFollowCamera = false;
                cameraFollowTimer = 0f;
            }
        }
        
        // Apply automatic camera following
        if (shouldFollowCamera)
        {
            UpdateAutomaticCameraFollow();
        }
        
        wasMovingLastFrame = isCurrentlyMoving;
        lastCharacterAngle = currentCharacterAngle;
    }

    private void UpdateAutomaticCameraFollow()
    {
        float targetHorizontalRotation = transform.eulerAngles.y;
        
        // Use the current follow speed
        float followSpeed = currentFollowSpeed;
        
        // For always-behind-player mode, use a more responsive interpolation
        if (alwaysFollowBehindPlayer && isMoving)
        {
            // Smoothly but continuously rotate camera to face behind character
            float currentHorizontal = horizontalRotation;
            float newHorizontal = Mathf.LerpAngle(currentHorizontal, targetHorizontalRotation, 
                Time.deltaTime * followSpeed);
            
            horizontalRotation = newHorizontal;
            freeLookRotation.x = horizontalRotation;
            
            // Don't stop following until movement stops - keep following continuously
            float angleDifference = Mathf.Abs(Mathf.DeltaAngle(horizontalRotation, targetHorizontalRotation));
            
            if (debugCameraFollow && Time.frameCount % 30 == 0) // Log every 30 frames to avoid spam
            {
                Debug.Log($"Continuously following - Current: {currentHorizontal:F1}°, Target: {targetHorizontalRotation:F1}°, Diff: {angleDifference:F1}°");
            }
        }
        else
        {
            // Original logic for angle-based following
            float currentHorizontal = horizontalRotation;
            float newHorizontal = Mathf.LerpAngle(currentHorizontal, targetHorizontalRotation, 
                Time.deltaTime * followSpeed);
            
            horizontalRotation = newHorizontal;
            verticalRotation = freeLookRotation.y;
            freeLookRotation.x = horizontalRotation;
            
            // Stop following when close enough
            float angleDifference = Mathf.Abs(Mathf.DeltaAngle(horizontalRotation, targetHorizontalRotation));
            if (angleDifference < 2f)
            {
                shouldFollowCamera = false;
                cameraFollowTimer = 0f;
                if (debugCameraFollow)
                    Debug.Log("Camera follow completed - within 2 degrees of target");
            }
        }
    }

    private void UpdateCameraPosition(float deltaTime)
    {
        if (isFirstPerson)
        {
            // First person positioning with smooth transition
            Vector3 targetPos = firstPersonPosition.position;
            targetPos.y += currentCameraYOffset;
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, targetPos, 
                deltaTime * cameraTransitionSpeed);
        }
        else
        {
            // Third person positioning
            Vector3 playerTargetPosition = transform.position + thirdPersonOffset;
            Vector3 directionToCamera = Quaternion.Euler(verticalRotation, horizontalRotation, 0f) * -Vector3.forward;
            Vector3 desiredCameraPos = playerTargetPosition + directionToCamera * currentCameraDistance;
            
            // Camera collision
            if (Physics.Raycast(playerTargetPosition, directionToCamera, out RaycastHit hit, currentCameraDistance))
            {
                desiredCameraPos = hit.point + directionToCamera * cameraCollisionOffset;
            }
            
            cameraTransform.position = desiredCameraPos;
            cameraTransform.LookAt(playerTargetPosition);
        }
    }

    private void UpdateZoom(float deltaTime)
    {
        if (!isFirstPerson && !isTransitioning)
        {
            currentCameraDistance = Mathf.Lerp(currentCameraDistance, targetCameraDistance, 
                deltaTime * zoomSpeed);
        }
    }

    private void UpdateHeadBob(float deltaTime)
    {
        Vector3 baseCameraPosition = originalCameraPosition;
        baseCameraPosition.y += currentCameraYOffset;
        
        if (isGrounded && isMoving)
        {
            float currentBobFrequency = isCrouching ? crouchBobFrequency : bobFrequency;
            float currentBobAmount = isCrouching ? crouchBobAmount : bobAmount;
            float speedMultiplier = currentMovementSpeed / walkSpeed;
            
            bobTimer += deltaTime * currentBobFrequency * speedMultiplier;
            
            Vector3 bobOffset = new Vector3(
                Mathf.Cos(bobTimer) * currentBobAmount,
                Mathf.Sin(bobTimer * 2) * currentBobAmount,
                0
            );
            
            cameraHolder.localPosition = baseCameraPosition + bobOffset;
        }
        else
        {
            bobTimer = 0;
            cameraHolder.localPosition = Vector3.Lerp(cameraHolder.localPosition, 
                baseCameraPosition, deltaTime * 5f);
        }
    }

    private void UpdateAnimations(float deltaTime)
    {
        if (animator == null) return;
        
        // Calculate animation values
        float inputMagnitude = moveInput.magnitude;
        bool isMovingForAnim = inputMagnitude > idleThreshold;
        
        float animHorizontal = 0f;
        float animVertical = 0f;
        float animSpeed = 0f;
        
        if (isMovingForAnim)
        {
            if (isFirstPerson)
            {
                animHorizontal = moveInput.x;
                animVertical = moveInput.y;
            }
            else
            {
                // Calculate relative to character or camera based on mode
                Vector3 inputDirection;
                if (useWoWCameraStyle && !isMouseLookMode)
                {
                    // Free look: relative to character
                    animHorizontal = moveInput.x;
                    animVertical = moveInput.y;
                }
                else
                {
                    // Mouse look: relative to camera
                    inputDirection = (cachedCameraRight * moveInput.x + cachedCameraForward * moveInput.y).normalized;
                    animVertical = Vector3.Dot(inputDirection, cachedCharacterForward);
                    animHorizontal = Vector3.Dot(inputDirection, cachedCharacterRight);
                }
            }
            
            // Calculate speed multipliers
            float baseSpeedMultiplier = isCrouching ? crouchSpeedThreshold : 
                                      (isRunning ? runSpeedThreshold : walkSpeedThreshold);
            
            // Apply directional speed modifications
            bool isPureStrafe = Mathf.Abs(animVertical) < 0.1f && Mathf.Abs(animHorizontal) > 0.7f;
            bool isPureBackward = animVertical < -0.7f && Mathf.Abs(animHorizontal) < 0.1f;
            bool isBackwardStrafe = animVertical < -0.3f && Mathf.Abs(animHorizontal) > 0.3f;
            
            isStrafing = isPureStrafe;
            isBackwardStrafing = isBackwardStrafe;
            
            if (isPureStrafe)
                animSpeed = baseSpeedMultiplier * strafeSpeedMultiplier;
            else if (isPureBackward)
                animSpeed = baseSpeedMultiplier * backwardSpeedMultiplier;
            else if (isBackwardStrafe)
                animSpeed = baseSpeedMultiplier * backwardStrafeSpeedMultiplier;
            else
                animSpeed = baseSpeedMultiplier * inputMagnitude;
        }
        else
        {
            isStrafing = false;
            isBackwardStrafing = false;
        }
        
        // Smooth animation values
        smoothedHorizontal = Mathf.SmoothDamp(smoothedHorizontal, animHorizontal, 
            ref horizontalVelocity, animationSmoothTime);
        smoothedVertical = Mathf.SmoothDamp(smoothedVertical, animVertical, 
            ref verticalVelocityAnim, animationSmoothTime);
        smoothedSpeed = Mathf.SmoothDamp(smoothedSpeed, animSpeed, 
            ref speedVelocity, animationSmoothTime);
        
        // Clamp values
        smoothedHorizontal = Mathf.Clamp(smoothedHorizontal, -1f, 1f);
        smoothedVertical = Mathf.Clamp(smoothedVertical, -1f, 1f);
        smoothedSpeed = Mathf.Clamp(smoothedSpeed, 0f, 1f);
        
        // Set animator parameters
        animator.SetFloat(AnimParamSpeed, smoothedSpeed);
        animator.SetFloat(AnimParamHorizontal, smoothedHorizontal);
        animator.SetFloat(AnimParamVertical, smoothedVertical);
        animator.SetBool(AnimParamIsGrounded, isGrounded);
        animator.SetBool(AnimParamIsRunning, isRunning && isMovingForAnim && !isCrouching);
        animator.SetBool(AnimParamIsCrouching, isCrouching);
        animator.SetBool(AnimParamIsStrafing, isStrafing);
        animator.SetBool(AnimParamIsBackwardStrafing, isBackwardStrafing);
    }

    private void UpdateCrouchState()
    {
        bool crouchInput = crouchAction.ReadValue<float>() > 0.5f;
        
        if (crouchInput && !isCrouching)
        {
            StartCrouch();
        }
        else if (!crouchInput && isCrouching)
        {
            if (CanStandUp())
            {
                StopCrouch();
            }
        }
    }

    private void UpdateCrouchTransition(float deltaTime)
    {
        // Smooth height transition
        characterController.height = Mathf.Lerp(characterController.height, targetHeight, 
            deltaTime * crouchTransitionSpeed);
        
        // Update controller center
        Vector3 currentCenter = characterController.center;
        currentCenter.y = Mathf.Lerp(currentCenter.y, targetControllerCenterY, 
            deltaTime * crouchTransitionSpeed);
        characterController.center = currentCenter;
        
        // Update camera offset for first person
        if (isFirstPerson)
        {
            currentCameraYOffset = Mathf.Lerp(currentCameraYOffset, targetCameraYOffset, 
                deltaTime * crouchTransitionSpeed);
        }
    }

    private void UpdateJumpState(float deltaTime)
    {
        if (isJumpQueued)
        {
            jumpTimer += deltaTime;
            if (jumpTimer >= jumpAnimationDelay)
            {
                // Apply vertical jump force
                currentVelocity.y = jumpForce;
            
                // Apply stored momentum
                if (maintainJumpMomentum && jumpMomentumDirection.magnitude > 0.1f)
                {
                    float finalMomentumSpeed = jumpMomentumSpeed * jumpMomentumMultiplier;
                    Vector3 horizontalMomentum = jumpMomentumDirection * finalMomentumSpeed;
                
                    // FIXED: Set the horizontal velocity directly instead of adding to it
                    currentVelocity.x = horizontalMomentum.x;
                    currentVelocity.z = horizontalMomentum.z;
                
                    if (debugMovementValues)
                    {
                        Debug.Log($"Jump executed - Applied momentum: {horizontalMomentum}, Final velocity: {currentVelocity}");
                    }
                }
            
                isJumpQueued = false;
                jumpTimer = 0f;
            
                // Clear stored momentum
                jumpMomentumDirection = Vector3.zero;
                jumpMomentumSpeed = 0f;
            }
        }
    }

    private void UpdateInteractions(float deltaTime)
    {
        // Update attack cooldown
        if (currentAttackCooldown > 0)
        {
            currentAttackCooldown -= deltaTime;
        }
        
        // Update held object
        UpdateHeldObject();
        
        // Update throw charging
        if (isChargingThrow)
        {
            throwChargeTimer += deltaTime;
        }
    }

    private void UpdateUI(float deltaTime)
    {
        // Update throw power bar
        if (isChargingThrow && throwPowerBar != null)
        {
            float percent = Mathf.Clamp01(throwChargeTimer / maxHoldTime);
            throwPowerBar.fillAmount = percent;
            
            if (!throwPowerBar.gameObject.activeInHierarchy)
                throwPowerBar.gameObject.SetActive(true);
        }
        else if (throwPowerBar != null && throwPowerBar.gameObject.activeInHierarchy)
        {
            throwPowerBar.gameObject.SetActive(false);
        }
    }

    private void UpdateCachedDirections()
    {
        cachedCharacterForward = transform.forward;
        cachedCharacterRight = transform.right;
        cachedCameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        cachedCameraRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        directionsNeedUpdate = false;
    }

    // Crouch methods
    private void StartCrouch()
    {
        isCrouching = true;
        targetHeight = crouchHeight;
        targetControllerCenterY = originalControllerCenterY - ((standingHeight - crouchHeight) * 0.5f);
        
        if (isFirstPerson)
        {
            targetCameraYOffset = -(standingHeight - crouchHeight) * 0.5f;
        }
    }

    private void StopCrouch()
    {
        isCrouching = false;
        targetHeight = standingHeight;
        targetControllerCenterY = originalControllerCenterY;
        
        if (isFirstPerson)
        {
            targetCameraYOffset = 0f;
        }
    }

    private bool CanStandUp()
    {
        if (!isCrouching) return true;
        
        Vector3 bottom = transform.position + characterController.center - Vector3.up * (characterController.height * 0.5f);
        Vector3 top = bottom + Vector3.up * standingHeight;
        float checkRadius = characterController.radius * 0.8f;
        
        return !Physics.CheckCapsule(bottom + Vector3.up * checkRadius, 
                                   top - Vector3.up * checkRadius, checkRadius);
    }

    // Input handling methods
    private void TryJump()
{
    if (isCrouching)
    {
        if (CanStandUp())
        {
            StopCrouch();
        }
        return;
    }
    
    if (isGrounded && !isJumpQueued)
    {
        // Store current movement for jump momentum
        if (maintainJumpMomentum && moveInput.magnitude > 0.1f)
        {
            // Calculate current movement direction
            if (isFirstPerson)
            {
                jumpMomentumDirection = cachedCharacterRight * moveInput.x + cachedCharacterForward * moveInput.y;
            }
            else if (useWoWCameraStyle)
            {
                if (isMouseLookMode)
                {
                    jumpMomentumDirection = cachedCameraRight * moveInput.x + cachedCameraForward * moveInput.y;
                }
                else
                {
                    jumpMomentumDirection = cachedCharacterRight * moveInput.x + cachedCharacterForward * moveInput.y;
                }
            }
            else
            {
                jumpMomentumDirection = cachedCameraRight * moveInput.x + cachedCameraForward * moveInput.y;
            }
            
            jumpMomentumDirection.y = 0f;
            jumpMomentumDirection = jumpMomentumDirection.normalized;
            
            // Store current horizontal speed
            jumpMomentumSpeed = new Vector3(currentVelocity.x, 0, currentVelocity.z).magnitude;
            
            // Ensure minimum forward force
            if (jumpMomentumSpeed < jumpForwardForce)
            {
                jumpMomentumSpeed = jumpForwardForce;
            }
            
            if (debugMovementValues)
            {
                Debug.Log($"Jump momentum stored - Direction: {jumpMomentumDirection}, Speed: {jumpMomentumSpeed:F2}");
            }
        }
        else
        {
            jumpMomentumDirection = Vector3.zero;
            jumpMomentumSpeed = 0f;
        }
        
        isJumpQueued = true;
        jumpTimer = 0f;
        
        if (animator != null)
        {
            animator.SetTrigger(AnimParamJump);
        }
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
            
            if (animator != null)
            {
                animator.SetTrigger(UnityEngine.Random.value > 0.5f ? AnimParamPunch : AnimParamKick);
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
        
        if (animator != null)
        {
            animator.SetTrigger(AnimParamInteract);
        }
    }

    private void TryPickup()
    {
        if (isHoldingObject)
        {
            DropObject();
        }
        else
        {
            GameObject targetObject = FindPickupTarget();
            if (targetObject != null)
            {
                PickupObject(targetObject);
            }
        }
    }

    private GameObject FindPickupTarget()
    {
        if (isFirstPerson)
        {
            if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, 
                out RaycastHit hit, pickupRange, pickupMask))
            {
                if (hit.collider.TryGetComponent(out SimplePickup pickup) && pickup.CanBePickedUp())
                {
                    return pickup.gameObject;
                }
            }
        }
        else
        {
            Vector3 playerCenter = transform.position + Vector3.up * 1.0f;
            Collider[] nearbyObjects = Physics.OverlapSphere(playerCenter, pickupRange, pickupMask);
            
            float closestDistance = float.MaxValue;
            GameObject closest = null;
            
            foreach (Collider col in nearbyObjects)
            {
                if (col.TryGetComponent(out SimplePickup pickup) && pickup.CanBePickedUp())
                {
                    float distance = Vector3.Distance(playerCenter, col.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closest = pickup.gameObject;
                    }
                }
            }
            
            return closest;
        }
        
        return null;
    }

    private void PickupObject(GameObject obj)
    {
        heldObject = obj;
        heldObjectRb = obj.GetComponent<Rigidbody>();
        isHoldingObject = true;
        
        if (heldObjectRb != null)
        {
            heldObjectRb.useGravity = false;
            heldObjectRb.linearVelocity = Vector3.zero;
            heldObjectRb.angularVelocity = Vector3.zero;
            heldObjectRb.isKinematic = false;
        }
        
        if (animator != null)
        {
            animator.SetTrigger(AnimParamPickup);
        }
    }

    private void DropObject()
    {
        if (heldObject != null)
        {
            heldObject.transform.SetParent(null);
            
            if (heldObjectRb != null)
            {
                heldObjectRb.useGravity = true;
                Vector3 dropDirection = cameraTransform.forward + Vector3.up * 0.2f;
                heldObjectRb.linearVelocity = dropDirection * 2f;
            }
            
            heldObject = null;
            heldObjectRb = null;
            isHoldingObject = false;
            
            if (animator != null)
            {
                animator.SetTrigger(AnimParamDrop);
            }
        }
    }

    private void UpdateHeldObject()
    {
        if (isHoldingObject && heldObject != null)
        {
            Vector3 targetPosition;
            Quaternion targetRotation;
            
            if (isFirstPerson)
            {
                targetPosition = cameraTransform.position + cameraTransform.forward * holdDistance;
                targetRotation = cameraTransform.rotation;
            }
            else
            {
                targetPosition = transform.position + Vector3.up * 1.5f + cameraTransform.forward * holdDistance;
                targetRotation = cameraTransform.rotation;
            }
            
            heldObject.transform.position = Vector3.Lerp(heldObject.transform.position, 
                targetPosition, Time.deltaTime * holdPositionSpeed);
            heldObject.transform.rotation = Quaternion.Lerp(heldObject.transform.rotation, 
                targetRotation, Time.deltaTime * holdPositionSpeed);
            
            if (heldObjectRb != null)
            {
                heldObjectRb.linearVelocity = Vector3.zero;
                heldObjectRb.angularVelocity = Vector3.zero;
            }
        }
    }

    private void StartChargingThrow()
    {
        if (isHoldingObject && !isChargingThrow)
        {
            isChargingThrow = true;
            throwChargeTimer = 0f;
        }
    }

    private void ReleaseThrow()
    {
        if (isChargingThrow && isHoldingObject)
        {
            float chargePercent = Mathf.Clamp01(throwChargeTimer / maxHoldTime);
            float force = Mathf.Lerp(minThrowForce, maxThrowForce, chargePercent);
            ThrowObject(force);
            
            isChargingThrow = false;
            throwChargeTimer = 0f;
        }
    }

    private void ThrowObject(float force)
    {
        if (heldObject != null)
        {
            heldObject.transform.SetParent(null);
            
            if (heldObjectRb != null)
            {
                heldObjectRb.useGravity = true;
                heldObjectRb.isKinematic = false;
                
                Vector3 throwDirection = cameraTransform.forward;
                heldObjectRb.AddForce(throwDirection * force);
                heldObjectRb.AddForce(Vector3.up * 0.2f * force);
            }
            
            heldObject = null;
            heldObjectRb = null;
            isHoldingObject = false;
            
            if (animator != null)
            {
                animator.SetTrigger(AnimParamThrow);
            }
        }
    }

    private void HandleZoom(float scrollValue)
    {
        if (!isFirstPerson && !isTransitioning && Mathf.Abs(scrollValue) > 0.1f)
        {
            float adjustedScrollValue = invertScrollDirection ? -scrollValue : scrollValue;
            targetCameraDistance -= adjustedScrollValue * zoomSensitivity;
            targetCameraDistance = Mathf.Clamp(targetCameraDistance, minCameraDistance, maxCameraDistance);
        }
    }

    private void ToggleView()
    {
        if (isFirstPerson)
        {
            StartCoroutine(EyeCloseTransition());
        }
        else
        {
            StartCoroutine(SmoothTransition());
        }
    }

    private void CreateEyeCloseOverlay()
    {
        GameObject canvasGO = new GameObject("EyeCloseCanvas");
        Canvas overlayCanvas = canvasGO.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 1000;
        
        var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        
        eyeCloseOverlay = new GameObject("EyeCloseOverlay");
        eyeCloseOverlay.transform.SetParent(canvasGO.transform, false);
        
        eyeCloseImage = eyeCloseOverlay.AddComponent<UnityEngine.UI.Image>();
        eyeCloseImage.color = eyeCloseColor;
        
        var rectTransform = eyeCloseOverlay.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        
        eyeCloseImage.color = new Color(eyeCloseColor.r, eyeCloseColor.g, eyeCloseColor.b, 0f);
        
        DontDestroyOnLoad(canvasGO);
    }

    private IEnumerator EyeCloseTransition()
    {
        isTransitioning = true;
        
        // Fade to black
        float fadeInDuration = eyeCloseTransitionDuration * 0.3f;
        float elapsedTime = 0f;
        
        while (elapsedTime < fadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = elapsedTime / fadeInDuration;
            eyeCloseImage.color = new Color(eyeCloseColor.r, eyeCloseColor.g, eyeCloseColor.b, alpha);
            yield return null;
        }
        
        // Switch to third person
        transform.rotation = Quaternion.Euler(0, horizontalRotation, 0);
        horizontalRotation = transform.eulerAngles.y;
        verticalRotation = 0f;
        
        Vector3 playerTargetPosition = transform.position + thirdPersonOffset;
        Vector3 directionToCamera = Quaternion.Euler(verticalRotation, horizontalRotation, 0f) * -Vector3.forward;
        
        currentCameraDistance = defaultCameraDistance;
        targetCameraDistance = defaultCameraDistance;
        
        Vector3 desiredCameraPos = playerTargetPosition + directionToCamera * currentCameraDistance;
        cameraTransform.position = desiredCameraPos;
        cameraTransform.LookAt(playerTargetPosition);
        
        isFirstPerson = false;
        cameraHolder.localPosition = originalCameraPosition;
        cameraHolder.localRotation = Quaternion.identity;
        directionsNeedUpdate = true;
        
        yield return new WaitForSeconds(eyeCloseTransitionDuration * 0.4f);
        
        // Fade from black
        float fadeOutDuration = eyeCloseTransitionDuration * 0.3f;
        elapsedTime = 0f;
        
        while (elapsedTime < fadeOutDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = 1f - (elapsedTime / fadeOutDuration);
            eyeCloseImage.color = new Color(eyeCloseColor.r, eyeCloseColor.g, eyeCloseColor.b, alpha);
            yield return null;
        }
        
        eyeCloseImage.color = new Color(eyeCloseColor.r, eyeCloseColor.g, eyeCloseColor.b, 0f);
        isTransitioning = false;
    }

    private IEnumerator SmoothTransition()
    {
        isTransitioning = true;
        
        Vector3 startPosition = cameraTransform.position;
        Quaternion startRotation = cameraTransform.rotation;
        Vector3 targetPosition = firstPersonPosition.position;
        Quaternion targetRotation = Quaternion.Euler(0f, horizontalRotation, 0f);
        
        float duration = 0.8f;
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
            
            // Smooth position transition with slight arc
            Vector3 straightPath = Vector3.Lerp(startPosition, targetPosition, smoothProgress);
            float arcOffset = Mathf.Sin(smoothProgress * Mathf.PI) * 0.3f;
            Vector3 currentPos = straightPath + Vector3.up * arcOffset;
            
            cameraTransform.position = currentPos;
            cameraTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, smoothProgress);
            
            yield return null;
        }
        
        // Finalize first person setup
        isFirstPerson = true;
        transform.rotation = Quaternion.Euler(0f, horizontalRotation, 0f);
        verticalRotation = 0f;
        cameraHolder.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
        currentCameraYOffset = targetCameraYOffset;
        
        cameraTransform.position = firstPersonPosition.position;
        cameraTransform.rotation = transform.rotation;
        directionsNeedUpdate = true;
        
        isTransitioning = false;
    }

    private void OnDestroy()
    {
        if (eyeCloseOverlay != null)
        {
            Destroy(eyeCloseOverlay.transform.parent.gameObject);
        }
    }

    private void Start()
    {
        if (throwPowerBar != null)
            throwPowerBar.gameObject.SetActive(false);
    }

    // Debug visualization
    private void OnGUI()
{
    if (debugMovementValues || debugCameraFollow)
    {
        GUILayout.BeginArea(new Rect(10, 10, 400, 350));
        
        if (debugMovementValues)
        {
            GUILayout.Label($"Mouse Look Mode: {isMouseLookMode}");
            GUILayout.Label($"Left Mouse Camera Active: {leftMouseCameraActive}");
            GUILayout.Label($"Is Holding Object: {isHoldingObject}");
            GUILayout.Label($"Move Input: {moveInput}");
            GUILayout.Label($"Current Speed: {currentMovementSpeed:F2}");
            GUILayout.Label($"Is Moving: {isMoving}");
        }
        
        if (debugCameraFollow)
        {
            GUILayout.Label("=== CAMERA FOLLOW DEBUG ===");
            GUILayout.Label($"Character Rotation: {transform.eulerAngles.y:F1}°");
            GUILayout.Label($"Camera Rotation: {horizontalRotation:F1}°");
            float angleDiff = Mathf.Abs(Mathf.DeltaAngle(horizontalRotation, transform.eulerAngles.y));
            GUILayout.Label($"Angle Difference: {angleDiff:F1}°");
            GUILayout.Label($"Should Follow Camera: {shouldFollowCamera}");
            GUILayout.Label($"Always Follow Behind: {alwaysFollowBehindPlayer}");
            GUILayout.Label($"Behind Follow Speed: {behindPlayerFollowSpeed}");
            GUILayout.Label($"Movement Timer: {movementTimer:F2}");
            GUILayout.Label($"Current Follow Speed: {currentFollowSpeed:F1}");
        }
        
        GUILayout.EndArea();
    }
}

private void OnDrawGizmosSelected()
{
    if (playerCamera != null)
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(cameraTransform.position, interactionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(cameraTransform.position, attackRange);
        Gizmos.color = Color.green;
        
        if (isFirstPerson)
        {
            Vector3 rayStart = cameraTransform.position;
            Vector3 rayEnd = rayStart + cameraTransform.forward * pickupRange;
            Gizmos.DrawLine(rayStart, rayEnd);
            Gizmos.DrawWireSphere(rayEnd, 0.1f);
        }
        else
        {
            Vector3 playerCenter = transform.position + Vector3.up * 1.0f;
            Gizmos.DrawWireSphere(playerCenter, pickupRange);
        }
    }
}
}

// Interface definitions remain the same
public interface IInteractable
{
    void Interact();
}

public interface IDamageable
{
    void TakeDamage(int amount);
}