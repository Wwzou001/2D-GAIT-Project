using UnityEngine;
using UnityEngine.InputSystem; 
using SteeringBehaviours;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{

    
    private GridMover gridMover;
    private Vector2 movementInput;

    // seek and arrive for player
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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            mode = mode == MovementMode.WASD ? MovementMode.SeekArrive : MovementMode.WASD;
            ApplyMode();
        }

        if (mode == MovementMode.SeekArrive && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector3 screenPos = Mouse.current.position.ReadValue();
            Vector2 clickPos = Camera.main.ScreenToWorldPoint(screenPos);
            targetPos = clickPos;
            minDistanceReached = float.MaxValue; 

            float clickDistance = Vector2.Distance(rb.position, clickPos);
            currentArriveRadius = Mathf.Clamp(clickDistance * arrivePercent, minArriveRadius, maxArriveRadius);

            if (targetMarker != null)
            {
                targetMarker.position = clickPos;
                targetMarker.gameObject.SetActive(true);
            }
        }
    }

    // new method for seek and arrive
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

        UpdateAnimation();
    }

    private void ApplyMode()
    {
        if (mode == MovementMode.WASD)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;

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

    void Awake(){
        gridMover = GetComponent<GridMover>();
        rb = GetComponent<Rigidbody2D>();   // seek and arrive
        ApplyMode();
    }

    public void Move(InputAction.CallbackContext context){
        
        if (mode != MovementMode.WASD)     //seek and arrive
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
                   

        
        if (movementInput.y > 0)
        {
            gridMover.TryMove(Direction.Up);
        }
        else if (movementInput.y < 0)
        {
            gridMover.TryMove(Direction.Down);
        }
        else if (movementInput.x < 0)
        {
            gridMover.TryMove(Direction.Left);
        }
        else if (movementInput.x > 0)
        {
            gridMover.TryMove(Direction.Right);
        }
    }
    // new method
    private void OnDrawGizmos()
    {
        if (mode == MovementMode.SeekArrive && targetPos.HasValue)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(targetPos.Value, 0.2f);
        }
    }

}
