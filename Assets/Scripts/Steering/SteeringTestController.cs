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

    private AvoidanceSettings avoidance = new AvoidanceSettings { Enabled = false };

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        targetPos = transform.position; //nothing moves until clicked
    }

    private void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector3 screenPos = Mouse.current.position.ReadValue();
            targetPos = Camera.main.ScreenToWorldPoint(screenPos);
        }
    }

    private void FixedUpdate()
    {
        Vector2 currentPos = rb.position;
        Vector2 desiredVelocity;

        switch (behaviour)
        {
            case BehaviourType.Seek:
                desiredVelocity = Steering.Seek(currentPos, targetPos, maxSpeed, avoidance);
                break;

            case BehaviourType.Arrive:
                desiredVelocity = Steering.Arrive(currentPos, targetPos, arriveSlowRadius, maxSpeed, avoidance);
                break;

            case BehaviourType.Flee:
                desiredVelocity = Steering.Flee(currentPos, targetPos, maxSpeed, avoidance);
                break;

            default:
                desiredVelocity = Vector2.zero;
                break;
        }

        Vector2 force = Steering.VelocityToForce(desiredVelocity, rb, accelTime, maxAccel);
        rb.AddForce(force);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(targetPos, 0.2f);
    }
}
