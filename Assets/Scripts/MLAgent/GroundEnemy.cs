using UnityEngine;

public class GroundEnemy : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;

    [SerializeField] private float patrolDistance = 5f;

    [SerializeField] private bool useObstacleDetection = true;
    [SerializeField] private Transform obstacleCheck;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float obstacleCheckDistance = 0.3f;

    // Enemy to enemy collision
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private float turnCooldown = 0.3f;

    [SerializeField] private bool useGroundEdgeDetection = false; // turn around at 1 edge
    [SerializeField] private Transform groundEdgeCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float edgeCheckDistance = 0.5f;

    [SerializeField, Range(0f, 0.5f)] private float distanceVariation = 0.3f;
    [SerializeField, Range(0f, 0.5f)] private float speedVariation = 0.2f;
    [SerializeField] private bool randomiseStartDirection = true;

    private float actualPatrolDistance;
    private float actualMoveSpeed;

    private float lastTurnTime = -999f;
    private Vector2 startPosition;
    private int direction = 1; // 1 = right, -1 = left
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        startPosition = transform.position;

        actualPatrolDistance = patrolDistance * Random.Range(1f - distanceVariation, 1f + distanceVariation);
        actualMoveSpeed = moveSpeed * Random.Range(1f - speedVariation, 1f + speedVariation);
        
        if (randomiseStartDirection)
        {
            direction = Random.value < 0.5f ? -1 : 1;
        }
    }


    private void FixedUpdate()
    {
        float distanceFromStart = transform.position.x - startPosition.x;

        // Turn around if reach patrol range limit
        if (direction > 0 && distanceFromStart >= actualPatrolDistance)
        {
            direction = -1;
        }
        else if (direction <0 && distanceFromStart <= -actualPatrolDistance)
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

        rb.linearVelocity = new Vector2(direction * actualMoveSpeed, rb.linearVelocity.y);

        // Flip the sprite to face the direction of movement
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (direction > 0 ? 1: -1);
        transform.localScale = scale;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Bumped into another enemy - turn around, only if not just turned
        if (other.CompareTag(enemyTag))
        {
            if (Time.time - lastTurnTime > turnCooldown)
            {
                direction *= -1;
                lastTurnTime = Time.time;
            }
            return;
        }

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
        float rangeToShow = Application.isPlaying ? actualPatrolDistance : patrolDistance;
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
