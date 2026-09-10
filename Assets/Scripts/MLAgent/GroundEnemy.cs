using UnityEngine;

public class GroundEnemy : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;

    [SerializeField] private float patrolDistance = 5f;

    [SerializeField] private bool useObstacleDetection = true;
    [SerializeField] private Transform obstacleCheck;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float obstacleCheckDistance = 0.3f;

    [SerializeField] private bool useGroundEdgeDetection = false; // turn around at 1 edge
    [SerializeField] private Transform groundEdgeCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float edgeCheckDistance = 0.5f;

    private Vector2 startPosition;
    private int direction = 1; // 1 = right, -1 = left
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        startPosition = transform.position;
    }


    private void FixedUpdate()
    {
        float distanceFromStart = transform.position.x - startPosition.x;

        // Turn around if reach patrol range limit
        if (direction > 0 && distanceFromStart >= patrolDistance)
        {
            direction = -1;
        }
        else if (direction <0 && distanceFromStart <= -patrolDistance)
        {
            direction = 1;
        }

        // Turn around if obstacle directly ahead
        if (useObstacleDetection && obstacleCheck != null)
        {
            Vector2 castDirection = direction > 0 ? Vector2.right : Vector2.left;
            bool obstacleAhead = Physics2D.Raycast(obstacleCheck.position, castDirection, obstacleCheckDistance, obstacleLayer);
            if (obstacleAhead)
            {
                direction *= -1;
            }
        }

        // Turn around if about to walk off a ledge
        if (useGroundEdgeDetection && groundEdgeCheck != null)
        {
            bool groundAhead = Physics2D.Raycast(groundEdgeCheck.position, Vector2.down, edgeCheckDistance, groundLayer);
            if (!groundAhead)
            {
                direction *= -1;
            }
        }

        rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);

        // Flip the sprite to face the direction of movement
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (direction > 0 ? 1: -1);
        transform.localScale = scale;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (PlatformerGameManager.Instance != null && PlatformerGameManager.Instance.LevelOver) return;

        if (!other.CompareTag("Player")) return;

        if (PlatformerGameManager.Instance != null)
        {
            PlatformerGameManager.Instance.Lose("hit by an enemy");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? (Vector3)startPosition : transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(center + Vector3.left * patrolDistance, center + Vector3.right * patrolDistance);

        if (obstacleCheck != null)
        {
            Gizmos.color = Color.yellow;
            Vector2 castDirection = direction > 0 ? Vector2.right : Vector2.left;
            Gizmos.DrawLine(obstacleCheck.position, obstacleCheck.position + (Vector3)(castDirection * obstacleCheckDistance));
        }
    }
}
