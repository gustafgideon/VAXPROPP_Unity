using UnityEngine;

/// <summary>
/// Bridge component that connects PlayerController state to Animator parameters
/// Handles animation state management for the humanoid character
/// </summary>
[RequireComponent(typeof(Animator))]
public class HumanoidAnimationController : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float movementBlendSpeed = 5f;
    [SerializeField] private float animationTransitionSpeed = 0.1f;
    
    // Animator reference
    private Animator animator;
    private PlayerController playerController;
    
    // Animation parameter hashes for performance
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int MovementSpeedHash = Animator.StringToHash("MovementSpeed");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsHoldingObjectHash = Animator.StringToHash("IsHoldingObject");
    private static readonly int JumpTriggerHash = Animator.StringToHash("JumpTrigger");
    private static readonly int LandTriggerHash = Animator.StringToHash("LandTrigger");
    private static readonly int PickupTriggerHash = Animator.StringToHash("PickupTrigger");
    private static readonly int PutdownTriggerHash = Animator.StringToHash("PutdownTrigger");
    private static readonly int KickTriggerHash = Animator.StringToHash("KickTrigger");
    private static readonly int PunchTriggerHash = Animator.StringToHash("PunchTrigger");
    
    // State tracking
    private bool wasGrounded = true;
    private bool wasHoldingObject = false;
    private Vector2 lastMoveInput;
    private float currentMovementSpeed;
    
    private void Awake()
    {
        animator = GetComponent<Animator>();
        playerController = GetComponentInParent<PlayerController>();
        
        if (playerController == null)
        {
            Debug.LogError("HumanoidAnimationController requires a PlayerController in parent objects!");
        }
    }
    
    private void Update()
    {
        if (playerController == null || animator == null) return;
        
        UpdateMovementAnimations();
        UpdateStateAnimations();
        UpdateTriggerAnimations();
    }
    
    /// <summary>
    /// Updates movement-related animation parameters
    /// </summary>
    private void UpdateMovementAnimations()
    {
        // Get movement input via reflection to avoid coupling
        Vector2 moveInput = GetMoveInput();
        bool isMoving = moveInput.magnitude > 0.1f;
        bool isRunning = GetIsRunning();
        
        // Smooth movement speed for blend tree
        float targetSpeed = isMoving ? (isRunning ? 1f : 0.5f) : 0f;
        currentMovementSpeed = Mathf.Lerp(currentMovementSpeed, targetSpeed, Time.deltaTime * movementBlendSpeed);
        
        // Set animator parameters
        animator.SetBool(IsMovingHash, isMoving);
        animator.SetFloat(MovementSpeedHash, currentMovementSpeed);
        animator.SetBool(IsRunningHash, isRunning && isMoving);
        
        lastMoveInput = moveInput;
    }
    
    /// <summary>
    /// Updates state-based animation parameters
    /// </summary>
    private void UpdateStateAnimations()
    {
        bool isCrouching = GetIsCrouching();
        bool isGrounded = GetIsGrounded();
        bool isHoldingObject = GetIsHoldingObject();
        
        animator.SetBool(IsCrouchingHash, isCrouching);
        animator.SetBool(IsGroundedHash, isGrounded);
        animator.SetBool(IsHoldingObjectHash, isHoldingObject);
        
        // Track state changes for triggers
        wasGrounded = isGrounded;
        wasHoldingObject = isHoldingObject;
    }
    
    /// <summary>
    /// Handles trigger-based animations
    /// </summary>
    private void UpdateTriggerAnimations()
    {
        bool isGrounded = GetIsGrounded();
        bool isHoldingObject = GetIsHoldingObject();
        
        // Landing trigger
        if (!wasGrounded && isGrounded)
        {
            animator.SetTrigger(LandTriggerHash);
        }
        
        // Pickup/Putdown triggers
        if (!wasHoldingObject && isHoldingObject)
        {
            animator.SetTrigger(PickupTriggerHash);
        }
        else if (wasHoldingObject && !isHoldingObject)
        {
            animator.SetTrigger(PutdownTriggerHash);
        }
    }
    
    /// <summary>
    /// Public method to trigger jump animation
    /// Call this from PlayerController when jump is initiated
    /// </summary>
    public void TriggerJump()
    {
        animator.SetTrigger(JumpTriggerHash);
    }
    
    /// <summary>
    /// Public method to trigger kick animation
    /// Call this from PlayerController when kick attack is performed
    /// </summary>
    public void TriggerKick()
    {
        animator.SetTrigger(KickTriggerHash);
    }
    
    /// <summary>
    /// Public method to trigger punch animation
    /// Call this from PlayerController when punch attack is performed
    /// </summary>
    public void TriggerPunch()
    {
        animator.SetTrigger(PunchTriggerHash);
    }
    
    // Private methods to get PlayerController state via reflection to avoid tight coupling
    private Vector2 GetMoveInput()
    {
        var field = typeof(PlayerController).GetField("moveInput", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field != null ? (Vector2)field.GetValue(playerController) : Vector2.zero;
    }
    
    private bool GetIsRunning()
    {
        var field = typeof(PlayerController).GetField("isRunning", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field != null ? (bool)field.GetValue(playerController) : false;
    }
    
    private bool GetIsCrouching()
    {
        var field = typeof(PlayerController).GetField("isCrouching", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field != null ? (bool)field.GetValue(playerController) : false;
    }
    
    private bool GetIsGrounded()
    {
        var field = typeof(PlayerController).GetField("isGrounded", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field != null ? (bool)field.GetValue(playerController) : false;
    }
    
    private bool GetIsHoldingObject()
    {
        var field = typeof(PlayerController).GetField("isHoldingObject", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field != null ? (bool)field.GetValue(playerController) : false;
    }
}