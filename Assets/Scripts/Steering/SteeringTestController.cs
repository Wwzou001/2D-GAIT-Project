using UnityEngine;
using UnityEngine.InputSystem;
using SteeringBehaviours;

// TEST-ONLY script for trying out Seek/Arrive/Flee in isolation.
public class SteeringTestController : MonoBehaviour
{
    public enum BehaviourType { Seek, Arrive, Flee }
 
    public BehaviourType behaviour = BehaviourType.Seek;
 
    public float maxSpeed = 5f;
    public float maxAccel = 10f;
    public float accelTime = 0.25f;
    public float arriveSlowRadius = 2f;
 
    private Rigidbody2D rb;
    private Vector2 targetPos;
 
    [Header("Seek Demo Polish (visual only, doesn't change real Seek math)")]
    [SerializeField] private float stopDistance = 0.15f;
    private bool hasOvershotTarget = false;
    private bool isFrozenAfterOvershoot = false;
    private float previousDistanceToTarget = float.MaxValue;
 

    private AvoidanceSettings avoidance = new AvoidanceSettings { Enabled = false };
 
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        targetPos = transform.position; // start with target at self, so nothing moves until clicked
    }
 
    private void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector3 screenPos = Mouse.current.position.ReadValue();
            targetPos = Camera.main.ScreenToWorldPoint(screenPos);
 
            // new target picked, reset the demo-polish tracking
            hasOvershotTarget = false;
            isFrozenAfterOvershoot = false;
            previousDistanceToTarget = float.MaxValue;
        }
    }
 
    private void FixedUpdate()
    {
        if (behaviour == BehaviourType.Seek && isFrozenAfterOvershoot)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }
 
        Vector2 currentPos = rb.position;
        Vector2 desiredVelocity;
 
        switch (behaviour)
        {
            case BehaviourType.Seek:
                desiredVelocity = Steering.Seek(currentPos, targetPos, maxSpeed, avoidance);
                TrackSeekOvershoot(currentPos);
                break;
 
            case BehaviourType.Arrive:
                desiredVelocity = Steering.Arrive(currentPos, targetPos, arriveSlowRadius, maxSpeed, avoidance);
                break;
 
            case BehaviourType.Flee:
                // for Flee, "targetPos" acts as the threat to run away from
                desiredVelocity = Steering.Flee(currentPos, targetPos, maxSpeed, avoidance);
                break;
 
            default:
                desiredVelocity = Vector2.zero;
                break;
        }
 
        Vector2 force = Steering.VelocityToForce(desiredVelocity, rb, accelTime, maxAccel);
        rb.AddForce(force);
    }
 
    private void TrackSeekOvershoot(Vector2 currentPos)
    {
        float distance = Vector2.Distance(currentPos, targetPos);
 
        if (!hasOvershotTarget)
        {
            if (distance > previousDistanceToTarget)
            {
                hasOvershotTarget = true;
            }
        }
        else
        {
            // already overshot once, now check if it's swung back close enough to freeze
            if (distance <= stopDistance)
            {
                isFrozenAfterOvershoot = true;
            }
        }
 
        previousDistanceToTarget = distance;
    }
 
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(targetPos, 0.2f);
    }
}