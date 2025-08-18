using UnityEngine;
using UnityEngine.InputSystem;
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
    [SerializeField] private float throwForce = 600f;
    [SerializeField] private LayerMask pickupMask = -1; // All layers by default
    [SerializeField] private float holdDistance = 1.5f;
    [SerializeField] private float holdPositionSpeed = 10f;

    [Header("View Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 80f;
    [SerializeField] private float defaultFOV = 60f;

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

    private void Awake()
    {
        InitializeComponents();
        SetupInputActions();
        CreateEyeCloseOverlay();
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
        // Create a canvas for the eye close effect
        GameObject canvasGO = new GameObject("EyeCloseCanvas");
        overlayCanvas = canvasGO.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 1000; // Make sure it's on top
        
        // Add CanvasScaler for responsive UI
        var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        // Add GraphicRaycaster
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        
        // Create the overlay image
        eyeCloseOverlay = new GameObject("EyeCloseOverlay");
        eyeCloseOverlay.transform.SetParent(canvasGO.transform, false);
        
        eyeCloseImage = eyeCloseOverlay.AddComponent<UnityEngine.UI.Image>();
        eyeCloseImage.color = eyeCloseColor;
        
        // Make it cover the entire screen
        var rectTransform = eyeCloseOverlay.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        
        // Start invisible
        eyeCloseImage.color = new Color(eyeCloseColor.r, eyeCloseColor.g, eyeCloseColor.b, 0f);
        
        // Make canvas persistent
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

        moveAction.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        moveAction.canceled += ctx => moveInput = Vector2.zero;
        
        lookAction.performed += ctx => 
        {
            if (!isTransitioning) // Disable look input during transition
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
            if (!isTransitioning) // Prevent view switching during transition
                ToggleView();
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
                            attackAction, switchViewAction, interactAction, crouchAction, zoomAction, pickupAction };
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
        
        if (isFirstPerson && !isTransitioning) 
            UpdateHeadBob();
        
        if (currentAttackCooldown > 0)
            currentAttackCooldown -= Time.deltaTime;
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
                        // Use SmoothDampAngle for smoother rotation without jitter
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
        // Align player rotation with camera direction
        transform.rotation = Quaternion.Euler(0, horizontalRotation, 0);
        
        // Set up third person camera position
        horizontalRotation = transform.eulerAngles.y;
        verticalRotation = 0f;
        
        Vector3 playerTargetPosition = transform.position + thirdPersonOffset;
        Vector3 directionToCamera = Quaternion.Euler(verticalRotation, horizontalRotation, 0f) * -Vector3.forward;
        
        // RESET ZOOM TO DEFAULT DISTANCE
        currentCameraDistance = defaultCameraDistance;
        targetCameraDistance = defaultCameraDistance;
        thirdPersonDistance = defaultCameraDistance;
        
        Vector3 desiredCameraPos = playerTargetPosition + directionToCamera * currentCameraDistance;
        
        cameraTransform.position = desiredCameraPos;
        cameraTransform.LookAt(playerTargetPosition);
        
        // Switch view state
        isFirstPerson = false;
        cameraHolder.localPosition = originalCameraPosition;
        cameraHolder.localRotation = Quaternion.identity;
        
        // Brief pause while eyes are closed
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
        
        // Ensure completely transparent
        eyeCloseImage.color = new Color(eyeCloseColor.r, eyeCloseColor.g, eyeCloseColor.b, 0f);
        
        isTransitioning = false;
    }

    private IEnumerator SmoothTransition()
    {
        isTransitioning = true;
        
        // Store starting position and rotation
        Vector3 startPosition = cameraTransform.position;
        Quaternion startRotation = cameraTransform.rotation;
        
        // IMPORTANT: Use the current third person camera's horizontal rotation as the target
        // This ensures continuity between camera direction and player direction
        float targetPlayerRotationValue = horizontalRotation;
        
        // Target position and rotation
        Vector3 targetPosition = firstPersonPosition.position;
        Quaternion targetRotation = Quaternion.Euler(0f, targetPlayerRotationValue, 0f);
        
        float elapsedTime = 0f;
        
        while (elapsedTime < smoothTransitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = smoothTransitionCurve.Evaluate(elapsedTime / smoothTransitionDuration);
            
            // Create a straight line with a slight upward arc to avoid the head
            Vector3 straightPath = Vector3.Lerp(startPosition, targetPosition, progress);
            
            // Add a subtle upward arc that peaks at 50% progress to avoid clipping through head
            float arcOffset = Mathf.Sin(progress * Mathf.PI) * avoidanceArcHeight;
            Vector3 currentPos = straightPath + Vector3.up * arcOffset;
            
            cameraTransform.position = currentPos;
            
            // Smoothly rotate to match the target direction
            cameraTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, progress);
            
            yield return null;
        }
        
        // Finalize first person setup
        isFirstPerson = true;
        
        // CRITICAL: Set the player's rotation to match the third person camera direction
        // This ensures that when we enter first person, the player is facing the same direction
        // as the camera was looking in third person
        transform.rotation = Quaternion.Euler(0f, targetPlayerRotationValue, 0f);
        
        // Reset camera rotation relative to player
        verticalRotation = 0f;
        cameraHolder.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
        currentCameraYOffset = targetCameraYOffset;
        
        // Ensure camera is exactly at first person position with proper local rotation
        cameraTransform.position = firstPersonPosition.position;
        cameraTransform.rotation = transform.rotation; // Match player's rotation exactly
        
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
            
            // TODO: Restore animation controller integration after meta file fix
            // Temporarily removed to get Unity out of safe mode
            /*
            HumanoidAnimationController animController = GetComponentInChildren<HumanoidAnimationController>();
            if (animController != null)
            {
                animController.TriggerJump();
            }
            */
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
            
            // TODO: Restore animation controller integration after meta file fix
            // Temporarily removed to get Unity out of safe mode
            /*
            HumanoidAnimationController animController = GetComponentInChildren<HumanoidAnimationController>();
            if (animController != null)
            {
                // Simple alternating attack system
                if (UnityEngine.Random.value > 0.5f)
                {
                    animController.TriggerPunch();
                }
                else
                {
                    animController.TriggerKick();
                }
            }
            */
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

    private void TryPickup()
    {
        Debug.Log("TryPickup() called - F key pressed!");
        
        if (isHoldingObject)
        {
            Debug.Log("Already holding object, dropping it");
            DropObject();
        }
        else
        {
            GameObject targetObject = null;
            
            if (isFirstPerson)
            {
                // First person: raycast from camera (must look at object)
                Debug.Log("First person mode - using raycast detection");
                
                if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, pickupRange, pickupMask))
                {
                    Debug.Log($"Raycast hit: {hit.collider.name} at distance {hit.distance}");
                    
                    if (hit.collider.TryGetComponent(out SimplePickup pickup))
                    {
                        if (pickup.CanBePickedUp())
                        {
                            targetObject = pickup.gameObject;
                            Debug.Log($"Found pickup object via raycast: {targetObject.name}");
                        }
                    }
                    else
                    {
                        Debug.Log($"Hit object {hit.collider.name} but it doesn't have SimplePickup component");
                    }
                }
                else
                {
                    Debug.Log("Raycast missed - no object in sight");
                }
            }
            else
            {
                // Third person: sphere detection around player (proximity-based)
                Debug.Log("Third person mode - using proximity detection");
                
                Vector3 playerCenter = transform.position + Vector3.up * 1.0f;
                Collider[] nearbyObjects = Physics.OverlapSphere(playerCenter, pickupRange, pickupMask);
                
                Debug.Log($"Found {nearbyObjects.Length} objects in proximity");
                
                float closestDistance = float.MaxValue;
                
                foreach (Collider col in nearbyObjects)
                {
                    if (col.TryGetComponent(out SimplePickup pickup))
                    {
                        if (pickup.CanBePickedUp())
                        {
                            float distance = Vector3.Distance(playerCenter, col.transform.position);
                            Debug.Log($"Pickup object {col.name} at distance {distance}");
                            
                            if (distance < closestDistance)
                            {
                                closestDistance = distance;
                                targetObject = pickup.gameObject;
                            }
                        }
                    }
                }
                
                if (targetObject != null)
                {
                    Debug.Log($"Found closest pickup object via proximity: {targetObject.name} at distance {closestDistance}");
                }
                else
                {
                    Debug.Log("No pickup objects found in proximity");
                }
            }
            
            // Pick up the target object if found
            if (targetObject != null)
            {
                Debug.Log($"Picking up object: {targetObject.name}");
                PickupObject(targetObject);
            }
            else
            {
                Debug.Log("No valid pickup object found");
            }
        }
    }

    private void PickupObject(GameObject obj)
    {
        Debug.Log($"Picking up: {obj.name}");
    
        heldObject = obj;
        heldObjectRb = obj.GetComponent<Rigidbody>();
        isHoldingObject = true;
    
        if (heldObjectRb != null)
        {
            heldObjectRb.useGravity = false;
            heldObjectRb.linearVelocity = Vector3.zero;
            heldObjectRb.angularVelocity = Vector3.zero;
            heldObjectRb.isKinematic = false; // Keep physics but controlled
        }
    
        // Parent to TempParent if available, otherwise to camera
        Transform parentTransform = TempParent.Instance != null ? TempParent.Instance.transform : cameraTransform;
        obj.transform.SetParent(parentTransform);
    
        // Position the object based on camera mode
        if (isFirstPerson)
        {
            // First person: position in front of camera
            obj.transform.localPosition = Vector3.forward * holdDistance;
        }
        else
        {
            // Third person: position in front of player, not camera
            Vector3 playerForward = cameraTransform.forward;
            Vector3 holdPosition = transform.position + Vector3.up * 1.5f + playerForward * holdDistance;
            obj.transform.position = holdPosition;
        }
    
        obj.transform.localRotation = Quaternion.identity;
    }

    private void DropObject()
    {
        if (heldObject != null)
        {
            Debug.Log($"Dropping: {heldObject.name}");
            
            heldObject.transform.SetParent(null);
            
            if (heldObjectRb != null)
            {
                heldObjectRb.useGravity = true;
                
                // Add a small forward velocity when dropping
                Vector3 dropDirection = cameraTransform.forward + Vector3.up * 0.2f;
                heldObjectRb.linearVelocity = dropDirection * 2f;
            }
            
            heldObject = null;
            heldObjectRb = null;
            isHoldingObject = false;
        }
    }

    // Update the held object position each frame
    private void UpdateHeldObject()
    {
        if (isHoldingObject && heldObject != null)
        {
            if (isFirstPerson)
            {
                // First person: use parenting system
                Transform parentTransform = TempParent.Instance != null ? TempParent.Instance.transform : cameraTransform;
            
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
                // Third person: position in front of player
                heldObject.transform.SetParent(null); // Don't parent in third person
            
                Vector3 playerForward = cameraTransform.forward;
                Vector3 targetPosition = transform.position + Vector3.up * 1.5f + playerForward * holdDistance;
                heldObject.transform.position = Vector3.Lerp(heldObject.transform.position, targetPosition, Time.deltaTime * holdPositionSpeed);
            
                // Keep object facing same direction as camera
                heldObject.transform.rotation = Quaternion.Lerp(heldObject.transform.rotation, cameraTransform.rotation, Time.deltaTime * holdPositionSpeed);
            }
        
            // Reset physics to prevent unwanted movement
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

    private void OnDestroy()
    {
        // Clean up the overlay canvas
        if (overlayCanvas != null)
        {
            Destroy(overlayCanvas.gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (playerCamera != null)
        {
            // Existing gizmos
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(playerCamera.transform.position, interactionRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(playerCamera.transform.position, attackRange);
        
            // Pickup gizmos - different for each mode
            if (isFirstPerson)
            {
                // First person: show raycast line
                Gizmos.color = Color.green;
                Vector3 rayStart = cameraTransform.position;
                Vector3 rayEnd = rayStart + cameraTransform.forward * pickupRange;
                Gizmos.DrawLine(rayStart, rayEnd);
                Gizmos.DrawWireSphere(rayEnd, 0.1f);
            }
            else
            {
                // Third person: show proximity sphere around player
                Gizmos.color = Color.green;
                Vector3 playerCenter = transform.position + Vector3.up * 1.0f;
                Gizmos.DrawWireSphere(playerCenter, pickupRange);
            }
        }
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