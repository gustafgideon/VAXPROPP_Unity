using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    #region Inspector: Camera Setup
    [Header("Camera Setup")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform firstPersonPosition;
    [SerializeField] private Transform thirdPersonPosition;
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private float cameraTransitionSpeed = 10f;
    #endregion

    #region Inspector: Animation
    [Header("Animation")]
    [SerializeField] private Animator animator;
    #endregion

    #region Inspector: WoW-Style Camera
    [Header("WoW-Style Camera Settings")]
    [SerializeField] private bool useWoWCameraStyle = true;
    [SerializeField] private float freeLookSensitivity = 2f;
    [SerializeField] private float mouseLookSensitivity = 2f;
    [SerializeField] private bool useRightMouseButton = true;
    [SerializeField] private bool enableLeftMouseCamera = true;
    [SerializeField] private float leftMouseCameraSensitivity = 1.5f;
    #endregion
    
    #region Inspector: Turning Tweaks
    [Header("Turning Tweaks")]
    [SerializeField, Tooltip("Multiplier (<1 slows) applied to rotation when holding forward+strafe (W+A / W+D) to create a wider turning arc.")]
    private float diagonalTurnSpeedMultiplier = 0.4f;
    #endregion

    #region Inspector: Camera Follow
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
    #endregion

    #region Inspector: Third Person Camera
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
    #endregion

    #region Inspector: Movement
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
    #endregion

    #region Inspector: Animation Tuning
    [Header("Animation Settings")]
    [SerializeField] private float animationSmoothTime = 0.1f;
    [SerializeField] private float walkSpeedThreshold = 0.5f;
    [SerializeField] private float runSpeedThreshold = 0.8f;
    [SerializeField] private float crouchSpeedThreshold = 0.3f;
    [SerializeField] private float idleThreshold = 0.1f;
    [SerializeField] private float freeLookRotationSpeed = 2.4f;
    [SerializeField] private float rotationAngleThreshold = 5f;
    #endregion

    #region Inspector: Head Bob
    [Header("Head Bob Settings")]
    [SerializeField] private float bobFrequency = 2f;
    [SerializeField] private float bobAmount = 0.05f;
    [SerializeField] private float crouchBobFrequency = 1.5f;
    [SerializeField] private float crouchBobAmount = 0.03f;
    [SerializeField] private bool enableCrouchHeadBob = true;
    #endregion

    #region Inspector: Interaction
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
    #endregion

    #region Inspector: UI
    [Header("UI")]
    [SerializeField] private Image throwPowerBar;
    [SerializeField] private float eyeCloseTransitionDuration = 0.6f;
    [SerializeField] private Color eyeCloseColor = Color.black;
    [SerializeField, Tooltip("When orbiting with Left Mouse (free look in 3rd person), restore the cursor to its original screen position on release.")]
    private bool restoreCursorPositionAfterOrbit = true;
    [SerializeField, Tooltip("Hide and lock cursor strictly in first person (prevents any accidental reappearance).")]
    private bool forceLockedCursorInFirstPerson = true;
    #endregion

    #region Inspector: Debug
    [Header("Debug")]
    [SerializeField] private bool debugAnimationStates = false;
    [SerializeField] private bool debugMovementValues = false;
    [SerializeField] private bool debugCameraFollow = false;
    #endregion

    #region Animator Parameter Hashes
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
    #endregion

    #region Components & Cached
    private CharacterController characterController;
    private PlayerInput playerInput;
    private Transform cameraTransform;
    #endregion

    #region UI Runtime
    private GameObject eyeCloseOverlay;
    private Image eyeCloseImage;
    private bool wasLeftMouseCameraActive = false;
    private Vector2 storedCursorPosition;
    private bool wasRightMouseLookActive = false;
    private Vector2 storedRightCursorPosition;
    private bool pendingLeftOrbitActivation = false;
    private int leftOrbitEnterFrame = -1;
    [SerializeField, Tooltip("Ignore (smooth out) the very first look delta frame after entering left orbit to avoid a perceived hitch.")]
    private bool dampFirstOrbitDeltaFrame = true;
    #endregion

    #region Input State
    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector2 smoothedLookInput;
    private Vector2 lookInputVelocity;
    #endregion

    #region Movement State
    private Vector3 currentVelocity;
    private Vector3 targetVelocity;
    private float currentMovementSpeed;
    private bool isGrounded;
    private bool isRunning;
    private bool isMoving;
    private Vector3 jumpMomentumDirection;
    private float jumpMomentumSpeed;
    #endregion

    #region Camera State
    private bool isFirstPerson = false;
    private float verticalRotation;
    private float horizontalRotation;
    private Vector2 freeLookRotation;
    private bool isMouseLookMode = false;
    private bool leftMouseCameraActive = false;
    private float currentCameraDistance;
    private float targetCameraDistance;
    #endregion

    #region Animation State
    private float smoothedHorizontal;
    private float smoothedVertical;
    private float smoothedSpeed;
    private float horizontalVelocity;
    private float verticalVelocityAnim;
    private float speedVelocity;
    private bool isStrafing;
    private bool isBackwardStrafing;
    #endregion

    #region Crouch State
    private bool isCrouching;
    private float targetHeight;
    private float currentCameraYOffset;
    private float targetCameraYOffset;
    private float originalControllerCenterY;
    private float targetControllerCenterY;
    #endregion

    #region Jump State
    private bool isJumpQueued = false;
    private float jumpTimer = 0f;
    #endregion

    #region Head Bob State
    private float bobTimer;
    private Vector3 originalCameraPosition;
    #endregion

    #region Interaction / Pickup State
    private GameObject heldObject;
    private Rigidbody heldObjectRb;
    private bool isHoldingObject = false;
    private bool isChargingThrow = false;
    private float throwChargeTimer = 0f;
    private float currentAttackCooldown;
    #endregion

    #region Camera Follow State
    private float cameraFollowTimer = 0f;
    private float movementTimer = 0f;
    private bool wasMovingLastFrame = false;
    private float lastCharacterAngle = 0f;
    private bool shouldFollowCamera = false;
    private float currentFollowSpeed = 2f;
    #endregion

    #region Transition State
    private bool isTransitioning = false;
    #endregion

    #region Direction Caches
    private Vector3 cachedCharacterForward;
    private Vector3 cachedCharacterRight;
    private Vector3 cachedCameraForward;
    private Vector3 cachedCameraRight;
    private bool directionsNeedUpdate = true;
    #endregion

    #region Mouse State
    private UnityEngine.InputSystem.Mouse cachedMouse;
    private bool rightMousePressed = false;
    private bool leftMousePressed = false;
    #endregion

    #region Input Actions
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
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        InitializeComponents();
        SetupInputActions();
        CreateEyeCloseOverlay();
        InitializeState();
    }

    private void Start()
    {
        if (throwPowerBar != null)
            throwPowerBar.gameObject.SetActive(false);
    }

    private void OnEnable() => EnableAllInputs(true);
    private void OnDisable() => EnableAllInputs(false);

    private void Update()
    {
        float dt = Time.deltaTime;

        UpdateInput(dt);
        UpdateMovement(dt);

        if (!isTransitioning)
            UpdateCamera(dt);

        UpdateAnimations(dt);
        UpdateInteractions(dt);
        UpdateUI(dt);

        if (directionsNeedUpdate)
            UpdateCachedDirections();
    }

    private void OnDestroy()
    {
        if (eyeCloseOverlay != null)
            Destroy(eyeCloseOverlay.transform.parent.gameObject);
    }
    #endregion

    #region Initialization
    private void InitializeComponents()
    {
        characterController = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();

        if (playerCamera == null)
            playerCamera = Camera.main;

        cameraTransform = playerCamera.transform;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        cachedMouse = UnityEngine.InputSystem.Mouse.current;
    }

    private void InitializeState()
    {
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

        if (useWoWCameraStyle && !isFirstPerson)
        {
            freeLookRotation = new Vector2(transform.eulerAngles.y, 0f);
            SafeSetCursor(CursorLockMode.None, true);
        }
        else
        {
            SafeSetCursor(CursorLockMode.Locked, false);
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

        moveAction.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        moveAction.canceled += _ => moveInput = Vector2.zero;

        lookAction.performed += ctx => { if (!isTransitioning) lookInput = ctx.ReadValue<Vector2>(); };
        lookAction.canceled += _ => lookInput = Vector2.zero;

        runAction.performed += _ => isRunning = true;
        runAction.canceled += _ => isRunning = false;

        zoomAction.performed += ctx => HandleZoom(ctx.ReadValue<float>());
        jumpAction.performed += _ => TryJump();
        attackAction.performed += _ => TryAttack();
        interactAction.performed += _ => TryInteract();
        pickupAction.performed += _ => TryPickup();
        switchViewAction.performed += _ => { if (!isTransitioning) ToggleView(); };

        throwAction.started += _ => { if (isHoldingObject) StartChargingThrow(); };
        throwAction.canceled += _ => { if (isHoldingObject) ReleaseThrow(); };
    }

    private void EnableAllInputs(bool enable)
    {
        var list = new[] {
            moveAction, lookAction, jumpAction, runAction, attackAction,
            switchViewAction, interactAction, crouchAction, zoomAction, pickupAction, throwAction
        };

        foreach (var a in list)
        {
            if (a == null) continue;
            if (enable) a.Enable(); else a.Disable();
        }
    }
    #endregion

    #region Update Systems
    private void UpdateInput(float deltaTime)
    {
        smoothedLookInput = Vector2.SmoothDamp(
            smoothedLookInput, lookInput,
            ref lookInputVelocity, 1f / 15f);
    }

    private void UpdateMovement(float deltaTime)
    {
        bool wasGround = isGrounded;
        isGrounded = characterController.isGrounded;
        if (isGrounded && !wasGround) currentVelocity.y = -2f;

        UpdateCrouchState();
        UpdateCrouchTransition(deltaTime);
        UpdateJumpState(deltaTime);

        CalculateMovement(deltaTime);

        characterController.Move(currentVelocity * deltaTime);

        isMoving = moveInput.magnitude > idleThreshold;
        currentMovementSpeed = new Vector3(currentVelocity.x, 0, currentVelocity.z).magnitude;
    }
    #endregion

    #region Movement
    private void CalculateMovement(float deltaTime)
    {
        Vector3 moveDirection = Vector3.zero;
        float inputMagnitude = moveInput.magnitude;

        if (inputMagnitude > pivotThreshold)
        {
            if (isFirstPerson)
            {
                moveDirection = cachedCharacterRight * moveInput.x + cachedCharacterForward * moveInput.y;
            }
            else if (useWoWCameraStyle)
            {
                if (isMouseLookMode)
                    moveDirection = cachedCameraRight * moveInput.x + cachedCameraForward * moveInput.y;
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

        float targetSpeed = CalculateTargetSpeed(moveDirection);
        targetVelocity = moveDirection * targetSpeed;

        if (isGrounded)
        {
            Vector3 horizVel = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            Vector3 targetHorizontal = new Vector3(targetVelocity.x, 0f, targetVelocity.z);

            float accel = targetHorizontal.magnitude > horizVel.magnitude ? movementAcceleration : movementDeceleration;
            horizVel = Vector3.Lerp(horizVel, targetHorizontal, accel * deltaTime);

            currentVelocity.x = horizVel.x;
            currentVelocity.z = horizVel.z;

            if (currentVelocity.y < 0) currentVelocity.y = -2f;
        }
        else
        {
            currentVelocity.y += Physics.gravity.y * gravityMultiplier * deltaTime;
            if (inputMagnitude > 0.1f)
            {
                Vector3 airTarget = moveDirection * targetSpeed * airControl;
                const float airControlStrength = 2f;
                currentVelocity.x = Mathf.Lerp(currentVelocity.x, airTarget.x, airControlStrength * deltaTime);
                currentVelocity.z = Mathf.Lerp(currentVelocity.z, airTarget.z, airControlStrength * deltaTime);

                if (debugMovementValues && Time.frameCount % 10 == 0)
                    Debug.Log($"Air control - Target: {airTarget}, Current: {new Vector3(currentVelocity.x, 0, currentVelocity.z)}");
            }
        }
    }

    private float CalculateTargetSpeed(Vector3 moveDirection)
    {
        float baseSpeed = isCrouching ? crouchSpeed : (isRunning ? runSpeed : walkSpeed);

        if (!isFirstPerson && moveDirection.magnitude > 0.1f)
        {
            Vector3 nd = moveDirection.normalized;
            float forwardDot = Vector3.Dot(nd, cachedCharacterForward);
            float rightDot = Vector3.Dot(nd, cachedCharacterRight);

            if (forwardDot < -0.7f && Mathf.Abs(rightDot) < 0.3f)
                return baseSpeed * backwardSpeedMultiplier;
            if (Mathf.Abs(rightDot) > 0.7f && Mathf.Abs(forwardDot) < 0.3f)
                return baseSpeed * strafeSpeedMultiplier;
            if (forwardDot < -0.3f && Mathf.Abs(rightDot) > 0.3f)
                return baseSpeed * backwardStrafeSpeedMultiplier;
        }
        return baseSpeed;
    }

    private void HandleFreeLookRotation(float deltaTime)
{
    if (moveInput.magnitude <= 0.1f) return;

    // Calculate desired movement direction in character space
    Vector3 inputDirection = cachedCharacterRight * moveInput.x + cachedCharacterForward * moveInput.y;
    inputDirection.y = 0f;
    inputDirection = inputDirection.normalized;

    bool isMovingForward  = moveInput.y >  0.1f;   // W
    bool isMovingBackward = moveInput.y < -0.1f;   // S
    bool hasSideInput     = Mathf.Abs(moveInput.x) > 0.3f; // A or D

    // We only rotate the character when moving forward/backward AND there is side input (same as before)
    if ((isMovingForward || isMovingBackward) && hasSideInput)
    {
        float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg;

        if (isMovingBackward)
        {
            // Backing up: flip direction
            targetAngle += 180f;
            if (targetAngle > 180f) targetAngle -= 360f;
            if (targetAngle < -180f) targetAngle += 360f;
        }

        float currentAngle = transform.eulerAngles.y;
        float angleDifference = Mathf.DeltaAngle(currentAngle, targetAngle);

        if (Mathf.Abs(angleDifference) > rotationAngleThreshold)
        {
            // Original speeds:
            //  - forward used freeLookRotationSpeed
            //  - backward used 0.7f * freeLookRotationSpeed
            // Now we add a diagonal damp when forward + strafe to create a wider arc.

            bool isForwardDiagonal = isMovingForward && hasSideInput && !isMovingBackward;

            float baseSpeed =
                isMovingForward  ? freeLookRotationSpeed :
                isMovingBackward ? freeLookRotationSpeed * 0.7f :
                freeLookRotationSpeed;

            // Apply diagonal multiplier (usually < 1) to slow rotation (wider turning circle)
            float rotationSpeed = isForwardDiagonal
                ? baseSpeed * diagonalTurnSpeedMultiplier
                : baseSpeed;

            // Interpolate towards target angle
            float rotationStep = rotationSpeed * deltaTime;
            float newAngle = Mathf.LerpAngle(currentAngle, targetAngle, rotationStep);
            transform.rotation = Quaternion.Euler(0, newAngle, 0);

            directionsNeedUpdate = true;
        }
    }
}
    #endregion

    #region Camera
    private void UpdateCamera(float deltaTime)
    {
        UpdateMouseState();
        UpdateCameraMode();
        UpdateCameraRotation(deltaTime);
        UpdateCameraPosition(deltaTime);
        UpdateZoom(deltaTime);
        if (isFirstPerson) UpdateHeadBob(deltaTime);
    }

    private void UpdateMouseState()
    {
        if (cachedMouse == null) return;
        rightMousePressed = cachedMouse.rightButton.isPressed;
        leftMousePressed = cachedMouse.leftButton.isPressed;
    }

    private void UpdateCameraMode()
{
    // First-person enforcement
    if (isFirstPerson && forceLockedCursorInFirstPerson)
    {
        SafeSetCursor(CursorLockMode.Locked, false);
        wasLeftMouseCameraActive = false;
        wasRightMouseLookActive = false;
        pendingLeftOrbitActivation = false;
        return;
    }

    // Determine modes
    isMouseLookMode = useRightMouseButton && rightMousePressed;
    leftMouseCameraActive = enableLeftMouseCamera && leftMousePressed &&
                            (!isHoldingObject || isChargingThrow);

    bool startedLeftOrbit = leftMouseCameraActive && !wasLeftMouseCameraActive;
    bool endedLeftOrbit   = !leftMouseCameraActive && wasLeftMouseCameraActive;

    bool startedRightLook = isMouseLookMode && !wasRightMouseLookActive;
    bool endedRightLook   = !isMouseLookMode && wasRightMouseLookActive;

    // Store cursor positions (unchanged from your earlier logic)
    if (startedLeftOrbit && restoreCursorPositionAfterOrbit && cachedMouse != null)
        storedCursorPosition = cachedMouse.position.ReadValue();
    if (startedRightLook && restoreCursorPositionAfterOrbit && cachedMouse != null)
        storedRightCursorPosition = cachedMouse.position.ReadValue();

    // Defer left orbit cursor hide to next frame
    if (startedLeftOrbit)
    {
        pendingLeftOrbitActivation = true;
        StartCoroutine(ActivateLeftOrbitNextFrame());
    }

    if (isMouseLookMode)
    {
        // Right mouse look: lock for infinite delta
        SafeSetCursor(CursorLockMode.Locked, false);
        shouldFollowCamera = false;
        cameraFollowTimer = 0f;
    }
    else if (leftMouseCameraActive)
    {
        // If we are still waiting to hide the cursor (first frame of click), do nothing yet.
        if (!pendingLeftOrbitActivation)
        {
            // Already activated: ensure we are hidden but not re-calling needlessly.
            SafeSetCursor(CursorLockMode.None, false);
        }
        shouldFollowCamera = false;
        cameraFollowTimer = 0f;
    }
    else
    {
        // Exiting orbit modes
        if (endedLeftOrbit && restoreCursorPositionAfterOrbit && cachedMouse != null)
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            UnityEngine.InputSystem.Mouse.current.WarpCursorPosition(storedCursorPosition);
#endif
        }
        if (endedRightLook && restoreCursorPositionAfterOrbit && cachedMouse != null)
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            UnityEngine.InputSystem.Mouse.current.WarpCursorPosition(storedRightCursorPosition);
#endif
        }

        if (useWoWCameraStyle && !isFirstPerson)
            SafeSetCursor(CursorLockMode.None, true);
        else
            SafeSetCursor(CursorLockMode.None, true);
    }

    wasLeftMouseCameraActive = leftMouseCameraActive;
    wasRightMouseLookActive  = isMouseLookMode;
}

    private void UpdateCameraRotation(float deltaTime)
    {
        if (isFirstPerson)
        {
            transform.Rotate(Vector3.up * smoothedLookInput.x * mouseLookSensitivity);
            verticalRotation = ClampVertical(verticalRotation - smoothedLookInput.y * mouseLookSensitivity);
            cameraHolder.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
            directionsNeedUpdate = true;
        }
        else if (useWoWCameraStyle)
        {
            UpdateWoWCameraRotation(deltaTime);
        }
        else
        {
            horizontalRotation += smoothedLookInput.x * mouseLookSensitivity;
            verticalRotation -= smoothedLookInput.y * mouseLookSensitivity;
            verticalRotation = Mathf.Clamp(verticalRotation, minVerticalAngle, maxVerticalAngle);
        }
    }

    private void UpdateWoWCameraRotation(float deltaTime)
    {
        if (isMouseLookMode)
        {
            float sens = mouseLookSensitivity;
            transform.Rotate(Vector3.up * smoothedLookInput.x * sens);
            verticalRotation = Mathf.Clamp(verticalRotation - smoothedLookInput.y * sens, minVerticalAngle, maxVerticalAngle);

            horizontalRotation = transform.eulerAngles.y;
            freeLookRotation = new Vector2(horizontalRotation, verticalRotation);
            directionsNeedUpdate = true;
        }
        else if (leftMouseCameraActive)
        {
            float sens = leftMouseCameraSensitivity;
            freeLookRotation.x += smoothedLookInput.x * sens;
            freeLookRotation.y = Mathf.Clamp(freeLookRotation.y - smoothedLookInput.y * sens, minVerticalAngle, maxVerticalAngle);
            horizontalRotation = freeLookRotation.x;
            verticalRotation = freeLookRotation.y;
        }
        else
        {
            UpdateCameraFollowing(deltaTime);
        }

        if (isHoldingObject && !isChargingThrow)
        {
            float targetHor = transform.eulerAngles.y;
            horizontalRotation = Mathf.LerpAngle(horizontalRotation, targetHor, deltaTime * cameraFollowSpeed * 2f);
            freeLookRotation.x = horizontalRotation;

            verticalRotation = Mathf.Lerp(verticalRotation, 5f, deltaTime * 2f);
            freeLookRotation.y = verticalRotation;
        }
    }

    private void UpdateCameraFollowing(float deltaTime)
    {
        bool currentlyMoving = moveInput.magnitude > minMovementForFollow;
        float currentAngle = transform.eulerAngles.y;

        if (currentlyMoving && !wasMovingLastFrame)
        {
            movementTimer = 0f;
            if (debugCameraFollow) Debug.Log("Started moving - camera follow candidate");
        }
        else if (currentlyMoving)
        {
            movementTimer += deltaTime;
        }
        else if (!currentlyMoving && wasMovingLastFrame)
        {
            cameraFollowTimer = 0f;
            shouldFollowCamera = false;
            if (debugCameraFollow) Debug.Log("Stopped moving - camera follow halted");
        }

        if (alwaysFollowBehindPlayer &&
            enableCameraFollow &&
            !isMouseLookMode &&
            !leftMouseCameraActive &&
            !isHoldingObject)
        {
            if (currentlyMoving && movementTimer > movementFollowDelay)
            {
                shouldFollowCamera = true;
                currentFollowSpeed = behindPlayerFollowSpeed;

                if (debugCameraFollow && Time.frameCount % 60 == 0)
                    Debug.Log($"Follow behind - Character: {currentAngle:F1}°, Cam: {horizontalRotation:F1}°");
            }
            else if (!currentlyMoving)
            {
                shouldFollowCamera = false;
            }
        }
        else
        {
            float angleDiff = Mathf.Abs(Mathf.DeltaAngle(lastCharacterAngle, currentAngle));

            if (enableCameraFollow && !isMouseLookMode && !leftMouseCameraActive)
            {
                bool trigger = false;
                float followDelay = cameraFollowDelay;

                if (onlyFollowWhenMoving)
                {
                    if (currentlyMoving && movementTimer > followDelay)
                    {
                        float cameraCharacterDiff = Mathf.Abs(Mathf.DeltaAngle(horizontalRotation, currentAngle));
                        if (cameraCharacterDiff > cameraFollowThreshold)
                        {
                            cameraFollowTimer += deltaTime;
                            if (cameraFollowTimer > followDelay)
                            {
                                trigger = true;
                                if (debugCameraFollow)
                                    Debug.Log($"Angle follow triggered - diff: {cameraCharacterDiff:F1}°");
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
                    if (angleDiff > cameraFollowThreshold)
                    {
                        cameraFollowTimer += deltaTime;
                        if (cameraFollowTimer > followDelay) trigger = true;
                    }
                    else
                    {
                        cameraFollowTimer = 0f;
                        shouldFollowCamera = false;
                    }
                }

                if (trigger)
                {
                    shouldFollowCamera = true;
                    currentFollowSpeed = cameraFollowSpeed;
                }
            }
            else
            {
                shouldFollowCamera = false;
                cameraFollowTimer = 0f;
            }
        }

        if (shouldFollowCamera)
            UpdateAutomaticCameraFollow();

        wasMovingLastFrame = currentlyMoving;
        lastCharacterAngle = currentAngle;
    }

    private void UpdateAutomaticCameraFollow()
    {
        float targetHorizontalRotation = transform.eulerAngles.y;
        float followSpeed = currentFollowSpeed;

        if (alwaysFollowBehindPlayer && isMoving)
        {
            horizontalRotation = Mathf.LerpAngle(horizontalRotation, targetHorizontalRotation, Time.deltaTime * followSpeed);
            freeLookRotation.x = horizontalRotation;

            float diff = Mathf.Abs(Mathf.DeltaAngle(horizontalRotation, targetHorizontalRotation));
            if (debugCameraFollow && Time.frameCount % 30 == 0)
                Debug.Log($"Continuous follow - Current: {horizontalRotation:F1}°, Target: {targetHorizontalRotation:F1}°, Diff: {diff:F1}°");
        }
        else
        {
            horizontalRotation = Mathf.LerpAngle(horizontalRotation, targetHorizontalRotation, Time.deltaTime * followSpeed);
            verticalRotation = freeLookRotation.y;
            freeLookRotation.x = horizontalRotation;

            float diff = Mathf.Abs(Mathf.DeltaAngle(horizontalRotation, targetHorizontalRotation));
            if (diff < 2f)
            {
                shouldFollowCamera = false;
                cameraFollowTimer = 0f;
                if (debugCameraFollow)
                    Debug.Log("Camera follow finished (<2°)");
            }
        }
    }

    private void UpdateCameraPosition(float deltaTime)
    {
        if (isFirstPerson)
        {
            Vector3 targetPos = firstPersonPosition.position;
            targetPos.y += currentCameraYOffset;
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, targetPos, deltaTime * cameraTransitionSpeed);
        }
        else
        {
            Vector3 playerTarget = transform.position + thirdPersonOffset;
            Vector3 dir = Quaternion.Euler(verticalRotation, horizontalRotation, 0f) * -Vector3.forward;
            Vector3 desired = playerTarget + dir * currentCameraDistance;

            if (Physics.Raycast(playerTarget, dir, out RaycastHit hit, currentCameraDistance))
                desired = hit.point + dir * cameraCollisionOffset;

            cameraTransform.position = desired;
            cameraTransform.LookAt(playerTarget);
        }
    }

    private void UpdateZoom(float deltaTime)
    {
        if (isFirstPerson || isTransitioning) return;
        currentCameraDistance = Mathf.Lerp(currentCameraDistance, targetCameraDistance, deltaTime * zoomSpeed);
    }

    private void UpdateHeadBob(float deltaTime)
    {
        Vector3 basePos = originalCameraPosition;
        basePos.y += currentCameraYOffset;

        if (isGrounded && isMoving)
        {
            float freq = isCrouching ? crouchBobFrequency : bobFrequency;
            float amt = isCrouching ? crouchBobAmount : bobAmount;
            float speedMul = currentMovementSpeed / walkSpeed;

            bobTimer += deltaTime * freq * speedMul;
            Vector3 bobOffset = new Vector3(
                Mathf.Cos(bobTimer) * amt,
                Mathf.Sin(bobTimer * 2) * amt,
                0);

            cameraHolder.localPosition = basePos + bobOffset;
        }
        else
        {
            bobTimer = 0;
            cameraHolder.localPosition = Vector3.Lerp(cameraHolder.localPosition, basePos, deltaTime * 5f);
        }
    }
    #endregion

    #region Animation
    private void UpdateAnimations(float deltaTime)
    {
        if (animator == null) return;

        float inputMag = moveInput.magnitude;
        bool movingForAnim = inputMag > idleThreshold;

        float animH = 0f;
        float animV = 0f;
        float animSpeed = 0f;

        if (movingForAnim)
        {
            if (isFirstPerson)
            {
                animH = moveInput.x;
                animV = moveInput.y;
            }
            else
            {
                if (useWoWCameraStyle && !isMouseLookMode)
                {
                    animH = moveInput.x;
                    animV = moveInput.y;
                }
                else
                {
                    Vector3 inputDir = (cachedCameraRight * moveInput.x + cachedCameraForward * moveInput.y).normalized;
                    animV = Vector3.Dot(inputDir, cachedCharacterForward);
                    animH = Vector3.Dot(inputDir, cachedCharacterRight);
                }
            }

            float baseSpeedMultiplier = isCrouching ? crouchSpeedThreshold :
                (isRunning ? runSpeedThreshold : walkSpeedThreshold);

            bool pureStrafe = Mathf.Abs(animV) < 0.1f && Mathf.Abs(animH) > 0.7f;
            bool pureBackward = animV < -0.7f && Mathf.Abs(animH) < 0.1f;
            bool backwardStrafe = animV < -0.3f && Mathf.Abs(animH) > 0.3f;

            isStrafing = pureStrafe;
            isBackwardStrafing = pureBackward;

            if (pureStrafe)
                animSpeed = baseSpeedMultiplier * strafeSpeedMultiplier;
            else if (pureBackward)
                animSpeed = baseSpeedMultiplier * backwardSpeedMultiplier;
            else if (backwardStrafe)
                animSpeed = baseSpeedMultiplier * backwardStrafeSpeedMultiplier;
            else
                animSpeed = baseSpeedMultiplier * inputMag;
        }
        else
        {
            isStrafing = false;
            isBackwardStrafing = false;
        }

        smoothedHorizontal = Mathf.SmoothDamp(smoothedHorizontal, animH, ref horizontalVelocity, animationSmoothTime);
        smoothedVertical = Mathf.SmoothDamp(smoothedVertical, animV, ref verticalVelocityAnim, animationSmoothTime);
        smoothedSpeed = Mathf.SmoothDamp(smoothedSpeed, animSpeed, ref speedVelocity, animationSmoothTime);

        smoothedHorizontal = Mathf.Clamp(smoothedHorizontal, -1f, 1f);
        smoothedVertical = Mathf.Clamp(smoothedVertical, -1f, 1f);
        smoothedSpeed = Mathf.Clamp(smoothedSpeed, 0f, 1f);

        animator.SetFloat(AnimParamSpeed, smoothedSpeed);
        animator.SetFloat(AnimParamHorizontal, smoothedHorizontal);
        animator.SetFloat(AnimParamVertical, smoothedVertical);
        animator.SetBool(AnimParamIsGrounded, isGrounded);
        animator.SetBool(AnimParamIsRunning, isRunning && movingForAnim && !isCrouching);
        animator.SetBool(AnimParamIsCrouching, isCrouching);
        animator.SetBool(AnimParamIsStrafing, isStrafing);
        animator.SetBool(AnimParamIsBackwardStrafing, isBackwardStrafing);
    }
    #endregion

    #region Crouch
    private void UpdateCrouchState()
    {
        bool crouchInputValue = crouchAction.ReadValue<float>() > 0.5f;
        if (crouchInputValue && !isCrouching)
            StartCrouch();
        else if (!crouchInputValue && isCrouching)
        {
            if (CanStandUp()) StopCrouch();
        }
    }

    private void UpdateCrouchTransition(float deltaTime)
    {
        characterController.height = Mathf.Lerp(characterController.height, targetHeight, deltaTime * crouchTransitionSpeed);

        Vector3 center = characterController.center;
        center.y = Mathf.Lerp(center.y, targetControllerCenterY, deltaTime * crouchTransitionSpeed);
        characterController.center = center;

        if (isFirstPerson)
        {
            currentCameraYOffset = Mathf.Lerp(currentCameraYOffset, targetCameraYOffset, deltaTime * crouchTransitionSpeed);
        }
    }

    private void StartCrouch()
    {
        isCrouching = true;
        targetHeight = crouchHeight;
        targetControllerCenterY = originalControllerCenterY - ((standingHeight - crouchHeight) * 0.5f);
        if (isFirstPerson)
            targetCameraYOffset = -(standingHeight - crouchHeight) * 0.5f;
    }

    private void StopCrouch()
    {
        isCrouching = false;
        targetHeight = standingHeight;
        targetControllerCenterY = originalControllerCenterY;
        if (isFirstPerson)
            targetCameraYOffset = 0f;
    }

    private bool CanStandUp()
    {
        if (!isCrouching) return true;
        Vector3 bottom = transform.position + characterController.center - Vector3.up * (characterController.height * 0.5f);
        Vector3 top = bottom + Vector3.up * standingHeight;
        float radius = characterController.radius * 0.8f;
        return !Physics.CheckCapsule(bottom + Vector3.up * radius, top - Vector3.up * radius, radius);
    }
    #endregion

    #region Jump
    private void UpdateJumpState(float deltaTime)
    {
        if (!isJumpQueued) return;

        jumpTimer += deltaTime;
        if (jumpTimer >= jumpAnimationDelay)
        {
            currentVelocity.y = jumpForce;

            if (maintainJumpMomentum && jumpMomentumDirection.magnitude > 0.1f)
            {
                float finalSpeed = jumpMomentumSpeed * jumpMomentumMultiplier;
                Vector3 horizMomentum = jumpMomentumDirection * finalSpeed;
                currentVelocity.x = horizMomentum.x;
                currentVelocity.z = horizMomentum.z;

                if (debugMovementValues)
                    Debug.Log($"Jump executed - Momentum: {horizMomentum}, Final Vel: {currentVelocity}");
            }

            isJumpQueued = false;
            jumpTimer = 0f;
            jumpMomentumDirection = Vector3.zero;
            jumpMomentumSpeed = 0f;
        }
    }

    private void TryJump()
    {
        if (isCrouching)
        {
            if (CanStandUp()) StopCrouch();
            return;
        }

        if (!(isGrounded && !isJumpQueued)) return;

        if (maintainJumpMomentum && moveInput.magnitude > 0.1f)
        {
            if (isFirstPerson)
                jumpMomentumDirection = (cachedCharacterRight * moveInput.x + cachedCharacterForward * moveInput.y);
            else if (useWoWCameraStyle)
                jumpMomentumDirection = isMouseLookMode
                    ? (cachedCameraRight * moveInput.x + cachedCameraForward * moveInput.y)
                    : (cachedCharacterRight * moveInput.x + cachedCharacterForward * moveInput.y);
            else
                jumpMomentumDirection = (cachedCameraRight * moveInput.x + cachedCameraForward * moveInput.y);

            jumpMomentumDirection.y = 0f;
            jumpMomentumDirection = jumpMomentumDirection.normalized;

            jumpMomentumSpeed = new Vector3(currentVelocity.x, 0, currentVelocity.z).magnitude;
            if (jumpMomentumSpeed < jumpForwardForce) jumpMomentumSpeed = jumpForwardForce;

            if (debugMovementValues)
                Debug.Log($"Jump momentum stored - Dir: {jumpMomentumDirection}, Speed: {jumpMomentumSpeed:F2}");
        }
        else
        {
            jumpMomentumDirection = Vector3.zero;
            jumpMomentumSpeed = 0f;
        }

        isJumpQueued = true;
        jumpTimer = 0f;

        if (animator != null)
            animator.SetTrigger(AnimParamJump);
    }
    #endregion

    #region Interaction / Combat / Pickup
    private void UpdateInteractions(float deltaTime)
    {
        if (currentAttackCooldown > 0) currentAttackCooldown -= deltaTime;
        UpdateHeldObject();
        if (isChargingThrow) throwChargeTimer += deltaTime;
    }

    private void TryAttack()
    {
        if (currentAttackCooldown > 0) return;

        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, attackRange))
        {
            if (hit.collider.TryGetComponent(out IDamageable dmg))
                dmg.TakeDamage(attackDamage);
        }

        if (animator != null)
            animator.SetTrigger(UnityEngine.Random.value > 0.5f ? AnimParamPunch : AnimParamKick);

        currentAttackCooldown = attackCooldown;
    }

    private void TryInteract()
    {
        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, interactionRange, interactionMask))
        {
            if (hit.collider.TryGetComponent(out IInteractable interactable))
                interactable.Interact();
        }

        if (animator != null)
            animator.SetTrigger(AnimParamInteract);
    }

    private void TryPickup()
    {
        if (isHoldingObject)
        {
            DropObject();
            return;
        }

        GameObject target = FindPickupTarget();
        if (target != null)
            PickupObject(target);
    }

    private GameObject FindPickupTarget()
    {
        if (isFirstPerson)
        {
            if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, pickupRange, pickupMask))
            {
                if (hit.collider.TryGetComponent(out SimplePickup pickup) && pickup.CanBePickedUp())
                    return pickup.gameObject;
            }
        }
        else
        {
            Vector3 center = transform.position + Vector3.up * 1.0f;
            Collider[] hits = Physics.OverlapSphere(center, pickupRange, pickupMask);

            float closest = float.MaxValue;
            GameObject selected = null;
            foreach (var c in hits)
            {
                if (c.TryGetComponent(out SimplePickup pickup) && pickup.CanBePickedUp())
                {
                    float dist = Vector3.Distance(center, c.transform.position);
                    if (dist < closest)
                    {
                        closest = dist;
                        selected = pickup.gameObject;
                    }
                }
            }
            return selected;
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
            animator.SetTrigger(AnimParamPickup);
    }

    private void DropObject()
    {
        if (heldObject == null) return;

        heldObject.transform.SetParent(null);
        if (heldObjectRb != null)
        {
            heldObjectRb.useGravity = true;
            Vector3 dir = cameraTransform.forward + Vector3.up * 0.2f;
            heldObjectRb.linearVelocity = dir * 2f;
        }

        heldObject = null;
        heldObjectRb = null;
        isHoldingObject = false;

        if (animator != null)
            animator.SetTrigger(AnimParamDrop);
    }

    private void UpdateHeldObject()
    {
        if (!isHoldingObject || heldObject == null) return;

        Vector3 targetPos;
        Quaternion targetRot;

        if (isFirstPerson)
        {
            targetPos = cameraTransform.position + cameraTransform.forward * holdDistance;
            targetRot = cameraTransform.rotation;
        }
        else
        {
            targetPos = transform.position + Vector3.up * 1.5f + cameraTransform.forward * holdDistance;
            targetRot = cameraTransform.rotation;
        }

        heldObject.transform.position = Vector3.Lerp(heldObject.transform.position, targetPos, Time.deltaTime * holdPositionSpeed);
        heldObject.transform.rotation = Quaternion.Lerp(heldObject.transform.rotation, targetRot, Time.deltaTime * holdPositionSpeed);

        if (heldObjectRb != null)
        {
            heldObjectRb.linearVelocity = Vector3.zero;
            heldObjectRb.angularVelocity = Vector3.zero;
        }
    }

    private void StartChargingThrow()
    {
        if (!isHoldingObject || isChargingThrow) return;
        isChargingThrow = true;
        throwChargeTimer = 0f;
    }

    private void ReleaseThrow()
    {
        if (!isChargingThrow || !isHoldingObject) return;

        float percent = Mathf.Clamp01(throwChargeTimer / maxHoldTime);
        float force = Mathf.Lerp(minThrowForce, maxThrowForce, percent);
        ThrowObject(force);

        isChargingThrow = false;
        throwChargeTimer = 0f;
    }

    private void ThrowObject(float force)
    {
        if (heldObject == null) return;

        heldObject.transform.SetParent(null);
        if (heldObjectRb != null)
        {
            heldObjectRb.useGravity = true;
            heldObjectRb.isKinematic = false;
            Vector3 dir = cameraTransform.forward;
            heldObjectRb.AddForce(dir * force);
            heldObjectRb.AddForce(Vector3.up * 0.2f * force);
        }

        heldObject = null;
        heldObjectRb = null;
        isHoldingObject = false;

        if (animator != null)
            animator.SetTrigger(AnimParamThrow);
    }
    #endregion

    #region UI
    private void UpdateUI(float deltaTime)
    {
        if (throwPowerBar == null) return;

        if (isChargingThrow)
        {
            float percent = Mathf.Clamp01(throwChargeTimer / maxHoldTime);
            throwPowerBar.fillAmount = percent;
            if (!throwPowerBar.gameObject.activeInHierarchy)
                throwPowerBar.gameObject.SetActive(true);
        }
        else if (throwPowerBar.gameObject.activeInHierarchy)
        {
            throwPowerBar.gameObject.SetActive(false);
        }
    }
    #endregion

    #region Direction Cache
    private void UpdateCachedDirections()
    {
        cachedCharacterForward = transform.forward;
        cachedCharacterRight = transform.right;
        cachedCameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        cachedCameraRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        directionsNeedUpdate = false;
    }
    #endregion

    #region Zoom / View / Transitions
    private void HandleZoom(float scrollValue)
    {
        if (isFirstPerson || isTransitioning || Mathf.Abs(scrollValue) <= 0.1f) return;
        float adj = invertScrollDirection ? -scrollValue : scrollValue;
        targetCameraDistance = Mathf.Clamp(targetCameraDistance - adj * zoomSensitivity, minCameraDistance, maxCameraDistance);
    }

    private void ToggleView()
    {
        if (isFirstPerson) StartCoroutine(EyeCloseTransition());
        else StartCoroutine(SmoothTransition());
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
        eyeCloseImage.color = new Color(eyeCloseColor.r, eyeCloseColor.g, eyeCloseColor.b, 0f);

        var rect = eyeCloseOverlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        DontDestroyOnLoad(canvasGO);
    }

    private IEnumerator EyeCloseTransition()
{
    isTransitioning = true;
    float fadeInDuration = eyeCloseTransitionDuration * 0.3f;
    float elapsedTime = 0f;

    while (elapsedTime < fadeInDuration)
    {
        elapsedTime += Time.deltaTime;
        float alpha = elapsedTime / fadeInDuration;
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

    Vector3 desiredCameraPos = playerTargetPosition + directionToCamera * currentCameraDistance;
    cameraTransform.position = desiredCameraPos;
    cameraTransform.LookAt(playerTargetPosition);

    isFirstPerson = false;
    cameraHolder.localPosition = originalCameraPosition;
    cameraHolder.localRotation = Quaternion.identity;
    directionsNeedUpdate = true;

    yield return new WaitForSeconds(eyeCloseTransitionDuration * 0.4f);

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

    // NEW: show cursor again in 3rd person (WoW style) after transition
    if (useWoWCameraStyle)
        SafeSetCursor(CursorLockMode.None, true);
    else
        SafeSetCursor(CursorLockMode.None, true);

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

            Vector3 straightPath = Vector3.Lerp(startPosition, targetPosition, smoothProgress);
            float arcOffset = Mathf.Sin(smoothProgress * Mathf.PI) * 0.3f;
            Vector3 currentPos = straightPath + Vector3.up * arcOffset;

            cameraTransform.position = currentPos;
            cameraTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, smoothProgress);

            yield return null;
        }

        isFirstPerson = true;
        transform.rotation = Quaternion.Euler(0f, horizontalRotation, 0f);
        verticalRotation = 0f;
        cameraHolder.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
        currentCameraYOffset = targetCameraYOffset;

        cameraTransform.position = firstPersonPosition.position;
        cameraTransform.rotation = transform.rotation;
        directionsNeedUpdate = true;

        // NEW: enforce hidden locked cursor in first-person
        if (forceLockedCursorInFirstPerson)
            SafeSetCursor(CursorLockMode.Locked, false);

        isTransitioning = false;
    }
    #endregion

    #region Debug / Gizmos
    private void OnGUI()
    {
        if (!(debugMovementValues || debugCameraFollow)) return;

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
            float diff = Mathf.Abs(Mathf.DeltaAngle(horizontalRotation, transform.eulerAngles.y));
            GUILayout.Label($"Angle Difference: {diff:F1}°");
            GUILayout.Label($"Should Follow Camera: {shouldFollowCamera}");
            GUILayout.Label($"Always Follow Behind: {alwaysFollowBehindPlayer}");
            GUILayout.Label($"Behind Follow Speed: {behindPlayerFollowSpeed}");
            GUILayout.Label($"Movement Timer: {movementTimer:F2}");
            GUILayout.Label($"Current Follow Speed: {currentFollowSpeed:F1}");
        }

        GUILayout.EndArea();
    }

    private void OnDrawGizmosSelected()
    {
        if (playerCamera == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(cameraTransform.position, interactionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(cameraTransform.position, attackRange);

        Gizmos.color = Color.green;
        if (isFirstPerson)
        {
            Vector3 start = cameraTransform.position;
            Vector3 end = start + cameraTransform.forward * pickupRange;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawWireSphere(end, 0.1f);
        }
        else
        {
            Vector3 center = transform.position + Vector3.up * 1.0f;
            Gizmos.DrawWireSphere(center, pickupRange);
        }
    }
    #endregion

    #region Helper Methods
    private float ClampVertical(float value) => Mathf.Clamp(value, -80f, 80f);

    private void SafeSetCursor(CursorLockMode mode, bool visible)
    {
        if (Cursor.lockState != mode)
            Cursor.lockState = mode;
        if (Cursor.visible != visible)
            Cursor.visible = visible;
    }
    
    private IEnumerator ActivateLeftOrbitNextFrame()
    {
        // Defer state change to avoid doing OS cursor hide on the same frame as the mouse down event.
        yield return null; // wait 1 frame
        if (leftMouseCameraActive) // still valid
        {
            SafeSetCursor(CursorLockMode.None, false); // hide but not lock
            leftOrbitEnterFrame = Time.frameCount;
        }
        pendingLeftOrbitActivation = false;
    }
    #endregion
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