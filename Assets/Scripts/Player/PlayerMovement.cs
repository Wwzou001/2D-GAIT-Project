using UnityEngine;
using UnityEngine.InputSystem;
using SteeringBehaviours;
 
// Moves the player in one of two modes. Press the toggle key (Tab) to switch:
//   WASD:       moves one grid cell at a time through GridMover.
//   SeekArrive: left click sets a target and the player steers toward it
//               using Seek or Arrive, with physics and obstacle avoidance.
// It also keeps track of which way the player is facing, so PlayerShooting
// knows which direction to fire.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    private GridMover gridMover;
    private Vector2 movementInput;
 
    // The direction the player last moved or is drifting toward.
    // PlayerShooting reads this so flames fire the way the player is facing.
    public Vector2 FacingDirection { get; private set; } = Vector2.up;
 
    // Seek and arrive for the player
    public enum MovementMode { WASD, SeekArrive }
    public enum ChaseBehaviour { Seek, Arrive }
 
    public MovementMode mode = MovementMode.WASD;
    public Key toggleKey = Key.Tab;
 
    public ChaseBehaviour behaviour = ChaseBehaviour.Arrive;
    public float maxSpeed = 5f;
    public float maxAccel = 10f;
    public float accelTime = 0.25f;
 
    public float arrivePercent = 0.3f;
    public float minArriveRadius = 0.5f;
    public float maxArriveRadius = 3f;
    private float currentArriveRadius;
 
    public Transform targetMarker;
    public float targetReachedTolerance = 0.1f;
 
    public Animator animator;
    public string walkingBoolParam = "Walking";
    public float minSpeedToAnimate = 0.1f;
 
    public AvoidanceSettings avoidance = new AvoidanceSettings
    {
        Enabled = false,
        LookAheadDistance = 2f,
        CheckRadius = 0.4f,
        AngleStep = 10f
    };
 
    private Rigidbody2D rb;
    private Vector2? targetPos;
    public float overshootBuffer = 0.3f;
    private float minDistanceReached = float.MaxValue;
 
    void Start()
    {
    }
 
    void Update()
    {
        // Tab switches between WASD and SeekArrive.
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            mode = mode == MovementMode.WASD ? MovementMode.SeekArrive : MovementMode.WASD;
            ApplyMode();
        }
 
        // In SeekArrive mode, a left click sets the target.
        if (mode == MovementMode.SeekArrive && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector3 screenPos = Mouse.current.position.ReadValue();
            Vector2 clickPos = Camera.main.ScreenToWorldPoint(screenPos);
            targetPos = clickPos;
            minDistanceReached = float.MaxValue;
 
            // The slowing down radius depends on how far away the click was.
            float clickDistance = Vector2.Distance(rb.position, clickPos);
            currentArriveRadius = Mathf.Clamp(clickDistance * arrivePercent, minArriveRadius, maxArriveRadius);
 
            if (targetMarker != null)
            {
                targetMarker.position = clickPos;
                targetMarker.gameObject.SetActive(true);
            }
        }
    }
 
    // Physics movement for SeekArrive mode.
    void FixedUpdate()
    {
        if (mode != MovementMode.SeekArrive)
            return;
 
        Vector2 currentPos = rb.position;
 
        if (targetPos.HasValue)
        {
            float distance = Vector2.Distance(currentPos, targetPos.Value);
            bool shouldStop = distance <= targetReachedTolerance;
 
            if (shouldStop)
            {
                targetPos = null;
 
                if (behaviour == ChaseBehaviour.Arrive)
                {
                    rb.linearVelocity = Vector2.zero; // Arrive stops precisely
                }
            }
            else
            {
                Vector2 desiredVelocity = behaviour == ChaseBehaviour.Seek
                    ? Steering.Seek(currentPos, targetPos.Value, maxSpeed, avoidance)
                    : Steering.Arrive(currentPos, targetPos.Value, currentArriveRadius, maxSpeed, avoidance);
 
                Vector2 force = Steering.VelocityToForce(desiredVelocity, rb, accelTime, maxAccel);
                rb.AddForce(force);
            }
        }
 
        // Face the way the player is drifting, ignoring tiny movements.
        if (rb.linearVelocity.magnitude > 0.1f)
        {
            FacingDirection = rb.linearVelocity.normalized;
        }
 
        UpdateAnimation();
    }
 
    // Switches the Rigidbody between the two modes.
    private void ApplyMode()
    {
        if (mode == MovementMode.WASD)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
 
            // The target flag only makes sense in SeekArrive mode.
            if (targetMarker != null)
                targetMarker.gameObject.SetActive(false);
        }
        else
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            targetPos = null;
        }
    }
 
    private void UpdateAnimation()
    {
        if (animator == null)
            return;
 
        bool isMoving = rb.linearVelocity.magnitude > minSpeedToAnimate;
        animator.SetBool(walkingBoolParam, isMoving);
 
        if (isMoving)
        {
            transform.up = rb.linearVelocity;
        }
    }
 
    void Awake()
    {
        gridMover = GetComponent<GridMover>();
        rb = GetComponent<Rigidbody2D>();   // seek and arrive
        ApplyMode();
    }
 
    // WASD movement. Hooked up to the Move action of the PlayerInput component.
    public void Move(InputAction.CallbackContext context)
    {
        if (mode != MovementMode.WASD)
            return;
 
        if (!context.performed)
        {
            return;
        }
 
        Vector2 movementInput = context.ReadValue<Vector2>();
 
        if (movementInput == Vector2.zero)
        {
            return;
        }
 
        // Each branch moves one cell and remembers which way the player is facing.
        // The facing direction is updated even if the move is blocked, so the
        // player can still aim at a wall.
        if (movementInput.y > 0)
        {
            gridMover.TryMove(Direction.Up);
            FacingDirection = Vector2.up;
        }
        else if (movementInput.y < 0)
        {
            gridMover.TryMove(Direction.Down);
            FacingDirection = Vector2.down;
        }
        else if (movementInput.x < 0)
        {
            gridMover.TryMove(Direction.Left);
            FacingDirection = Vector2.left;
        }
        else if (movementInput.x > 0)
        {
            gridMover.TryMove(Direction.Right);
            FacingDirection = Vector2.right;
        }
    }
 
    // Draws a red circle at the target in the Scene view.
    private void OnDrawGizmos()
    {
        if (mode == MovementMode.SeekArrive && targetPos.HasValue)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(targetPos.Value, 0.2f);
        }
    }
}