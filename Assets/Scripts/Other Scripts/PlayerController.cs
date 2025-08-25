using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    #region Inspector - Camera (Third-Person)
    [Header("Third Person Camera")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float distance = 6f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 14f;
    [SerializeField] private float zoomSensitivity = 2.5f;
    [SerializeField] private float orbitSensitivityX = 180f;
    [SerializeField] private float orbitSensitivityY = 120f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 70f;
    [SerializeField] private float followYawLag = 6f;
    [SerializeField] private float cameraCollisionRadius = 0.25f;
    [SerializeField] private LayerMask cameraCollisionMask = ~0;
    [SerializeField] private float cameraCollisionRecoverSpeed = 6f;
    [SerializeField] private float cameraHeightOffset = 1.6f;
    #endregion

    #region Inspector - First Person
    [Header("First Person")]
    [SerializeField] private bool startInFirstPerson = false;
    [SerializeField] private Transform firstPersonAnchor;
    [SerializeField] private float fpLookSensitivityX = 180f;
    [SerializeField] private float fpLookSensitivityY = 120f;
    [SerializeField] private float fpMinPitch = -80f;
    [SerializeField] private float fpMaxPitch = 80f;
    [SerializeField] private bool lockCursorInFP = true;
    [SerializeField] private bool hideCursorInFP = true;
    #endregion

    #region Inspector - Head Bob (FP)
    [Header("Head Bob (FP)")]
    [SerializeField] private bool enableHeadBob = true;
    [SerializeField] private float bobFreqWalk = 2.2f;
    [SerializeField] private float bobFreqRun = 3.4f;
    [SerializeField] private float bobAmpHorizontal = 0.025f;
    [SerializeField] private float bobAmpVertical = 0.035f;
    [SerializeField] private float bobReturnSpeed = 6f;
    #endregion

    #region Inspector - Eye Close Transition
    [Header("Eye Close Transition")]
    [SerializeField] private bool useEyeCloseTransition = true;
    [SerializeField] private float eyeCloseTotalDuration = 0.6f;
    [SerializeField] private Color eyeCloseColor = Color.black;
    [SerializeField, Range(0.05f, 0.95f)] private float eyeCloseFadeInPortion = 0.35f;
    #endregion

    #region Inspector - Movement
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4.2f;
    [SerializeField] private float runSpeed = 7.5f;
    [SerializeField] private float acceleration = 14f;
    [SerializeField] private float deceleration = 18f;
    [SerializeField] private float airControlPercent = 0.45f;
    [SerializeField] private float gravity = -28f;
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float extraFallGravityMultiplier = 1.3f;
    [SerializeField] private bool allowRotateToMovementWhenNotMouselooking = true;
    [SerializeField] private float rotateToMovementSpeed = 540f;
    [SerializeField] private bool forceAAndDAsStrafe = true;
    [SerializeField] private float characterMouseLookYawSpeed = 720f;
    #endregion

    #region Inspector - Turning Style (NEW WoW tuning)
    [Header("Turning Style (WoW-like)")]
    [Tooltip("Only auto-rotate when there is forward input (W). Pure strafe or backward no rotation.")]
    [SerializeField] private bool rotateOnlyWhenMovingForward = true;
    [Tooltip("Allow rotation if moving purely backward (S). Usually false for WoW feel.")]
    [SerializeField] private bool allowBackwardTurn = false;
    [Tooltip("Allow rotation on pure strafe (A or D only). Usually false; enabling recreates the spinning you saw.")]
    [SerializeField] private bool allowPureStrafeTurn = false;
    [Tooltip("Multiplier (<1 slows) applied to rotation when moving forward+strafe (W+A / W+D) to widen turning arc.")]
    [SerializeField, Range(0.05f, 1f)] private float forwardDiagonalTurnSpeedMultiplier = 0.4f;
    [Tooltip("Extra slow-down when forward AND strafe input present; set 0 for none.")]
    [SerializeField] private float diagonalExtraDampDegrees = 0f;
    [Tooltip("Minimum forward input before considering it 'forward'. Helps ignore analog noise.")]
    [SerializeField] private float forwardThreshold = 0.2f;
    [Tooltip("Minimum strafe input threshold.")]
    [SerializeField] private float strafeThreshold = 0.2f;
    #endregion

    #region Inspector - Animation
    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private float speedBlendAcceleration = 8f;
    [SerializeField] private float turnDirectionSensitivity = 3f;
    [Tooltip("Speed threshold to determine if player is strafing vs turning")]
    [SerializeField] private float strafeDetectionSpeed = 0.3f;
    
    // Animator parameter names
    [Header("Animator Parameters")]
    [SerializeField] private string animParamSpeed = "Speed";
    [SerializeField] private string animParamHorizontal = "Horizontal";
    [SerializeField] private string animParamVertical = "Vertical";
    [SerializeField] private string animParamTurnDirection = "TurnDirection";
    [SerializeField] private string animParamIsGrounded = "IsGrounded";
    [SerializeField] private string animParamIsRunning = "IsRunning";
    [SerializeField] private string animParamIsStrafing = "IsStrafing";
    [SerializeField] private string animParamIsBackwardStrafing = "IsBackwardStrafing";
    [SerializeField] private string animParamJump = "Jump";
    [SerializeField] private string animParamPickup = "Pickup";
    [SerializeField] private string animParamDrop = "Drop";
    [SerializeField] private string animParamThrow = "Throw";
    [SerializeField] private string animParamInteract = "Interact";
    #endregion

    #region Inspector - View Toggle
    [Header("View Toggle")]
    [SerializeField] private string switchViewActionName = "SwitchView";
    [SerializeField] private float tpToFpTransitionDuration = 0.8f;
    [SerializeField] private float tpToFpVerticalArc = 0.3f;
    #endregion

    #region Inspector - Pickup / Throw
    [Header("Pickup / Throw")]
    [SerializeField] private string pickupActionName = "Pickup";
    [SerializeField] private string throwActionName = "Throw";
    [SerializeField] private string interactActionName = "Interact";
    [SerializeField] private float pickupRange = 3f;
    [SerializeField] private LayerMask pickupMask = ~0;
    [SerializeField] private float holdDistance = 1.6f;
    [SerializeField] private float holdHeightOffset = 0.0f;
    [SerializeField] private float holdLerpSpeed = 14f;
    [SerializeField] private bool rotateHeldObjectToCamera = true;
    [SerializeField] private float throwMinForce = 250f;
    [SerializeField] private float throwMaxForce = 1200f;
    [SerializeField] private float throwChargeTime = 1.2f;
    [SerializeField] private AnimationCurve throwChargeCurve = AnimationCurve.EaseInOut(0,0,1,1);
    [SerializeField] private UnityEngine.UI.Image throwPowerBar;
    [SerializeField] private bool showThrowBarWhileCharging = true;
    [SerializeField] private float forwardThrowUpwardFactor = 0.15f;
    #endregion

    #region Runtime - Components & Input
    private CharacterController controller;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction runAction;
    private InputAction switchViewAction;
    private InputAction pickupAction;
    private InputAction throwAction;
    private InputAction interactAction;
    private Vector2 moveInput;
    private Vector2 lookDelta;
    #endregion

    #region Runtime - Movement State
    private Vector3 velocity;
    private bool isGrounded;
    private bool wasGrounded;
    private bool jumpQueued;
    private bool isRunning;
    private float animSpeedValue;
    #endregion

    #region Runtime - Camera State
    private float cameraYaw;
    private float cameraPitch;
    private float targetDistance;
    private float currentDistance;
    private bool rightMouseHeld;
    private bool leftMouseHeld;
    private bool isFirstPerson;
    private bool isTransitioningView;
    private float movementReferenceYaw;
    #endregion

    #region Runtime - Head Bob
    private float bobTimer;
    private Vector3 bobOffset;
    #endregion

    #region Runtime - Eye Close UI
    private Canvas eyeCanvas;
    private UnityEngine.UI.Image eyeImage;
    #endregion

    #region Runtime - Pickup / Throw State
    private GameObject heldObject;
    private Rigidbody heldRb;
    private bool isHoldingObject;
    private bool isChargingThrow;
    private float throwChargeTimer;
    #endregion

    #region Runtime - Animation Values
    private float animHorizontal;
    private float animVertical;
    private float animTurnDirection;
    private bool animIsStrafing;
    private bool animIsBackwardStrafing;
    private float previousYaw;
    #endregion

    #region Constants
    private const float EPS = 0.0001f;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();
        if (!playerCamera) playerCamera = Camera.main;
        if (!cameraPivot) cameraPivot = transform;
        if (!animator) animator = GetComponentInChildren<Animator>();

        var actions = playerInput.actions;
        moveAction = actions["Move"];
        lookAction = actions["Look"];
        jumpAction = actions["Jump"];
        runAction = actions["Run"];
        if (!string.IsNullOrEmpty(switchViewActionName) && actions.FindAction(switchViewActionName) != null)
            switchViewAction = actions[switchViewActionName];
        if (!string.IsNullOrEmpty(pickupActionName) && actions.FindAction(pickupActionName) != null)
            pickupAction = actions[pickupActionName];
        if (!string.IsNullOrEmpty(throwActionName) && actions.FindAction(throwActionName) != null)
            throwAction = actions[throwActionName];
        if (!string.IsNullOrEmpty(interactActionName) && actions.FindAction(interactActionName) != null)
            interactAction = actions[interactActionName];

        targetDistance = distance;
        currentDistance = distance;

        Vector3 forwardFlat = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
        cameraYaw = (forwardFlat.sqrMagnitude > 0.1f)
            ? Mathf.Atan2(forwardFlat.x, forwardFlat.z) * Mathf.Rad2Deg
            : transform.eulerAngles.y;

        Vector3 localForward = Quaternion.Euler(0, -cameraYaw, 0) * playerCamera.transform.forward;
        cameraPitch = Mathf.Asin(localForward.y) * Mathf.Rad2Deg;
        cameraPitch = Mathf.Clamp(cameraPitch, minPitch, maxPitch);

        movementReferenceYaw = transform.eulerAngles.y;
        previousYaw = transform.eulerAngles.y;

        isFirstPerson = startInFirstPerson;
        if (isFirstPerson)
        {
            cameraPitch = Mathf.Clamp(cameraPitch, fpMinPitch, fpMaxPitch);
            ApplyCursorStateFP();
        }
        else
        {
            ApplyCursorStateTP();
        }

        if (useEyeCloseTransition) CreateEyeCloseOverlay();
        if (throwPowerBar) throwPowerBar.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        lookAction?.Enable();
        jumpAction?.Enable();
        runAction?.Enable();
        switchViewAction?.Enable();
        pickupAction?.Enable();
        throwAction?.Enable();
        interactAction?.Enable();

        if (switchViewAction != null) switchViewAction.performed += OnSwitchView;
        if (pickupAction != null) pickupAction.performed += OnPickupPressed;
        if (throwAction != null)
        {
            throwAction.started += OnThrowStarted;
            throwAction.canceled += OnThrowCanceled;
        }
        if (interactAction != null) interactAction.performed += OnInteractPressed;
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        lookAction?.Disable();
        jumpAction?.Disable();
        runAction?.Disable();
        switchViewAction?.Disable();
        pickupAction?.Disable();
        throwAction?.Disable();
        interactAction?.Disable();

        if (switchViewAction != null) switchViewAction.performed -= OnSwitchView;
        if (pickupAction != null) pickupAction.performed -= OnPickupPressed;
        if (throwAction != null)
        {
            throwAction.started -= OnThrowStarted;
            throwAction.canceled -= OnThrowCanceled;
        }
        if (interactAction != null) interactAction.performed -= OnInteractPressed;
    }

    private void Update()
    {
        if (isTransitioningView)
        {
            ReadMovementInput();
            UpdateMovement(Time.deltaTime);
            UpdatePickupThrow(Time.deltaTime);
            return;
        }

        ReadInput();
        HandleJumpQueue();
        UpdateMovement(Time.deltaTime);
        UpdateCamera(Time.deltaTime);
        UpdateAnimation(Time.deltaTime);
        UpdatePickupThrow(Time.deltaTime);
    }
    #endregion

    #region Input
    private void ReadInput()
    {
        ReadMovementInput();
        ReadMouseButtons();
        ReadLookInput();
        ReadRunJump();
        UpdateCursorVisibility();
        UpdateMovementReference();
    }

    private void ReadMovementInput() => moveInput = moveAction.ReadValue<Vector2>();

    private void ReadMouseButtons()
    {
        if (Mouse.current != null)
        {
            leftMouseHeld = Mouse.current.leftButton.isPressed;
            rightMouseHeld = Mouse.current.rightButton.isPressed;
        }
    }

    private void ReadLookInput() => lookDelta = lookAction.ReadValue<Vector2>();

    private void ReadRunJump()
    {
        isRunning = runAction.ReadValue<float>() > 0.5f;
        if (jumpAction.triggered && !jumpQueued && isGrounded)
            jumpQueued = true;
    }

    private void UpdateMovementReference()
    {
        if (isFirstPerson)
        {
            movementReferenceYaw = cameraYaw;
        }
        else
        {
            if (rightMouseHeld)
            {
                movementReferenceYaw = cameraYaw;
            }
            else
            {
                movementReferenceYaw = transform.eulerAngles.y;
            }
        }
    }

    private void UpdateCursorVisibility()
    {
        if (isFirstPerson)
        {
            return;
        }

        bool shouldHideCursor = leftMouseHeld || rightMouseHeld;
        Cursor.visible = !shouldHideCursor;
        Cursor.lockState = CursorLockMode.None;
    }

    private void OnSwitchView(InputAction.CallbackContext ctx)
    {
        if (!isTransitioningView) ToggleView();
    }

    private void OnInteractPressed(InputAction.CallbackContext ctx)
    {
        TriggerAnimatorAction(animParamInteract);
        // Add your interaction logic here
    }
    #endregion

    #region Movement
    private void HandleJumpQueue() { }

    private void UpdateMovement(float dt)
    {
        wasGrounded = isGrounded;
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0f) velocity.y = -2f;

        Vector3 refForward = Quaternion.Euler(0, movementReferenceYaw, 0) * Vector3.forward;
        Vector3 refRight = Quaternion.Euler(0, movementReferenceYaw, 0) * Vector3.right;

        Vector3 inputDir = (refForward * moveInput.y + refRight * moveInput.x);
        if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();

        float targetSpeed = (isRunning ? runSpeed : walkSpeed) * inputDir.magnitude;

        Vector3 horizVel = new Vector3(velocity.x, 0f, velocity.z);
        float currentSpeed = horizVel.magnitude;
        float accel = targetSpeed > currentSpeed ? acceleration : deceleration;
        if (!isGrounded) accel *= airControlPercent;

        Vector3 targetHoriz = inputDir * targetSpeed;
        horizVel = targetSpeed < EPS
            ? Vector3.MoveTowards(horizVel, Vector3.zero, accel * dt)
            : Vector3.MoveTowards(horizVel, targetHoriz, accel * dt);

        velocity.x = horizVel.x;
        velocity.z = horizVel.z;

        if (jumpQueued)
        {
            velocity.y = jumpForce;
            jumpQueued = false;
            TriggerAnimatorAction(animParamJump);
        }

        float g = gravity;
        if (velocity.y < 0f) g *= extraFallGravityMultiplier;
        velocity.y += g * dt;

        controller.Move(velocity * dt);
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0f) velocity.y = -2f;

        HandleCharacterRotation(inputDir, dt);
    }

    private void HandleCharacterRotation(Vector3 desiredDir, float dt)
    {
        if (isFirstPerson)
        {
            transform.rotation = Quaternion.Euler(0f, cameraYaw, 0f);
            return;
        }

        if (rightMouseHeld)
        {
            float newYaw = Mathf.MoveTowardsAngle(
                transform.eulerAngles.y,
                cameraYaw,
                characterMouseLookYawSpeed * dt
            );
            transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
            return;
        }

        if (!allowRotateToMovementWhenNotMouselooking) return;

        float forward = moveInput.y;
        float strafe = moveInput.x;

        bool hasForward = forward > forwardThreshold;
        bool hasBackward = forward < -forwardThreshold;
        bool hasStrafe = Mathf.Abs(strafe) > strafeThreshold;

        bool shouldRotate = true;

        if (rotateOnlyWhenMovingForward)
        {
            if (!hasForward)
            {
                if (hasBackward && allowBackwardTurn) shouldRotate = true;
                else if (hasStrafe && allowPureStrafeTurn) shouldRotate = true;
                else shouldRotate = false;
            }
        }
        else
        {
            if (hasBackward && !allowBackwardTurn) shouldRotate = false;
            if (hasStrafe && !hasForward && !hasBackward && !allowPureStrafeTurn) shouldRotate = false;
        }

        if (!shouldRotate || desiredDir.sqrMagnitude < 0.0001f)
            return;

        Vector3 flat = desiredDir;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) return;
        flat.Normalize();

        float targetYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        float currentYaw = transform.eulerAngles.y;

        float turningSpeed = rotateToMovementSpeed;

        bool diagonal = hasForward && hasStrafe;
        if (diagonal)
            turningSpeed *= forwardDiagonalTurnSpeedMultiplier;

        float maxStep = turningSpeed * dt;

        if (diagonal && diagonalExtraDampDegrees > 0f)
        {
            float diff = Mathf.Abs(Mathf.DeltaAngle(currentYaw, targetYaw));
            float damp = Mathf.Clamp01(diff / diagonalExtraDampDegrees);
            maxStep *= damp;
        }

        float newYawSmooth = Mathf.MoveTowardsAngle(currentYaw, targetYaw, maxStep);
        transform.rotation = Quaternion.Euler(0f, newYawSmooth, 0f);
    }
    #endregion

    #region Camera
    private void UpdateCamera(float dt)
    {
        if (!playerCamera) return;

        if (!isFirstPerson && !isTransitioningView)
        {
            float scroll = 0f;
            if (Mouse.current != null)
                scroll = Mouse.current.scroll.ReadValue().y / 120f;
            if (Mathf.Abs(scroll) > 0.01f)
                targetDistance = Mathf.Clamp(targetDistance - scroll * zoomSensitivity, minDistance, maxDistance);
            currentDistance = Mathf.MoveTowards(currentDistance, targetDistance, dt * 10f);
        }

        if (isFirstPerson) UpdateFirstPersonLook(dt);
        else UpdateThirdPersonLook(dt);

        if (isFirstPerson) PositionFirstPersonCamera(dt);
        else PositionThirdPersonCamera(dt);
    }

    private void UpdateThirdPersonLook(float dt)
    {
        if ((leftMouseHeld || rightMouseHeld) && !isTransitioningView)
        {
            float dx = lookDelta.x;
            float dy = lookDelta.y;
            float yawDelta = (dx / Mathf.Max(1, Screen.width)) * orbitSensitivityX;
            float pitchDelta = (dy / Mathf.Max(1, Screen.height)) * orbitSensitivityY;
            cameraYaw += yawDelta;
            cameraPitch = Mathf.Clamp(cameraPitch - pitchDelta, minPitch, maxPitch);
        }
        else if (!rightMouseHeld)
        {
            float targetYaw = transform.eulerAngles.y;
            float diff = Mathf.Abs(Mathf.DeltaAngle(cameraYaw, targetYaw));
            cameraYaw = Mathf.MoveTowardsAngle(cameraYaw, targetYaw, followYawLag * dt * diff);
        }
    }

    private void UpdateFirstPersonLook(float dt)
    {
        float dx = lookDelta.x;
        float dy = lookDelta.y;
        float yawDelta = (dx / Mathf.Max(1, Screen.width)) * fpLookSensitivityX;
        float pitchDelta = (dy / Mathf.Max(1, Screen.height)) * fpLookSensitivityY;
        cameraYaw += yawDelta;
        cameraPitch = Mathf.Clamp(cameraPitch - pitchDelta, fpMinPitch, fpMaxPitch);
    }

    private void PositionThirdPersonCamera(float dt)
    {
        Vector3 pivotPos = transform.position + Vector3.up * cameraHeightOffset;
        Quaternion camRot = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
        Vector3 desired = pivotPos - camRot * Vector3.forward * currentDistance;

        if (Physics.SphereCast(pivotPos, cameraCollisionRadius,
            (desired - pivotPos).normalized,
            out RaycastHit hit, currentDistance, cameraCollisionMask, QueryTriggerInteraction.Ignore))
        {
            float hitDist = hit.distance - 0.1f;
            float safeDist = Mathf.Clamp(hitDist, minDistance * 0.35f, currentDistance);
            Vector3 collidedPos = pivotPos - camRot * Vector3.forward * safeDist;
            playerCamera.transform.position = Vector3.Lerp(playerCamera.transform.position, collidedPos, dt * cameraCollisionRecoverSpeed);
        }
        else
        {
            playerCamera.transform.position = desired;
        }

        playerCamera.transform.rotation = camRot;
    }

    private void PositionFirstPersonCamera(float dt)
    {
        Vector3 basePos = firstPersonAnchor ? firstPersonAnchor.position : transform.position + Vector3.up * cameraHeightOffset;
        Vector3 bobbed = basePos + (enableHeadBob ? ComputeHeadBob(dt) : Vector3.zero);
        playerCamera.transform.position = Vector3.Lerp(playerCamera.transform.position, bobbed, dt * 20f);
        playerCamera.transform.rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
    }
    #endregion

    #region Head Bob
    private Vector3 ComputeHeadBob(float dt)
    {
        Vector2 planar = new Vector2(velocity.x, velocity.z);
        float speed = planar.magnitude;
        if (!isGrounded || speed < 0.1f)
        {
            bobTimer = Mathf.MoveTowards(bobTimer, 0f, dt * bobReturnSpeed);
            bobOffset = Vector3.Lerp(bobOffset, Vector3.zero, dt * bobReturnSpeed);
            return bobOffset;
        }

        float norm = Mathf.Clamp01(speed / runSpeed);
        float freq = Mathf.Lerp(bobFreqWalk, bobFreqRun, norm);
        bobTimer += dt * freq;
        float horiz = Mathf.Cos(bobTimer) * bobAmpHorizontal;
        float vert = Mathf.Sin(bobTimer * 2f) * bobAmpVertical;
        bobOffset = new Vector3(horiz, vert, 0f);
        return bobOffset;
    }
    #endregion

    #region View Toggle / Transitions
    private void ToggleView()
    {
        if (isFirstPerson)
        {
            if (useEyeCloseTransition) StartCoroutine(EyeCloseToThirdPerson());
            else SetThirdPersonInstant();
        }
        else StartCoroutine(ThirdPersonToFirstPerson());
    }

    private void SetThirdPersonInstant()
    {
        isFirstPerson = false;
        isTransitioningView = false;
        ApplyCursorStateTP();
        cameraPitch = Mathf.Clamp(cameraPitch, minPitch, maxPitch);
    }

    private IEnumerator ThirdPersonToFirstPerson()
    {
        if (!firstPersonAnchor)
        {
            isFirstPerson = true;
            ApplyCursorStateFP();
            yield break;
        }
        isTransitioningView = true;
        ApplyCursorStateFP();

        Vector3 startPos = playerCamera.transform.position;
        Quaternion startRot = playerCamera.transform.rotation;
        Quaternion targetRot = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
        Vector3 targetPos = firstPersonAnchor.position;

        float elapsed = 0f;
        while (elapsed < tpToFpTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / tpToFpTransitionDuration);
            float s = Mathf.SmoothStep(0f, 1f, t);
            float arc = Mathf.Sin(s * Mathf.PI) * tpToFpVerticalArc;
            Vector3 pos = Vector3.Lerp(startPos, targetPos, s) + Vector3.up * arc;
            playerCamera.transform.position = pos;
            playerCamera.transform.rotation = Quaternion.Slerp(startRot, targetRot, s);
            yield return null;
        }

        isFirstPerson = true;
        isTransitioningView = false;
        cameraPitch = Mathf.Clamp(cameraPitch, fpMinPitch, fpMaxPitch);
    }

    private IEnumerator EyeCloseToThirdPerson()
    {
        isTransitioningView = true;
        if (!eyeImage)
        {
            SetThirdPersonInstant();
            yield break;
        }

        float total = Mathf.Max(0.05f, eyeCloseTotalDuration);
        float fadeInDur = total * eyeCloseFadeInPortion;
        float fadeOutDur = total - fadeInDur;

        float t = 0f;
        while (t < fadeInDur)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / fadeInDur);
            eyeImage.color = new Color(eyeCloseColor.r, eyeCloseColor.g, eyeCloseColor.b, a);
            yield return null;
        }

        isFirstPerson = false;
        ApplyCursorStateTP();
        cameraYaw = transform.eulerAngles.y;
        cameraPitch = Mathf.Clamp(cameraPitch, minPitch, maxPitch);

        t = 0f;
        while (t < fadeOutDur)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / fadeOutDur);
            eyeImage.color = new Color(eyeCloseColor.r, eyeCloseColor.g, eyeCloseColor.b, a);
            yield return null;
        }
        eyeImage.color = new Color(eyeCloseColor.r, eyeCloseColor.g, eyeCloseColor.b, 0f);
        isTransitioningView = false;
    }
    #endregion

    #region Animation
    private void UpdateAnimation(float dt)
    {
        if (!animator) return;

        // Calculate all animation values
        CalculateAnimationValues(dt);
        
        // Set all animator parameters
        SetAnimatorParameters();
    }

    private void CalculateAnimationValues(float dt)
    {
        // Speed calculation (overall movement speed normalized)
        Vector3 horiz = new Vector3(velocity.x, 0f, velocity.z);
        float targetSpeed = Mathf.Clamp01(horiz.magnitude / runSpeed);
        animSpeedValue = Mathf.MoveTowards(animSpeedValue, targetSpeed, speedBlendAcceleration * dt);

        // Calculate movement relative to character's forward direction
        Vector3 localVelocity = transform.InverseTransformDirection(horiz);
        
        // Horizontal and Vertical (relative to character facing)
        float targetHorizontal = Mathf.Clamp(localVelocity.x / runSpeed, -1f, 1f);
        float targetVertical = Mathf.Clamp(localVelocity.z / runSpeed, -1f, 1f);
        
        animHorizontal = Mathf.MoveTowards(animHorizontal, targetHorizontal, speedBlendAcceleration * dt);
        animVertical = Mathf.MoveTowards(animVertical, targetVertical, speedBlendAcceleration * dt);

        // Turn Direction (how fast character is rotating)
        float currentYaw = transform.eulerAngles.y;
        float yawDelta = Mathf.DeltaAngle(previousYaw, currentYaw);
        float targetTurnDirection = Mathf.Clamp(yawDelta / (Time.deltaTime * 180f), -1f, 1f);
        animTurnDirection = Mathf.MoveTowards(animTurnDirection, targetTurnDirection, turnDirectionSensitivity * dt);
        previousYaw = currentYaw;

        // Strafe detection
        bool isMoving = animSpeedValue > strafeDetectionSpeed;
        bool hasHorizontalMovement = Mathf.Abs(animHorizontal) > 0.1f;
        bool hasForwardMovement = animVertical > 0.1f;
        bool hasBackwardMovement = animVertical < -0.1f;

        // IsStrafing: moving sideways with minimal forward/backward movement
        animIsStrafing = isMoving && hasHorizontalMovement && Mathf.Abs(animVertical) < 0.3f;
        
        // IsBackwardStrafing: moving backward with or without strafe
        animIsBackwardStrafing = isMoving && hasBackwardMovement;
    }

    private void SetAnimatorParameters()
    {
        // Set float parameters
        animator.SetFloat(animParamSpeed, animSpeedValue);
        animator.SetFloat(animParamHorizontal, animHorizontal);
        animator.SetFloat(animParamVertical, animVertical);
        animator.SetFloat(animParamTurnDirection, animTurnDirection);
        
        // Set bool parameters
        animator.SetBool(animParamIsGrounded, isGrounded);
        animator.SetBool(animParamIsRunning, isRunning && animSpeedValue > 0.01f);
        animator.SetBool(animParamIsStrafing, animIsStrafing);
        animator.SetBool(animParamIsBackwardStrafing, animIsBackwardStrafing);
    }

    // Helper method to trigger animator actions
    private void TriggerAnimatorAction(string parameterName)
    {
        if (animator && !string.IsNullOrEmpty(parameterName))
        {
            animator.SetTrigger(parameterName);
        }
    }
    #endregion

    #region Pickup / Throw
    private void OnPickupPressed(InputAction.CallbackContext ctx)
    {
        if (isHoldingObject) 
        {
            DropHeld();
            TriggerAnimatorAction(animParamDrop);
        }
        else 
        {
            TryPickup();
            TriggerAnimatorAction(animParamPickup);
        }
    }

    private void OnThrowStarted(InputAction.CallbackContext ctx)
    {
        if (!isHoldingObject || isChargingThrow) return;
        isChargingThrow = true;
        throwChargeTimer = 0f;
        if (throwPowerBar && showThrowBarWhileCharging)
        {
            throwPowerBar.fillAmount = 0f;
            throwPowerBar.gameObject.SetActive(true);
        }
    }

    private void OnThrowCanceled(InputAction.CallbackContext ctx)
    {
        if (!isChargingThrow || !isHoldingObject) return;
        ReleaseThrow();
        TriggerAnimatorAction(animParamThrow);
    }

    private void UpdatePickupThrow(float dt)
    {
        if (isChargingThrow)
        {
            throwChargeTimer += dt;
            float pct = Mathf.Clamp01(throwChargeTimer / Mathf.Max(0.01f, throwChargeTime));
            float curved = throwChargeCurve != null ? throwChargeCurve.Evaluate(pct) : pct;
            if (throwPowerBar && showThrowBarWhileCharging) throwPowerBar.fillAmount = curved;
        }
        if (isHoldingObject) UpdateHeldObject(dt);
    }

    private void TryPickup()
    {
        GameObject target = FindPickupCandidate();
        if (!target) return;
        heldObject = target;
        heldRb = heldObject.GetComponent<Rigidbody>();
        if (heldRb)
        {
            heldRb.useGravity = false;
            heldRb.isKinematic = false;
            heldRb.linearVelocity = Vector3.zero;
            heldRb.angularVelocity = Vector3.zero;
        }
        isHoldingObject = true;
    }

    private GameObject FindPickupCandidate()
    {
        if (isFirstPerson)
        {
            Vector3 origin = playerCamera.transform.position;
            Vector3 dir = playerCamera.transform.forward;
            if (Physics.Raycast(origin, dir, out RaycastHit hit, pickupRange, pickupMask, QueryTriggerInteraction.Collide))
            {
                if (IsPickupValid(hit.collider.gameObject)) return hit.collider.gameObject;
            }
        }
        else
        {
            Vector3 center = transform.position + Vector3.up * 1.1f;
            Collider[] hits = Physics.OverlapSphere(center, pickupRange, pickupMask, QueryTriggerInteraction.Collide);
            float closest = float.MaxValue;
            GameObject best = null;
            foreach (var c in hits)
            {
                if (!IsPickupValid(c.gameObject)) continue;
                float d = Vector3.Distance(center, c.transform.position);
                if (d < closest) { closest = d; best = c.gameObject; }
            }
            return best;
        }
        return null;
    }

    private bool IsPickupValid(GameObject obj)
    {
        if (!obj) return false;
        if (obj.TryGetComponent(out SimplePickup sp))
            if (!sp.CanBePickedUp()) return false;
        return obj.GetComponent<Rigidbody>() != null;
    }

    private void UpdateHeldObject(float dt)
    {
        if (!heldObject) { ClearHeld(); return; }
        Vector3 basePos;
        Quaternion baseRot;
        if (isFirstPerson)
        {
            basePos = playerCamera.transform.position + playerCamera.transform.forward * holdDistance + playerCamera.transform.up * holdHeightOffset;
            baseRot = playerCamera.transform.rotation;
        }
        else
        {
            Vector3 refPos = transform.position + Vector3.up * (cameraHeightOffset * 0.7f);
            basePos = refPos + playerCamera.transform.forward * holdDistance + playerCamera.transform.up * holdHeightOffset;
            baseRot = playerCamera.transform.rotation;
        }

        heldObject.transform.position = Vector3.Lerp(heldObject.transform.position, basePos, dt * holdLerpSpeed);
        if (rotateHeldObjectToCamera)
            heldObject.transform.rotation = Quaternion.Slerp(heldObject.transform.rotation, baseRot, dt * holdLerpSpeed);

        if (heldRb)
        {
#if UNITY_600_OR_NEWER
            heldRb.linearVelocity = Vector3.zero;
            heldRb.angularVelocity = Vector3.zero;
#else
            heldRb.linearVelocity = Vector3.zero;
            heldRb.angularVelocity = Vector3.zero;
#endif
        }
    }

    private void ReleaseThrow()
    {
        float pct = Mathf.Clamp01(throwChargeTimer / Mathf.Max(0.01f, throwChargeTime));
        float curved = throwChargeCurve != null ? throwChargeCurve.Evaluate(pct) : pct;
        float force = Mathf.Lerp(throwMinForce, throwMaxForce, curved);
        PerformThrow(force);
        isChargingThrow = false;
        throwChargeTimer = 0f;
        if (throwPowerBar) throwPowerBar.gameObject.SetActive(false);
    }

    private void PerformThrow(float force)
    {
        if (!isHoldingObject || !heldObject) { ClearHeld(); return; }
        if (heldRb)
        {
            heldRb.useGravity = true;
            heldRb.isKinematic = false;
            Vector3 dir = playerCamera.transform.forward;
            Vector3 finalVel = dir * (force / Mathf.Max(1f, heldRb.mass))
                               + Vector3.up * (force / Mathf.Max(1f, heldRb.mass) * forwardThrowUpwardFactor);
            heldRb.AddForce(finalVel, ForceMode.VelocityChange);
        }
        ClearHeld();
    }

    private void DropHeld()
    {
        if (!isHoldingObject) return;
        if (heldRb)
        {
            heldRb.useGravity = true;
            heldRb.isKinematic = false;
        }
        ClearHeld();
    }

    private void ClearHeld()
    {
        heldObject = null;
        heldRb = null;
        isHoldingObject = false;
        isChargingThrow = false;
        if (throwPowerBar) throwPowerBar.gameObject.SetActive(false);
    }
    #endregion

    #region Eye Close Overlay
    private void CreateEyeCloseOverlay()
    {
        GameObject canvasGO = new GameObject("EyeCloseCanvas");
        eyeCanvas = canvasGO.AddComponent<Canvas>();
        eyeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        eyeCanvas.sortingOrder = 9999;
        var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        GameObject imgGO = new GameObject("EyeCloseImage");
        imgGO.transform.SetParent(canvasGO.transform, false);
        eyeImage = imgGO.AddComponent<UnityEngine.UI.Image>();
        eyeImage.color = new Color(eyeCloseColor.r, eyeCloseColor.g, eyeCloseColor.b, 0f);

        RectTransform rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        DontDestroyOnLoad(canvasGO);
    }
    #endregion

    #region Cursor Helpers
    private void ApplyCursorStateFP()
    {
        if (lockCursorInFP) Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = !hideCursorInFP;
    }
    private void ApplyCursorStateTP()
    {
        // Cursor state is now handled by UpdateCursorVisibility()
    }
    #endregion

    #region Public Methods for External Access
    /// <summary>
    /// Manually trigger jump animation (useful for external scripts)
    /// </summary>
    public void TriggerJumpAnimation() => TriggerAnimatorAction(animParamJump);
    
    /// <summary>
    /// Manually trigger pickup animation
    /// </summary>
    public void TriggerPickupAnimation() => TriggerAnimatorAction(animParamPickup);
    
    /// <summary>
    /// Manually trigger drop animation
    /// </summary>
    public void TriggerDropAnimation() => TriggerAnimatorAction(animParamDrop);
    
    /// <summary>
    /// Manually trigger throw animation
    /// </summary>
    public void TriggerThrowAnimation() => TriggerAnimatorAction(animParamThrow);
    
    /// <summary>
    /// Manually trigger interact animation
    /// </summary>
    public void TriggerInteractAnimation() => TriggerAnimatorAction(animParamInteract);

    /// <summary>
    /// Get current animation values for debugging
    /// </summary>
    public void GetAnimationValues(out float speed, out float horizontal, out float vertical, 
        out float turnDirection, out bool isStrafing, out bool isBackwardStrafing)
    {
        speed = animSpeedValue;
        horizontal = animHorizontal;
        vertical = animVertical;
        turnDirection = animTurnDirection;
        isStrafing = animIsStrafing;
        isBackwardStrafing = animIsBackwardStrafing;
    }
    #endregion

    #region Gizmos
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying && cameraPivot)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(cameraPivot.position + Vector3.up * cameraHeightOffset, 0.2f);
        }
        if (firstPersonAnchor)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(firstPersonAnchor.position, 0.1f);
        }
        Gizmos.color = Color.yellow;
        if (isFirstPerson)
        {
            Camera cam = playerCamera ? playerCamera : Camera.main;
            if (cam)
            {
                Gizmos.DrawLine(cam.transform.position, cam.transform.position + cam.transform.forward * pickupRange);
                Gizmos.DrawWireSphere(cam.transform.position + cam.transform.forward * pickupRange, 0.15f);
            }
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.1f, pickupRange);
        }
    }
    #endregion
}