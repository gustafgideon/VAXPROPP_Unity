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

    [Header("Third Person Zoom Settings")]
    [SerializeField] private float minCameraDistance = 1f;
    [SerializeField] private float maxCameraDistance = 10f;
    [SerializeField] private float defaultCameraDistance = 5f;
    [SerializeField] private float zoomSensitivity = 1f;
    [SerializeField] private float zoomSpeed = 8f;
    [SerializeField] private bool invertScrollDirection = false;

    [Header("Player Pivot Settings")]
    [SerializeField] private float playerRotationSpeed = 10f;
    [SerializeField] private bool instantPivot = false;
    [SerializeField] private float pivotThreshold = 0.1f;
    [SerializeField] private float pivotSmoothing = 0.15f;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float gravityMultiplier = 2.5f;
    [SerializeField] private float airControl = 0.8f;
    [SerializeField] private float crouchSpeed = 3f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float standingHeight = 2f;
    [SerializeField] private float crouchTransitionSpeed = 8f;

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
    [SerializeField] private float throwForce = 600f; // Deprecated, replaced by throw settings below
    [SerializeField] private LayerMask pickupMask = -1; // All layers by default
    [SerializeField] private float holdDistance = 1.5f;
    [SerializeField] private float holdPositionSpeed = 10f;

    [Header("View Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 80f;
    [SerializeField] private float defaultFOV = 60f;

    // ---- THROW SYSTEM ----
    [Header("Throw Settings")]
    [SerializeField] private float minThrowForce = 200f;
    [SerializeField] private float maxThrowForce = 1200f;
    [SerializeField] private float maxHoldTime = 1.5f;

    [Header("Throw UI")]
    [SerializeField] private Image throwPowerBar; // assign in inspector!

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

    // Zoom state
    private float currentCameraDistance;
    private float targetCameraDistance;

    // View transition state
    private bool isTransitioning = false;

    // Pivot smoothing
    private float targetPlayerRotation;
    private float pivotVelocity;

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
        currentCameraYOffset = 0f;
        targetCameraYOffset = 0f;

        // Initialize camera rotation and distance
        horizontalRotation = transform.eulerAngles.y;
        verticalRotation = 0f;
        targetPlayerRotation = horizontalRotation;
        currentCameraDistance = defaultCameraDistance;
        targetCameraDistance = defaultCameraDistance;
        thirdPersonDistance = defaultCameraDistance;

        // Initialize FOV
        playerCamera.fieldOfView = defaultFOV;

        // Auto-detect platform and set scroll direction
        AutoDetectScrollDirection();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
        throwAction = actions["Throw"]; // Map to left mouse button in Input Actions

        moveAction.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        moveAction.canceled += ctx => moveInput = Vector2.zero;

        lookAction.performed += ctx =>
        {
            if (!isTransitioning)
                lookInput = ctx.ReadValue<Vector2>();
        };
        lookAction.canceled += ctx => lookInput = Vector2.zero;

        runAction.performed += ctx => isRunning = true;
        runAction.canceled += ctx => isRunning = false;

        crouchAction.performed += ctx => { crouchInputHeld = true; StartCrouch(); };
        crouchAction.canceled += ctx => { crouchInputHeld = false; StopCrouch(); };

        zoomAction.performed += ctx => HandleZoom(ctx.ReadValue<float>());

        jumpAction.performed += _ => TryJump();
        attackAction.performed += _ => TryAttack();
        interactAction.performed += _ => TryInteract();
        pickupAction.performed += _ => TryPickup();
        switchViewAction.performed += _ =>
        {
            if (!isTransitioning)
                ToggleView();
        };

        throwAction.started += ctx => StartChargingThrow();
        throwAction.canceled += ctx => ReleaseThrow();
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
        UpdateCrouchState();

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
    }

    private void UpdateCrouchState()
    {
        if (crouchInputHeld && !isCrouching)
        {
            StartCrouch();
        }
        else if (!crouchInputHeld && isCrouching)
        {
            StopCrouch();
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        float moveAmount = moveInput.magnitude;
        float currentSpeed = isCrouching ? crouchSpeed : (isRunning ? runSpeed : walkSpeed);
        float normalizedSpeed = moveAmount * (isRunning ? 1f : 0.5f);

        animator.SetFloat(animParamSpeed, normalizedSpeed);
        animator.SetBool(animParamIsGrounded, isGrounded);
        animator.SetBool(animParamIsRunning, isRunning);
        animator.SetBool(animParamIsCrouching, isCrouching);
    }

    private void UpdateMovement()
    {
        isGrounded = characterController.isGrounded;

        Vector3 moveDir = Vector3.zero;

        if (moveInput.magnitude > pivotThreshold)
        {
            if (isFirstPerson)
            {
                moveDir = transform.right * moveInput.x + transform.forward * moveInput.y;
            }
            else
            {
                float cameraYRotation = horizontalRotation;
                Vector3 forward = new Vector3(Mathf.Sin(cameraYRotation * Mathf.Deg2Rad), 0, Mathf.Cos(cameraYRotation * Mathf.Deg2Rad));
                Vector3 right = new Vector3(forward.z, 0, -forward.x);

                moveDir = right * moveInput.x + forward * moveInput.y;
                moveDir.Normalize();

                if (moveDir.magnitude > pivotThreshold)
                {
                    targetPlayerRotation = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;

                    if (instantPivot)
                    {
                        transform.rotation = Quaternion.Euler(0, targetPlayerRotation, 0);
                    }
                    else
                    {
                        float currentYRotation = transform.eulerAngles.y;
                        float smoothedRotation = Mathf.SmoothDampAngle(currentYRotation, targetPlayerRotation,
                            ref pivotVelocity, pivotSmoothing);
                        transform.rotation = Quaternion.Euler(0, smoothedRotation, 0);
                    }
                }
            }
        }

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
        if (isFirstPerson)
        {
            transform.Rotate(Vector3.up * lookInput.x * mouseSensitivity);

            verticalRotation -= lookInput.y * mouseSensitivity;
            verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
            cameraHolder.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
        }
        else
        {
            horizontalRotation += lookInput.x * mouseSensitivity;
            verticalRotation -= lookInput.y * mouseSensitivity;
            verticalRotation = Mathf.Clamp(verticalRotation, minVerticalAngle, maxVerticalAngle);
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

        // Phase 1: Eyes closing (fade to black)
        float fadeInDuration = eyeCloseTransitionDuration * 0.3f;
        float elapsedTime = 0f;

        while (elapsedTime < fadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsedTime / fadeInDuration);
            eyeCloseImage.color = new Color(eyeCloseColor.r, eyeCloseColor.g, eyeCloseColor.b, alpha);
            yield return null;
        }

        // Phase 2: Switch to third person while screen is black
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

        // Phase 3: Eyes opening (fade from black)
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

    private void UpdateCrouchTransition()
    {
        characterController.height = Mathf.Lerp(characterController.height, targetHeight,
            Time.deltaTime * crouchTransitionSpeed);

        if (isFirstPerson)
        {
            currentCameraYOffset = Mathf.Lerp(currentCameraYOffset, targetCameraYOffset,
                Time.deltaTime * crouchTransitionSpeed);
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

    private void TryJump()
    {
        if (isGrounded && !isCrouching)
        {
            currentVelocity.y = jumpForce;

            if (animator != null)
            {
                animator.SetTrigger(animParamJump);
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
                if (UnityEngine.Random.value > 0.5f)
                {
                    animator.SetTrigger(animParamPunch);
                }
                else
                {
                    animator.SetTrigger(animParamKick);
                }
            }
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
            // If holding, drop
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

    private void StartCrouch()
    {
        if (!isCrouching)
        {
            isCrouching = true;
            targetHeight = crouchHeight;

            if (isFirstPerson)
            {
                float heightDifference = standingHeight - crouchHeight;
                targetCameraYOffset = -heightDifference * 0.5f;
            }
        }
    }

    private void StopCrouch()
    {
        if (isCrouching)
        {
            isCrouching = false;
            targetHeight = standingHeight;

            if (isFirstPerson)
            {
                targetCameraYOffset = 0f;
            }
        }
    }

    private void TriggerAnimationSafely(string methodName)
    {
        try
        {
            var animController = GetComponentInChildren(System.Type.GetType("HumanoidAnimationController"));
            if (animController != null)
            {
                var method = animController.GetType().GetMethod(methodName);
                if (method != null)
                {
                    method.Invoke(animController, null);
                }
            }
        }
        catch (System.Exception)
        {
        }
    }

    private void OnDestroy()
    {
        if (overlayCanvas != null)
        {
            Destroy(overlayCanvas.gameObject);
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
    }

    // ---- THROW SYSTEM ----

    private void StartChargingThrow()
    {
        if (isHoldingObject && heldObject != null && !isChargingThrow)
        {
            isChargingThrow = true;
            throwChargeTimer = 0f;
            ShowThrowBar(true);
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
}

// Interfaces go OUTSIDE the PlayerController class
public interface IInteractable
{
    void Interact();
}

public interface IDamageable
{
    void TakeDamage(int amount);
}