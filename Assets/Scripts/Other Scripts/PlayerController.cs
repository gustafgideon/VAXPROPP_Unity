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

    private readonly int animParamSpeed = Animator.StringToHash("Speed");
    private readonly int animParamIsGrounded = Animator.StringToHash("IsGrounded");
    private readonly int animParamIsRunning = Animator.StringToHash("IsRunning");
    private readonly int animParamIsCrouching = Animator.StringToHash("IsCrouching");
    private readonly int animParamJump = Animator.StringToHash("Jump");
    private readonly int animParamPunch = Animator.StringToHash("Punch");
    private readonly int animParamKick = Animator.StringToHash("Kick");
    private readonly int animParamPickup = Animator.StringToHash("Pickup");
    private readonly int animParamDrop = Animator.StringToHash("Drop");
    private readonly int animParamThrow = Animator.StringToHash("Throw");
    private readonly int animParamInteract = Animator.StringToHash("Interact");
    private readonly int animParamHorizontal = Animator.StringToHash("Horizontal");
    private readonly int animParamVertical = Animator.StringToHash("Vertical");
    private readonly int animParamTurnDirection = Animator.StringToHash("TurnDirection");
    private readonly int animParamIsStrafing = Animator.StringToHash("IsStrafing");
    private readonly int animParamIsBackwardStrafing = Animator.StringToHash("IsBackwardStrafing");
    
    [Header("Animation Smoothing")]
    [SerializeField] private float animationSmoothTime = 0.1f;
    [SerializeField] private float rotationSmoothTime = 0.12f;
    [SerializeField] private float animationResetSpeed = 5f;
    
    private float currentHorizontalVelocity;
    private float currentVerticalVelocity;
    private float currentSpeedVelocity;
    private float smoothedHorizontal;
    private float smoothedVertical;
    private float smoothedSpeed;

    [Header("Animation Thresholds")]
    [SerializeField] private float walkSpeedThreshold = 0.5f;
    [SerializeField] private float runSpeedThreshold = 0.8f;
    [SerializeField] private float crouchSpeedThreshold = 0.3f;
    [SerializeField] private float idleThreshold = 0.1f;

    [Header("WoW-Style Camera Settings")]
    [SerializeField] private bool useWoWCameraStyle = true;
    [SerializeField] private float freeLookSensitivity = 2f;
    [SerializeField] private float mouseLookSensitivity = 2f;
    [SerializeField] private bool useRightMouseButton = true;
    [SerializeField] private bool useLeftMouseButton = false; // Keep this false for manual camera
    [SerializeField] private bool requireBothButtons = false;
    [SerializeField] private bool enableLeftMouseCamera = true; // NEW: Enable left mouse camera control
    [SerializeField] private float leftMouseCameraSensitivity = 1.5f;
    
    [Header("WoW Camera Follow Settings")]
    [SerializeField] private bool enableCameraFollow = true; // Enable automatic camera following
    [SerializeField] private float cameraFollowSpeed = 2f; // How fast camera follows character
    [SerializeField] private float cameraFollowDelay = 0.5f; // Delay before camera starts following
    [SerializeField] private float cameraFollowThreshold = 15f; // Angle difference to trigger follow
    [SerializeField] private bool onlyFollowWhenMoving = true; // Only follow when character is moving
    [SerializeField] private float movementFollowDelay = 0.3f;
    
    [Header("WoW Free Look Rotation Settings")]
    [SerializeField] private float freeLookRotationSpeed = 2.4f; // Slower rotation speed
    [SerializeField] private float rotationAngleThreshold = 5f; // Minimum angle before rotating
    [SerializeField] private bool smoothFreeLookRotation = true; // Enable smooth rotation
    [SerializeField] private float maxRotationSpeed = 45f;

    [Header("View Transition Settings")]
    [SerializeField] private float eyeCloseTransitionDuration = 0.6f;
    [SerializeField] private float smoothTransitionDuration = 0.8f;
    [SerializeField] private AnimationCurve smoothTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float avoidanceArcHeight = 0.3f;
    [SerializeField] private Color eyeCloseColor = Color.black;

    [Header("Third Person Camera Settings")]
    [SerializeField] private float thirdPersonDistance = 5f;
    [SerializeField] private float minVerticalAngle = -30f;
    [SerializeField] private float maxVerticalAngle = 60f;
    [SerializeField] private float cameraCollisionOffset = 0.2f;
    [SerializeField] private Vector3 thirdPersonOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Enhanced Camera Settings")]
    [SerializeField] private float cameraAcceleration = 15f;
    [SerializeField] private float cameraDeceleration = 10f;
    [SerializeField] private float maxCameraSpeed = 8f;

    [Header("Third Person Zoom Settings")]
    [SerializeField] private float minCameraDistance = 1f;
    [SerializeField] private float maxCameraDistance = 10f;
    [SerializeField] private float defaultCameraDistance = 5f;
    [SerializeField] private float zoomSensitivity = 1f;
    [SerializeField] private float zoomSpeed = 8f;
    [SerializeField] private bool invertScrollDirection = false;

    [Header("Player Pivot Settings")]
    [SerializeField] private float playerRotationSpeed = 8f;
    [SerializeField] private bool instantPivot = false;
    [SerializeField] private float pivotThreshold = 0.1f;
    [SerializeField] private float pivotSmoothing = 0.12f;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float runSpeed = 7f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float gravityMultiplier = 2.5f;
    [SerializeField] private float airControl = 0.8f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float standingHeight = 2f;
    [SerializeField] private float crouchTransitionSpeed = 8f;

    [Header("Enhanced Movement Feel")]
    [SerializeField] private float movementAcceleration = 12f;
    [SerializeField] private float movementDeceleration = 15f;
    [SerializeField] private AnimationCurve accelerationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Third Person Movement")]
    [SerializeField] private float backwardSpeedMultiplier = 0.6f;
    [SerializeField] private float strafeSpeedMultiplier = 0.8f;
    [SerializeField] private float backwardStrafeSpeedMultiplier = 0.5f;
    [SerializeField] private float rotationDeadzone = 0.1f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpAnimationDelay = 0.08f;
    [SerializeField] private bool useJumpBuffer = true;
    [SerializeField] private float jumpBufferTime = 0.15f;

    [Header("Crouch Settings Enhanced")]
    [SerializeField] private float crouchCenterOffset = 0.5f;
    [SerializeField] private bool adjustControllerCenter = true;
    [SerializeField] private float standUpCheckRadius = 0.4f;
    [SerializeField] private float standUpCheckHeight = 0.2f;
    [SerializeField] private LayerMask standUpObstacleMask = -1;
    [SerializeField] private bool debugCrouchChecks = false;

    [Header("Head Bob Settings")]
    [SerializeField] private float bobFrequency = 2f;
    [SerializeField] private float bobAmount = 0.05f;

    [Header("Crouch Head Bob Settings")]
    [SerializeField] private float crouchBobFrequency = 1.5f;
    [SerializeField] private float crouchBobAmount = 0.03f;
    [SerializeField] private bool enableCrouchHeadBob = true;

    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private LayerMask interactionMask;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackCooldown = 0.5f;
    [SerializeField] private int attackDamage = 10;

    [Header("Pickup Settings")]
    [SerializeField] private float pickupRange = 3f;
    [SerializeField] private float throwForce = 600f;
    [SerializeField] private LayerMask pickupMask = -1;
    [SerializeField] private float holdDistance = 1.5f;
    [SerializeField] private float holdPositionSpeed = 10f;

    [Header("View Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 80f;
    [SerializeField] private float defaultFOV = 60f;

    [Header("Throw Settings")]
    [SerializeField] private float minThrowForce = 200f;
    [SerializeField] private float maxThrowForce = 1200f;
    [SerializeField] private float maxHoldTime = 1.5f;

    [Header("Throw UI")]
    [SerializeField] private Image throwPowerBar;

    [Header("Debug")]
    [SerializeField] private bool debugAnimationStates = false;
    [SerializeField] private bool debugMovementValues = false;
    [SerializeField] private bool forceCrouchExit = false;
    [SerializeField] private float maxCrouchTime = 5f;

    // Private references
    private CharacterController characterController;
    private PlayerInput playerInput;
    private Transform cameraTransform;

    // UI overlay for eye close effect
    private GameObject eyeCloseOverlay;
    private UnityEngine.UI.Image eyeCloseImage;
    private Canvas overlayCanvas;

    // Movement state
    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector3 currentVelocity;
    private Vector3 targetVelocity;
    private float verticalRotation;
    private float horizontalRotation;
    private bool isGrounded;
    private bool isRunning;
    private bool isFirstPerson = false;
    private float currentAttackCooldown;
    private bool isCrouching;
    private bool crouchInputHeld;
    private Vector3 originalCameraPosition;
    private float bobTimer;
    private float targetHeight;
    private float currentCameraYOffset;
    private float targetCameraYOffset;
    private Vector2 lastMoveInput;
    private bool isStrafing;
    private bool isBackwardStrafing;
    private float strafeThreshold = 0.8f;
    private float backwardStrafeThreshold = 0.3f;
    private bool leftMouseCameraActive = false;
    private bool wasHoldingObjectLastFrame = false;

    // Enhanced movement variables
    private float targetPlayerRotation;
    private float rotationVelocity;
    private Vector3 lastMovementDirection;
    private float currentMovementSpeed;
    private bool wasMoving;

    // Enhanced state management
    private float originalControllerCenterY;
    private float targetControllerCenterY;
    private bool isJumpQueued = false;
    private float jumpTimer = 0f;
    private float jumpBufferTimer = 0f;
    private bool wasGroundedLastFrame = true;

    // Camera smoothing
    private Vector2 cameraVelocity;
    private Vector2 currentLookInput;
    private Vector2 targetLookInput;

    // WoW Camera variables
    private bool isMouseLookMode = false;
    private Vector2 freeLookRotation;
    private bool rightMousePressed = false;
    private bool leftMousePressed = false;
    private float cameraFollowTimer = 0f;
    private float movementTimer = 0f;
    private bool wasMovingLastFrame = false;
    private float lastCharacterAngle = 0f;
    private bool shouldFollowCamera = false;

    // Crouch state management
    private bool canStandUp = true;
    private float crouchStateTimer = 0f;
    private bool forceStandUpNextFrame = false;

    // Zoom state
    private float currentCameraDistance;
    private float targetCameraDistance;

    // View transition state
    private bool isTransitioning = false;

    // Pickup state
    private GameObject heldObject;
    private Rigidbody heldObjectRb;
    private bool isHoldingObject = false;

    // Throw charge state
    private bool isChargingThrow = false;
    private float throwChargeTimer = 0f;

    // Input action references
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
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void InitializeComponents()
    {
        characterController = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();

        if (playerCamera == null)
            playerCamera = Camera.main;

        cameraTransform = playerCamera.transform;
        originalCameraPosition = cameraHolder.localPosition;

        characterController.height = standingHeight;
        targetHeight = standingHeight;
    
        originalControllerCenterY = characterController.center.y;
        targetControllerCenterY = originalControllerCenterY;
    
        currentCameraYOffset = 0f;
        targetCameraYOffset = 0f;

        horizontalRotation = transform.eulerAngles.y;
        verticalRotation = 0f;
        targetPlayerRotation = horizontalRotation;
        currentCameraDistance = defaultCameraDistance;
        targetCameraDistance = defaultCameraDistance;
        thirdPersonDistance = defaultCameraDistance;

        playerCamera.fieldOfView = defaultFOV;

        AutoDetectScrollDirection();

        // FIXED: Start with free cursor instead of locked
        if (useWoWCameraStyle)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void CreateEyeCloseOverlay()
    {
        GameObject canvasGO = new GameObject("EyeCloseCanvas");
        overlayCanvas = canvasGO.AddComponent<Canvas>();
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

    private void AutoDetectScrollDirection()
    {
        #if UNITY_STANDALONE_OSX
            invertScrollDirection = true;
        #elif UNITY_STANDALONE_WIN
            invertScrollDirection = false;
        #elif UNITY_STANDALONE_LINUX
            invertScrollDirection = false;
        #endif
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

        moveAction.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        moveAction.canceled += ctx => moveInput = Vector2.zero;

        lookAction.performed += ctx =>
        {
            if (!isTransitioning)
                targetLookInput = ctx.ReadValue<Vector2>();
        };
        lookAction.canceled += ctx => targetLookInput = Vector2.zero;

        runAction.performed += ctx => isRunning = true;
        runAction.canceled += ctx => isRunning = false;

        zoomAction.performed += ctx => HandleZoom(ctx.ReadValue<float>());

        jumpAction.performed += _ => 
        {
            if (isCrouching)
            {
                forceStandUpNextFrame = true;
            }
            TryJump();
        };
    
        attackAction.performed += _ => TryAttack();
        interactAction.performed += _ => TryInteract();
        pickupAction.performed += _ => TryPickup();
        switchViewAction.performed += _ =>
        {
            if (!isTransitioning)
                ToggleView();
        };

        // UPDATED: Throw action only works when holding object
        throwAction.started += ctx => 
        {
            if (isHoldingObject)
            {
                StartChargingThrow();
            }
        };
        throwAction.canceled += ctx => 
        {
            if (isHoldingObject)
            {
                ReleaseThrow();
            }
        };
    }

    private void OnEnable()
    {
        EnableAllInputs(true);
        throwAction?.Enable();
    }

    private void OnDisable()
    {
        EnableAllInputs(false);
        throwAction?.Disable();
    }

    private void EnableAllInputs(bool enable)
    {
        var actions = new[] { moveAction, lookAction, jumpAction, runAction,
                            attackAction, switchViewAction, interactAction, crouchAction, zoomAction, pickupAction, throwAction };
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
        if (debugCrouchChecks)
        {
            bool currentCrouchInput = crouchAction.ReadValue<float>() > 0.5f;
            if (Time.frameCount % 60 == 0)
            {
                Debug.Log($"Crouch Debug - Input: {currentCrouchInput}, State: {isCrouching}, Held: {crouchInputHeld}");
            }
        }
        
        if (forceCrouchExit && isCrouching)
        {
            forceCrouchExit = false;
            forceStandUpNextFrame = true;
            StopCrouch();
            Debug.Log("Force exited crouch via inspector");
        }
        
        if (isCrouching && crouchStateTimer > maxCrouchTime)
        {
            Debug.LogWarning($"Auto-exiting crouch after {maxCrouchTime} seconds");
            forceStandUpNextFrame = true;
            StopCrouch();
        }

        UpdateCrouchState();
        UpdateJump();
        UpdateInputSmoothing();

        if (!isTransitioning)
        {
            UpdateCameraRotation();
            UpdateZoom();
            UpdateCameraPosition();
        }

        UpdateMovement();
        UpdateCrouchTransition();
        UpdateHeldObject();
        UpdateAnimator();

        if (isFirstPerson && !isTransitioning)
            UpdateHeadBob();

        if (currentAttackCooldown > 0)
            currentAttackCooldown -= Time.deltaTime;

        UpdateThrowCharge();

        if (debugMovementValues && moveInput.magnitude > 0.1f)
        {
            Debug.Log($"Movement - Input: {moveInput.magnitude:F2}, Speed: {smoothedSpeed:F2}, Running: {isRunning}");
        }
    }

    private void UpdateInputSmoothing()
    {
        currentLookInput = Vector2.SmoothDamp(currentLookInput, targetLookInput, ref cameraVelocity, 
            1f / cameraAcceleration);
        
        if (currentLookInput.magnitude > maxCameraSpeed)
        {
            currentLookInput = currentLookInput.normalized * maxCameraSpeed;
        }
        
        lookInput = currentLookInput;

        if (useJumpBuffer && jumpBufferTimer > 0f)
        {
            jumpBufferTimer -= Time.deltaTime;
            if (isGrounded && !wasGroundedLastFrame)
            {
                TryJump();
                jumpBufferTimer = 0f;
            }
        }
        
        wasGroundedLastFrame = isGrounded;
    }

    private void UpdateCrouchState()
    {
        bool crouchInputCurrentlyPressed = crouchAction.ReadValue<float>() > 0.5f;
        
        if (isCrouching)
        {
            crouchStateTimer += Time.deltaTime;
        }
        
        if (isCrouching)
        {
            canStandUp = CheckCanStandUp();
            
            if (debugCrouchChecks)
            {
                Debug.Log($"Crouch Hold - Input Pressed: {crouchInputCurrentlyPressed}, Can Stand: {canStandUp}, Timer: {crouchStateTimer:F1}");
            }
        }
        
        if (crouchInputCurrentlyPressed && !isCrouching)
        {
            StartCrouch();
        }
        else if (!crouchInputCurrentlyPressed && isCrouching)
        {
            if (canStandUp || forceStandUpNextFrame)
            {
                StopCrouch();
                forceStandUpNextFrame = false;
            }
            else if (crouchStateTimer > 3f)
            {
                Debug.LogWarning("Force standing up - crouch state stuck");
                forceStandUpNextFrame = true;
            }
        }
        
        crouchInputHeld = crouchInputCurrentlyPressed;
    }

    private void UpdateJump()
    {
        if (isJumpQueued)
        {
            jumpTimer += Time.deltaTime;
            
            if (jumpTimer >= jumpAnimationDelay)
            {
                currentVelocity.y = jumpForce;
                isJumpQueued = false;
                jumpTimer = 0f;
            }
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        float inputMagnitude = moveInput.magnitude;
        bool isMoving = inputMagnitude > idleThreshold;
        
        float animHorizontal = 0f;
        float animVertical = 0f;
        float animSpeed = 0f;

        if (isMoving)
        {
            if (isFirstPerson)
            {
                animHorizontal = moveInput.x;
                animVertical = moveInput.y;
            }
            else
            {
                // FIXED: Use consistent direction calculation for animations
                if (useWoWCameraStyle && !isMouseLookMode)
                {
                    // Free look mode: animations relative to character
                    animHorizontal = moveInput.x;
                    animVertical = moveInput.y;
                }
                else
                {
                    // Mouse look or original mode: animations relative to camera
                    Vector3 characterForward = transform.forward;
                    Vector3 characterRight = transform.right;
                    
                    Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                    Vector3 cameraRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
                    
                    Vector3 inputDirection = (cameraRight * moveInput.x + cameraForward * moveInput.y).normalized;
                    
                    animVertical = Vector3.Dot(inputDirection, characterForward);
                    animHorizontal = Vector3.Dot(inputDirection, characterRight);
                }
            }

            float baseSpeedMultiplier = 1f;
            
            if (isCrouching)
            {
                baseSpeedMultiplier = crouchSpeedThreshold;
            }
            else if (isRunning)
            {
                baseSpeedMultiplier = runSpeedThreshold;
            }
            else
            {
                baseSpeedMultiplier = walkSpeedThreshold;
            }

            bool isPureStrafe = Mathf.Abs(animVertical) < 0.1f && Mathf.Abs(animHorizontal) > 0.7f;
            bool isPureBackward = animVertical < -0.7f && Mathf.Abs(animHorizontal) < 0.1f;
            bool isBackwardStrafe = animVertical < -backwardStrafeThreshold && Mathf.Abs(animHorizontal) > backwardStrafeThreshold;
            bool isForwardStrafe = animVertical > backwardStrafeThreshold && Mathf.Abs(animHorizontal) > backwardStrafeThreshold;

            isStrafing = isPureStrafe;
            isBackwardStrafing = isBackwardStrafe;

            if (isPureStrafe)
            {
                animSpeed = baseSpeedMultiplier * strafeSpeedMultiplier;
            }
            else if (isPureBackward)
            {
                animSpeed = baseSpeedMultiplier * backwardSpeedMultiplier;
            }
            else if (isBackwardStrafe)
            {
                animSpeed = baseSpeedMultiplier * backwardStrafeSpeedMultiplier;
            }
            else if (isForwardStrafe)
            {
                animSpeed = baseSpeedMultiplier * strafeSpeedMultiplier;
            }
            else
            {
                animSpeed = baseSpeedMultiplier * inputMagnitude;
            }
        }
        else
        {
            isStrafing = false;
            isBackwardStrafing = false;
            animHorizontal = 0f;
            animVertical = 0f;
            animSpeed = 0f;
        }

        wasMoving = isMoving;

        float smoothingTime = animationSmoothTime;
        
        if (!isMoving && (Mathf.Abs(smoothedHorizontal) > 0.01f || Mathf.Abs(smoothedVertical) > 0.01f))
        {
            smoothingTime = animationSmoothTime * 0.5f;
        }
        
        float horizontalSmoothing = isStrafing ? smoothingTime * 0.7f : smoothingTime;
        
        smoothedHorizontal = Mathf.SmoothDamp(smoothedHorizontal, animHorizontal, ref currentHorizontalVelocity, horizontalSmoothing);
        smoothedVertical = Mathf.SmoothDamp(smoothedVertical, animVertical, ref currentVerticalVelocity, smoothingTime);
        smoothedSpeed = Mathf.SmoothDamp(smoothedSpeed, animSpeed, ref currentSpeedVelocity, smoothingTime);

        smoothedHorizontal = Mathf.Clamp(smoothedHorizontal, -1f, 1f);
        smoothedVertical = Mathf.Clamp(smoothedVertical, -1f, 1f);
        smoothedSpeed = Mathf.Clamp(smoothedSpeed, 0f, 1f);

        animator.SetBool(animParamIsCrouching, isCrouching);
        
        animator.SetFloat(animParamSpeed, smoothedSpeed);
        animator.SetFloat(animParamHorizontal, smoothedHorizontal);
        animator.SetFloat(animParamVertical, smoothedVertical);
        animator.SetBool(animParamIsGrounded, isGrounded);
        animator.SetBool(animParamIsRunning, isRunning && isMoving && !isCrouching);
        animator.SetBool(animParamIsStrafing, isStrafing);
        animator.SetBool(animParamIsBackwardStrafing, isBackwardStrafing);

        float turnDirection = 0f;
        if (inputMagnitude < rotationDeadzone && !isFirstPerson && !isCrouching && Mathf.Abs(lookInput.x) > 0.5f)
        {
            turnDirection = Mathf.Sign(lookInput.x);
        }
        animator.SetFloat(animParamTurnDirection, turnDirection);

        if (debugAnimationStates)
        {
            Debug.Log($"Animation - Crouching: {isCrouching}, Speed: {smoothedSpeed:F2}, H: {smoothedHorizontal:F2}, V: {smoothedVertical:F2}");
        }
    }

    // FIXED: Complete rewrite of movement system
    private void UpdateMovement()
{
    isGrounded = characterController.isGrounded;

    Vector3 moveDir = Vector3.zero;
    float inputMagnitude = moveInput.magnitude;

    if (inputMagnitude > pivotThreshold)
    {
        if (isFirstPerson)
        {
            // First person: always relative to character
            moveDir = transform.right * moveInput.x + transform.forward * moveInput.y;
        }
        else
        {
            // Third person with WoW-style logic
            if (useWoWCameraStyle)
            {
                if (isMouseLookMode)
                {
                    // MOUSE LOOK MODE: Movement relative to camera (standard 3rd person)
                    Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                    Vector3 cameraRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
                    moveDir = cameraRight * moveInput.x + cameraForward * moveInput.y;
                    
                    // Character rotation handled by camera system
                }
                else
                {
                    // FREE LOOK MODE: Movement relative to character facing
                    // FIXED: More precise movement direction calculation
                    Vector3 characterForward = transform.forward;
                    Vector3 characterRight = transform.right;
                    
                    // Calculate raw movement direction
                    moveDir = characterRight * moveInput.x + characterForward * moveInput.y;
                    
                    // FIXED: Handle rotation BEFORE applying movement
                    HandleFreeLookMovementRotation();
                    
                    // IMPORTANT: Recalculate movement direction after potential rotation
                    // This ensures smooth movement even during rotation
                    characterForward = transform.forward;
                    characterRight = transform.right;
                    moveDir = characterRight * moveInput.x + characterForward * moveInput.y;
                }
            }
            else
            {
                // Original system: camera-relative with auto-rotation
                Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                Vector3 cameraRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
                moveDir = cameraRight * moveInput.x + cameraForward * moveInput.y;
                
                // Original rotation logic
                HandleOriginalMovementRotation(moveDir);
            }
        }

        moveDir = Vector3.ClampMagnitude(moveDir, 1f);
    }

    // Apply movement physics
    ApplyMovementPhysics(moveDir, inputMagnitude);
}
    
    private void UpdateWoWCamera()
{
    var mouse = UnityEngine.InputSystem.Mouse.current;
    
    if (mouse != null)
    {
        rightMousePressed = mouse.rightButton.isPressed;
        leftMousePressed = mouse.leftButton.isPressed;
    }
    
    // Determine right mouse look mode (unchanged)
    bool rightMouseLookMode = false;
    if (requireBothButtons)
    {
        rightMouseLookMode = rightMousePressed && leftMousePressed;
    }
    else if (useRightMouseButton && useLeftMouseButton)
    {
        rightMouseLookMode = rightMousePressed || leftMousePressed;
    }
    else if (useRightMouseButton)
    {
        rightMouseLookMode = rightMousePressed;
    }
    else if (useLeftMouseButton)
    {
        rightMouseLookMode = leftMousePressed;
    }
    else
    {
        rightMouseLookMode = rightMousePressed;
    }
    
    // NEW: Determine left mouse camera mode (only when not holding object)
    bool leftMouseCameraMode = enableLeftMouseCamera && leftMousePressed && !isHoldingObject;
    
    // Combined mouse look mode
    isMouseLookMode = rightMouseLookMode;
    leftMouseCameraActive = leftMouseCameraMode;
    
    // Handle cursor state
    if (isMouseLookMode || leftMouseCameraActive)
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Reset camera follow when any mouse control is active
        cameraFollowTimer = 0f;
        shouldFollowCamera = false;
    }
    else
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Handle different mouse control modes
    if (isMouseLookMode)
    {
        // RIGHT MOUSE MODE: Mouse controls character rotation (unchanged)
        HandleRightMouseLookMode();
    }
    else if (leftMouseCameraActive)
    {
        // LEFT MOUSE MODE: Mouse controls camera only, character doesn't rotate
        HandleLeftMouseCameraMode();
    }
    else
    {
        // FREE LOOK MODE: No mouse control, automatic camera following
        HandleFreeLookMode();
    }
    
    // NEW: Handle object holding camera behavior
    HandleObjectHoldingCamera();
    
    wasHoldingObjectLastFrame = isHoldingObject;
}

// NEW: Right mouse look mode (extracted from original)
private void HandleRightMouseLookMode()
{
    float sensitivity = mouseLookSensitivity;
    
    // Rotate character horizontally
    float horizontalInput = lookInput.x * sensitivity;
    transform.Rotate(Vector3.up * horizontalInput);
    
    // Rotate camera vertically
    verticalRotation -= lookInput.y * sensitivity;
    verticalRotation = Mathf.Clamp(verticalRotation, minVerticalAngle, maxVerticalAngle);
    
    // Keep horizontal rotation synced
    horizontalRotation = transform.eulerAngles.y;
    
    // Update free look rotation to match current state
    freeLookRotation = new Vector2(horizontalRotation, verticalRotation);
}

// NEW: Left mouse camera mode (camera only, no character rotation)
private void HandleLeftMouseCameraMode()
{
    float sensitivity = leftMouseCameraSensitivity;
    
    // Only rotate camera, not character
    freeLookRotation.x += lookInput.x * sensitivity;
    freeLookRotation.y -= lookInput.y * sensitivity;
    freeLookRotation.y = Mathf.Clamp(freeLookRotation.y, minVerticalAngle, maxVerticalAngle);
    
    // Update camera rotation
    horizontalRotation = freeLookRotation.x;
    verticalRotation = freeLookRotation.y;
    
    // Stop automatic camera following during manual control
    shouldFollowCamera = false;
    cameraFollowTimer = 0f;
}

// NEW: Free look mode (no mouse input)
private void HandleFreeLookMode()
{
    // Handle camera following logic (automatic camera positioning)
    UpdateCameraFollowing();
    
    if (shouldFollowCamera)
    {
        // Automatic camera following
        UpdateAutomaticCameraFollow();
    }
    
    // No manual camera rotation from mouse in this mode
}

// NEW: Handle camera behavior when holding objects
private void HandleObjectHoldingCamera()
{
    if (isHoldingObject)
    {
        // When holding an object, force camera to point forward behind character
        if (!wasHoldingObjectLastFrame)
        {
            // Just picked up an object - start forcing camera behind character
            Debug.Log("Object picked up - forcing camera behind character");
        }
        
        // Force camera to follow character facing direction
        float targetHorizontalRotation = transform.eulerAngles.y;
        
        // Smoothly move camera to behind character
        float currentHorizontal = horizontalRotation;
        float newHorizontal = Mathf.LerpAngle(currentHorizontal, targetHorizontalRotation, 
            Time.deltaTime * cameraFollowSpeed * 2f); // Faster when holding object
        
        horizontalRotation = newHorizontal;
        freeLookRotation.x = horizontalRotation;
        
        // Disable camera following and manual control
        shouldFollowCamera = false;
        cameraFollowTimer = 0f;
        
        // Keep vertical rotation but slightly elevated for better throwing view
        float targetVerticalRotation = 5f; // Slightly look down for throwing
        verticalRotation = Mathf.Lerp(verticalRotation, targetVerticalRotation, Time.deltaTime * 2f);
        freeLookRotation.y = verticalRotation;
    }
    else if (wasHoldingObjectLastFrame && !isHoldingObject)
    {
        // Just dropped/threw an object - resume normal camera behavior
        Debug.Log("Object dropped - resuming normal camera behavior");
        
        // No special handling needed, camera will resume normal free look behavior
    }
}
    
    private void UpdateCameraFollowing()
{
    bool isCurrentlyMoving = moveInput.magnitude > pivotThreshold;
    float currentCharacterAngle = transform.eulerAngles.y;
    
    // Track movement state
    if (isCurrentlyMoving && !wasMovingLastFrame)
    {
        // Just started moving
        movementTimer = 0f;
    }
    else if (isCurrentlyMoving)
    {
        // Continuing to move
        movementTimer += Time.deltaTime;
    }
    else if (!isCurrentlyMoving && wasMovingLastFrame)
    {
        // Just stopped moving
        cameraFollowTimer = 0f;
        shouldFollowCamera = false;
    }
    
    // Check if character has rotated significantly
    float angleDifference = Mathf.Abs(Mathf.DeltaAngle(lastCharacterAngle, currentCharacterAngle));
    
    // Determine if camera should follow
    if (enableCameraFollow)
    {
        if (onlyFollowWhenMoving)
        {
            // Only follow when moving
            if (isCurrentlyMoving && movementTimer > movementFollowDelay)
            {
                // Check if camera is significantly off from character facing
                float cameraCharacterAngleDiff = Mathf.Abs(Mathf.DeltaAngle(horizontalRotation, currentCharacterAngle));
                
                if (cameraCharacterAngleDiff > cameraFollowThreshold)
                {
                    cameraFollowTimer += Time.deltaTime;
                    
                    if (cameraFollowTimer > cameraFollowDelay)
                    {
                        shouldFollowCamera = true;
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
            // Follow anytime character rotates significantly
            if (angleDifference > cameraFollowThreshold)
            {
                cameraFollowTimer += Time.deltaTime;
                
                if (cameraFollowTimer > cameraFollowDelay)
                {
                    shouldFollowCamera = true;
                }
            }
            else
            {
                cameraFollowTimer = 0f;
                shouldFollowCamera = false;
            }
        }
    }
    
    // REMOVED: No longer reset follow on manual mouse input since mouse is free
    // The camera only follows automatically or stays put
    
    wasMovingLastFrame = isCurrentlyMoving;
    lastCharacterAngle = currentCharacterAngle;
}
    
    private void UpdateAutomaticCameraFollow()
    {
        float targetHorizontalRotation = transform.eulerAngles.y;
    
        // Smoothly rotate camera to face behind character
        float currentHorizontal = horizontalRotation;
        float newHorizontal = Mathf.LerpAngle(currentHorizontal, targetHorizontalRotation, 
            Time.deltaTime * cameraFollowSpeed);
    
        horizontalRotation = newHorizontal;
        verticalRotation = freeLookRotation.y; // Keep vertical rotation unchanged
    
        // Update free look rotation to match
        freeLookRotation.x = horizontalRotation;
    
        // Stop following when camera is close enough to target
        float angleDifference = Mathf.Abs(Mathf.DeltaAngle(horizontalRotation, targetHorizontalRotation));
        if (angleDifference < 2f) // Within 2 degrees
        {
            shouldFollowCamera = false;
            cameraFollowTimer = 0f;
        }
    }

    // FIXED: New method for handling free look movement rotation
    private void HandleFreeLookMovementRotation()
{
    if (moveInput.magnitude > 0.1f)
    {
        // Calculate desired movement direction
        Vector3 inputDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
        inputDirection.y = 0f;
        inputDirection = inputDirection.normalized;
        
        // FIXED: Handle both forward AND backward turning
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
                // We want the character to turn while walking backward
                targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg;
                
                // For backward movement, we actually want to turn the opposite way
                // This makes S+A turn the character left while backing up (like a car)
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
                if (smoothFreeLookRotation)
                {
                    // FIXED: Use different rotation speeds for forward vs backward
                    float rotationSpeed = isMovingForward ? freeLookRotationSpeed : freeLookRotationSpeed * 0.7f;
                    
                    float rotationStep = rotationSpeed * Time.deltaTime;
                    rotationStep = Mathf.Min(rotationStep, maxRotationSpeed * Time.deltaTime);
                    
                    float newAngle = Mathf.LerpAngle(currentAngle, targetAngle, rotationStep);
                    transform.rotation = Quaternion.Euler(0, newAngle, 0);
                }
                else
                {
                    // Direct rotation
                    transform.rotation = Quaternion.Euler(0, targetAngle, 0);
                }
            }
        }
        // Pure forward/backward or pure strafe - no rotation (unchanged)
    }
}
    
    // FIXED: Cleaned up original movement rotation
    private void HandleOriginalMovementRotation(Vector3 moveDir)
    {
        if (moveDir.magnitude > pivotThreshold)
        {
            lastMovementDirection = moveDir;
            
            float targetYRotation = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
            
            Vector3 characterForward = transform.forward;
            float forwardComponent = Vector3.Dot(moveDir.normalized, characterForward);
            float rightComponent = Vector3.Dot(moveDir.normalized, transform.right);
            
            bool isPureBackward = forwardComponent < -0.8f && Mathf.Abs(rightComponent) < 0.3f;
            bool isPureStrafe = Mathf.Abs(rightComponent) > 0.8f && Mathf.Abs(forwardComponent) < 0.3f;
            bool isBackwardStrafe = forwardComponent < -backwardStrafeThreshold && Mathf.Abs(rightComponent) > backwardStrafeThreshold;
            
            bool shouldRotate = !(isPureBackward || isPureStrafe || isBackwardStrafe);
            
            if (shouldRotate)
            {
                targetPlayerRotation = targetYRotation;
                
                if (!instantPivot)
                {
                    float currentYRotation = transform.eulerAngles.y;
                    float smoothedRotation = Mathf.SmoothDampAngle(currentYRotation, targetPlayerRotation, ref rotationVelocity, rotationSmoothTime);
                    transform.rotation = Quaternion.Euler(0, smoothedRotation, 0);
                }
                else
                {
                    transform.rotation = Quaternion.Euler(0, targetPlayerRotation, 0);
                }
            }
        }
    }

    // FIXED: Improved physics application
    private void ApplyMovementPhysics(Vector3 moveDir, float inputMagnitude)
    {
        float targetSpeed = walkSpeed;
        
        if (isCrouching)
        {
            targetSpeed = crouchSpeed;
        }
        else if (isRunning)
        {
            targetSpeed = runSpeed;
        }

        // Apply directional speed modifiers only in third person
        if (!isFirstPerson && moveDir.magnitude > 0.1f)
        {
            Vector3 characterForward = transform.forward;
            float forwardDot = Vector3.Dot(moveDir.normalized, characterForward);
            float rightDot = Vector3.Dot(moveDir.normalized, transform.right);

            bool isPureBackward = forwardDot < -0.7f && Mathf.Abs(rightDot) < 0.3f;
            bool isPureStrafe = Mathf.Abs(rightDot) > 0.7f && Mathf.Abs(forwardDot) < 0.3f;
            bool isBackwardStrafe = forwardDot < -backwardStrafeThreshold && Mathf.Abs(rightDot) > backwardStrafeThreshold;
            bool isForwardStrafe = forwardDot > backwardStrafeThreshold && Mathf.Abs(rightDot) > backwardStrafeThreshold;

            if (isPureBackward)
            {
                targetSpeed *= backwardSpeedMultiplier;
            }
            else if (isPureStrafe)
            {
                targetSpeed *= strafeSpeedMultiplier;
            }
            else if (isBackwardStrafe)
            {
                targetSpeed *= backwardStrafeSpeedMultiplier;
            }
            else if (isForwardStrafe)
            {
                targetSpeed *= strafeSpeedMultiplier;
            }
        }

        // Calculate target velocity
        targetVelocity = moveDir * targetSpeed;
        
        // Separate horizontal and vertical velocity handling
        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        Vector3 targetHorizontalVelocity = new Vector3(targetVelocity.x, 0f, targetVelocity.z);
        
        // Apply acceleration/deceleration
        float acceleration = targetHorizontalVelocity.magnitude > horizontalVelocity.magnitude ? 
            movementAcceleration : movementDeceleration;
        
        // FIXED: Proper velocity interpolation
        horizontalVelocity = Vector3.Lerp(horizontalVelocity, targetHorizontalVelocity, 
            Mathf.Clamp01(acceleration * Time.deltaTime));
        
        currentVelocity.x = horizontalVelocity.x;
        currentVelocity.z = horizontalVelocity.z;
        currentMovementSpeed = horizontalVelocity.magnitude;

        // Handle vertical movement
        if (!isGrounded)
        {
            if (inputMagnitude > 0.1f)
            {
                Vector3 airMovement = moveDir * targetSpeed * airControl;
                currentVelocity.x = Mathf.Lerp(currentVelocity.x, airMovement.x, Time.deltaTime * 2f);
                currentVelocity.z = Mathf.Lerp(currentVelocity.z, airMovement.z, Time.deltaTime * 2f);
            }
            currentVelocity.y += Physics.gravity.y * gravityMultiplier * Time.deltaTime;
        }
        else
        {
            if (currentVelocity.y < 0)
                currentVelocity.y = -2f;
        }

        // Apply final movement
        Vector3 movement = currentVelocity * Time.deltaTime;
        characterController.Move(movement);

        // Debug output
        if (debugMovementValues && inputMagnitude > 0.1f)
        {
            Debug.Log($"Movement Debug - Input: {inputMagnitude:F2}, TargetSpeed: {targetSpeed:F2}, " +
                      $"CurrentSpeed: {currentMovementSpeed:F2}, MoveDir: {moveDir}, MouseLook: {isMouseLookMode}");
        }
    }

    private void UpdateCameraRotation()
    {
        if (isFirstPerson)
        {
            transform.Rotate(Vector3.up * lookInput.x * mouseSensitivity);

            verticalRotation -= lookInput.y * mouseSensitivity;
            verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
            cameraHolder.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
        }
        else
        {
            if (useWoWCameraStyle)
            {
                UpdateWoWCamera();
            }
            else
            {
                horizontalRotation += lookInput.x * mouseSensitivity;
                verticalRotation -= lookInput.y * mouseSensitivity;
                verticalRotation = Mathf.Clamp(verticalRotation, minVerticalAngle, maxVerticalAngle);
            }
        }
    }

    private void UpdateZoom()
    {
        if (!isFirstPerson)
        {
            currentCameraDistance = Mathf.Lerp(currentCameraDistance, targetCameraDistance, Time.deltaTime * zoomSpeed);
            thirdPersonDistance = currentCameraDistance;
        }
    }

    private void UpdateCameraPosition()
    {
        if (!isFirstPerson)
        {
            Vector3 playerTargetPosition = transform.position + thirdPersonOffset;
            Vector3 directionToCamera = Quaternion.Euler(verticalRotation, horizontalRotation, 0f) * -Vector3.forward;
            Vector3 desiredCameraPos = playerTargetPosition + directionToCamera * thirdPersonDistance;

            if (Physics.Raycast(playerTargetPosition, directionToCamera, out RaycastHit hit, thirdPersonDistance))
            {
                desiredCameraPos = hit.point + directionToCamera * cameraCollisionOffset;
            }

            cameraTransform.position = desiredCameraPos;
            cameraTransform.LookAt(playerTargetPosition);
        }
        else
        {
            cameraTransform.position = Vector3.Lerp(cameraTransform.position,
                firstPersonPosition.position, Time.deltaTime * cameraTransitionSpeed);
        }
    }

    private bool CheckCanStandUp()
    {
        if (!isCrouching) return true;
        
        Vector3 bottom = transform.position + characterController.center - Vector3.up * (characterController.height * 0.5f);
        Vector3 top = bottom + Vector3.up * standingHeight;
        
        float checkRadius = characterController.radius * 0.8f;
        
        bool blocked = false;
        
        if (Physics.CheckCapsule(bottom + Vector3.up * checkRadius, 
                               top - Vector3.up * checkRadius, 
                               checkRadius, standUpObstacleMask))
        {
            blocked = true;
        }
        
        Vector3 headCheck = transform.position + Vector3.up * (standingHeight - 0.1f);
        if (Physics.CheckSphere(headCheck, checkRadius, standUpObstacleMask))
        {
            blocked = true;
        }
        
        if (debugCrouchChecks)
        {
            Color debugColor = blocked ? Color.red : Color.green;
            Debug.DrawLine(bottom, top, debugColor, 0.1f);
           
        }
        
        return !blocked;
    }

    private void StartCrouch()
    {
        if (isCrouching) return;
        
        Debug.Log("Starting crouch (input held)");
        
        isCrouching = true;
        targetHeight = crouchHeight;
        crouchStateTimer = 0f;
        canStandUp = false;
        
        if (animator != null)
        {
            animator.SetBool(animParamIsCrouching, true);
        }
        
        if (adjustControllerCenter)
        {
            float heightDifference = standingHeight - crouchHeight;
            targetControllerCenterY = originalControllerCenterY - (heightDifference * crouchCenterOffset);
        }

        if (isFirstPerson)
        {
            float heightDifference = standingHeight - crouchHeight;
            targetCameraYOffset = -heightDifference * 0.5f;
        }
    }

    private void StopCrouch()
    {
        if (!isCrouching) return;
        
        if (!forceStandUpNextFrame && !CheckCanStandUp())
        {
            Debug.Log("Cannot stand up - obstacle detected, keeping crouch state");
            return;
        }
        
        Debug.Log("Stopping crouch (input released)");
        
        isCrouching = false;
        targetHeight = standingHeight;
        targetControllerCenterY = originalControllerCenterY;
        crouchStateTimer = 0f;

        if (animator != null)
        {
            animator.SetBool(animParamIsCrouching, false);
        }

        if (isFirstPerson)
        {
            targetCameraYOffset = 0f;
        }
    }

    private void UpdateCrouchTransition()
    {
        characterController.height = Mathf.Lerp(characterController.height, targetHeight,
            Time.deltaTime * crouchTransitionSpeed);
        
        if (adjustControllerCenter)
        {
            Vector3 currentCenter = characterController.center;
            currentCenter.y = Mathf.Lerp(currentCenter.y, targetControllerCenterY,
                Time.deltaTime * crouchTransitionSpeed);
            characterController.center = currentCenter;
        }

        if (isFirstPerson)
        {
            currentCameraYOffset = Mathf.Lerp(currentCameraYOffset, targetCameraYOffset,
                Time.deltaTime * crouchTransitionSpeed);
        }
        
        if (animator != null)
        {
            animator.SetBool(animParamIsCrouching, isCrouching);
        }
    }

    private void UpdateHeadBob()
    {
        Vector3 baseCameraPosition = originalCameraPosition;
        baseCameraPosition.y += currentCameraYOffset;

        if (isGrounded && moveInput.magnitude > pivotThreshold)
        {
            float currentBobFrequency;
            float currentBobAmount;
            float speedMultiplier;

            if (isCrouching && enableCrouchHeadBob)
            {
                currentBobFrequency = crouchBobFrequency;
                currentBobAmount = crouchBobAmount;
                speedMultiplier = crouchSpeed / walkSpeed;
            }
            else if (!isCrouching)
            {
                currentBobFrequency = bobFrequency;
                currentBobAmount = bobAmount;
                float currentSpeed = isRunning ? runSpeed : walkSpeed;
                speedMultiplier = currentSpeed / walkSpeed;
            }
            else
            {
                bobTimer = 0;
                cameraHolder.localPosition = Vector3.Lerp(
                    cameraHolder.localPosition,
                    baseCameraPosition,
                    Time.deltaTime * 5f
                );
                return;
            }

            bobTimer += Time.deltaTime * currentBobFrequency * speedMultiplier;

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
            cameraHolder.localPosition = Vector3.Lerp(
                cameraHolder.localPosition,
                baseCameraPosition,
                Time.deltaTime * 5f
            );
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

    private IEnumerator EyeCloseTransition()
    {
        isTransitioning = true;

        float fadeInDuration = eyeCloseTransitionDuration * 0.3f;
        float elapsedTime = 0f;

        while (elapsedTime < fadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsedTime / fadeInDuration);
            eyeCloseImage.color = new Color(eyeCloseColor.r, eyeCloseColor.g, eyeCloseColor.b, alpha);
            yield return null;
        }

        transform.rotation = Quaternion.Euler(0, horizontalRotation, 0);

        horizontalRotation = transform.eulerAngles.y;
        verticalRotation = 0f;

        Vector3 playerTargetPosition = transform.position + thirdPersonOffset;
        Vector3 directionToCamera = Quaternion.Euler(verticalRotation, horizontalRotation, 0f) * -Vector3.forward;

        currentCameraDistance = defaultCameraDistance;
        targetCameraDistance = defaultCameraDistance;
        thirdPersonDistance = defaultCameraDistance;

        Vector3 desiredCameraPos = playerTargetPosition + directionToCamera * currentCameraDistance;

        cameraTransform.position = desiredCameraPos;
        cameraTransform.LookAt(playerTargetPosition);

        isFirstPerson = false;
        cameraHolder.localPosition = originalCameraPosition;
        cameraHolder.localRotation = Quaternion.identity;

        yield return new WaitForSeconds(eyeCloseTransitionDuration * 0.4f);

        float fadeOutDuration = eyeCloseTransitionDuration * 0.3f;
        elapsedTime = 0f;

        while (elapsedTime < fadeOutDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsedTime / fadeOutDuration);
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

        float targetPlayerRotationValue = horizontalRotation;

        Vector3 targetPosition = firstPersonPosition.position;
        Quaternion targetRotation = Quaternion.Euler(0f, targetPlayerRotationValue, 0f);

        float elapsedTime = 0f;

        while (elapsedTime < smoothTransitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = smoothTransitionCurve.Evaluate(elapsedTime / smoothTransitionDuration);

            Vector3 straightPath = Vector3.Lerp(startPosition, targetPosition, progress);

            float arcOffset = Mathf.Sin(progress * Mathf.PI) * avoidanceArcHeight;
            Vector3 currentPos = straightPath + Vector3.up * arcOffset;

            cameraTransform.position = currentPos;

            cameraTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, progress);

            yield return null;
        }

        isFirstPerson = true;
        transform.rotation = Quaternion.Euler(0f, targetPlayerRotationValue, 0f);
        verticalRotation = 0f;
        cameraHolder.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
        currentCameraYOffset = targetCameraYOffset;

        cameraTransform.position = firstPersonPosition.position;
        cameraTransform.rotation = transform.rotation;

        isTransitioning = false;
    }

    private void TryJump()
    {
        if (isGrounded && !isCrouching && !isJumpQueued)
        {
            isJumpQueued = true;
            jumpTimer = 0f;
            
            if (animator != null)
            {
                animator.SetTrigger(animParamJump);
            }
        }
        else if (useJumpBuffer && !isGrounded)
        {
            jumpBufferTimer = jumpBufferTime;
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
                if (UnityEngine.Random.value > 0.5f)
                {
                    animator.SetTrigger(animParamPunch);
                }
                else
                {
                    animator.SetTrigger(animParamKick);
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

        if (animator != null)
        {
            animator.SetTrigger(animParamInteract);
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
            GameObject targetObject = null;

            if (isFirstPerson)
            {
                if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, pickupRange, pickupMask))
                {
                    if (hit.collider.TryGetComponent(out SimplePickup pickup))
                    {
                        if (pickup.CanBePickedUp())
                        {
                            targetObject = pickup.gameObject;
                        }
                    }
                }
            }
            else
            {
                Vector3 playerCenter = transform.position + Vector3.up * 1.0f;
                Collider[] nearbyObjects = Physics.OverlapSphere(playerCenter, pickupRange, pickupMask);

                float closestDistance = float.MaxValue;
                foreach (Collider col in nearbyObjects)
                {
                    if (col.TryGetComponent(out SimplePickup pickup))
                    {
                        if (pickup.CanBePickedUp())
                        {
                            float distance = Vector3.Distance(playerCenter, col.transform.position);
                            if (distance < closestDistance)
                            {
                                closestDistance = distance;
                                targetObject = pickup.gameObject;
                            }
                        }
                    }
                }
            }

            if (targetObject != null)
            {
                PickupObject(targetObject);
            }
        }
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

        Transform parentTransform = cameraTransform;
        obj.transform.SetParent(parentTransform);

        if (isFirstPerson)
        {
            obj.transform.localPosition = Vector3.forward * holdDistance;
        }
        else
        {
            Vector3 playerForward = cameraTransform.forward;
            Vector3 holdPosition = transform.position + Vector3.up * 1.5f + playerForward * holdDistance;
            obj.transform.position = holdPosition;
        }

        obj.transform.localRotation = Quaternion.identity;

        if (animator != null)
        {
            animator.SetTrigger(animParamPickup);
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
                animator.SetTrigger(animParamDrop);
            }
        }
    }

    private void UpdateHeldObject()
    {
        if (isHoldingObject && heldObject != null)
        {
            if (isFirstPerson)
            {
                Transform parentTransform = cameraTransform;
                if (heldObject.transform.parent != parentTransform)
                {
                    heldObject.transform.SetParent(parentTransform);
                }

                Vector3 targetPosition = parentTransform.position + parentTransform.forward * holdDistance;
                heldObject.transform.position = Vector3.Lerp(heldObject.transform.position, targetPosition, Time.deltaTime * holdPositionSpeed);
                heldObject.transform.rotation = Quaternion.Lerp(heldObject.transform.rotation, parentTransform.rotation, Time.deltaTime * holdPositionSpeed);
            }
            else
            {
                heldObject.transform.SetParent(null);

                Vector3 playerForward = cameraTransform.forward;
                Vector3 targetPosition = transform.position + Vector3.up * 1.5f + playerForward * holdDistance;
                heldObject.transform.position = Vector3.Lerp(heldObject.transform.position, targetPosition, Time.deltaTime * holdPositionSpeed);

                heldObject.transform.rotation = Quaternion.Lerp(heldObject.transform.rotation, cameraTransform.rotation, Time.deltaTime * holdPositionSpeed);
            }

            if (heldObjectRb != null)
            {
                heldObjectRb.linearVelocity = Vector3.zero;
                heldObjectRb.angularVelocity = Vector3.zero;
            }
        }
    }

    private void StartChargingThrow()
    {
        if (isHoldingObject && heldObject != null && !isChargingThrow)
        {
            // Only allow throwing when holding an object
            isChargingThrow = true;
            throwChargeTimer = 0f;
            ShowThrowBar(true);
        
            Debug.Log("Started charging throw - left mouse reserved for throwing");
        }
    }

    private void ReleaseThrow()
    {
        if (isChargingThrow && isHoldingObject && heldObject != null)
        {
            float chargePercent = Mathf.Clamp01(throwChargeTimer / maxHoldTime);
            float force = Mathf.Lerp(minThrowForce, maxThrowForce, chargePercent);
            ThrowObject(force);
            isChargingThrow = false;
            throwChargeTimer = 0f;
            ShowThrowBar(false);
        
            Debug.Log("Released throw - left mouse now available for camera");
        }
    }
    
        private void UpdateThrowCharge()
    {
        if (isChargingThrow)
        {
            throwChargeTimer += Time.deltaTime;
            float percent = Mathf.Clamp01(throwChargeTimer / maxHoldTime);
            UpdateThrowBar(percent);
        }
    }

    private void ThrowObject(float force)
    {
        heldObject.transform.SetParent(null);

        if (heldObjectRb != null)
        {
            heldObjectRb.useGravity = true;
            heldObjectRb.isKinematic = false;
            heldObjectRb.linearVelocity = Vector3.zero;
            heldObjectRb.angularVelocity = Vector3.zero;

            Vector3 throwDirection = cameraTransform.forward;
            heldObjectRb.AddForce(throwDirection * force);
            heldObjectRb.AddForce(Vector3.up * 0.2f * force);
        }

        heldObject = null;
        heldObjectRb = null;
        isHoldingObject = false;

        if (animator != null)
        {
            animator.SetTrigger(animParamThrow);
        }
    }

    private void Start()
    {
        if (throwPowerBar != null)
            throwPowerBar.gameObject.SetActive(false);
        
        // Initialize WoW camera system
        if (useWoWCameraStyle && !isFirstPerson)
        {
            freeLookRotation = new Vector2(transform.eulerAngles.y, 0f);
            horizontalRotation = transform.eulerAngles.y;
            verticalRotation = 0f;
            
            // Start in free look mode
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
    

    private void ShowThrowBar(bool show)
    {
        if (throwPowerBar != null)
            throwPowerBar.gameObject.SetActive(show);
    }

    private void UpdateThrowBar(float percent)
    {
        if (throwPowerBar != null)
            throwPowerBar.fillAmount = percent;
    }

    private void OnDestroy()
    {
        if (overlayCanvas != null)
        {
            Destroy(overlayCanvas.gameObject);
        }
    }

    // Debug GUI for movement troubleshooting
    private void OnGUI()
    {
        if (debugMovementValues)
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label($"Mouse Look Mode: {isMouseLookMode}");
            GUILayout.Label($"Move Input: {moveInput}");
            GUILayout.Label($"Current Speed: {currentMovementSpeed:F2}");
            GUILayout.Label($"Is Running: {isRunning}");
            GUILayout.Label($"Is Crouching: {isCrouching}");
            GUILayout.Label($"Character Rotation: {transform.eulerAngles.y:F1}°");
            GUILayout.Label($"Camera Rotation: {horizontalRotation:F1}°");
            GUILayout.Label($"Use WoW Camera: {useWoWCameraStyle}");
            GUILayout.EndArea();
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

            if (isFirstPerson)
            {
                Gizmos.color = Color.green;
                Vector3 rayStart = cameraTransform.position;
                Vector3 rayEnd = rayStart + cameraTransform.forward * pickupRange;
                Gizmos.DrawLine(rayStart, rayEnd);
                Gizmos.DrawWireSphere(rayEnd, 0.1f);
            }
            else
            {
                Gizmos.color = Color.green;
                Vector3 playerCenter = transform.position + Vector3.up * 1.0f;
                Gizmos.DrawWireSphere(playerCenter, pickupRange);
            }
        }

        // Draw crouch check gizmos
        if (debugCrouchChecks && isCrouching)
        {
            Vector3 bottom = transform.position + characterController.center - Vector3.up * (characterController.height * 0.5f);
            Vector3 top = bottom + Vector3.up * standingHeight;
            float checkRadius = characterController.radius * 0.8f;
            
            Gizmos.color = canStandUp ? Color.green : Color.red;
            Gizmos.DrawWireCube((bottom + top) * 0.5f, new Vector3(checkRadius * 2, standingHeight, checkRadius * 2));
            
            Vector3 headCheck = transform.position + Vector3.up * (standingHeight - 0.1f);
            Gizmos.DrawWireSphere(headCheck, checkRadius);
        }
    }
}

// Interface definitions
public interface IInteractable
{
    void Interact();
}

public interface IDamageable
{
    void TakeDamage(int amount);
}