using UnityEngine;
using UnityEngine.InputSystem;
using SteeringBehaviours;

// Lets the Player switch between two movement modes:
// - WASD: the existing grid-based movement (PlayerMovement + GridMover).
// - SeekArrive: click somewhere, and the player moves there continuously
//   using Seek or Arrive.

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovementModeController : MonoBehaviour
{
    public enum MovementMode { WASD, SeekArrive }
    public enum ChaseBehaviour { Seek, Arrive }

    public MovementMode mode = MovementMode.WASD;
    public Key toggleKey = Key.Tab;

    public ChaseBehaviour behaviour = ChaseBehaviour.Arrive;
    public float maxSpeed = 5f;
    public float maxAccel = 10f;
    public float accelTime = 0.25f;
    public float arriveSlowRadius = 2f;

    public AvoidanceSettings avoidance = new AvoidanceSettings
    {
        Enabled = false,
        LookAheadDistance = 2f,
        CheckRadius = 0.4f,
        AngleStep = 10f
    };

    private Rigidbody2D rb;
    private PlayerMovement playerMovement;
    private Vector2 targetPos;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<PlayerMovement>();
        targetPos = transform.position;

        ApplyMode();
    }

    private void Update()
    {
        // toggle mode
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            mode = mode == MovementMode.WASD ? MovementMode.SeekArrive : MovementMode.WASD;
            ApplyMode();
        }

        // in SeekArrive mode, left-click sets a new target
        if (mode == MovementMode.SeekArrive && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector3 screenPos = Mouse.current.position.ReadValue();
            targetPos = Camera.main.ScreenToWorldPoint(screenPos);
        }
    }

    private void ApplyMode()
    {
        if (mode == MovementMode.WASD)
        {
            if (playerMovement != null)
                playerMovement.enabled = true;

            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
        else // SeekArrive
        {
            if (playerMovement != null)
                playerMovement.enabled = false;

            rb.bodyType = RigidbodyType2D.Dynamic;
            targetPos = rb.position; // don't immediately dash off to an old target
        }
    }

    private void FixedUpdate()
    {
        if (mode != MovementMode.SeekArrive)
            return;

        Vector2 currentPos = rb.position;

        Vector2 desiredVelocity = behaviour == ChaseBehaviour.Seek
            ? Steering.Seek(currentPos, targetPos, maxSpeed, avoidance)
            : Steering.Arrive(currentPos, targetPos, arriveSlowRadius, maxSpeed, avoidance);

        Vector2 force = Steering.VelocityToForce(desiredVelocity, rb, accelTime, maxAccel);
        rb.AddForce(force);
    }

    private void OnDrawGizmos()
    {
        if (mode == MovementMode.SeekArrive)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(targetPos, 0.2f);
        }
    }
}
