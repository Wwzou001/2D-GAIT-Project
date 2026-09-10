using UnityEngine;

public class FlyingEnemy : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float wanderRadius = 2.5f; // distance from starting position to wander
    [SerializeField] private float pickNewTargetInterval = 1f; // frequence it pick a new random spot
    [SerializeField] private float arrivalThreshold = 0.2f; // how close count as "reached target"

    // Randomised speed/radius/interval
    [SerializeField, Range(0f, 0.5f)] private float speedVariation = 0.2f;
    [SerializeField, Range(0f, 0.5f)] private float radiusVariation = 0.2f;
    [SerializeField, Range(0f, 0.5f)] private float intervalVariation = 0.3f;

    // Prevent randomly pick target position below ground level
    [SerializeField] private float minHeightAboveStart = -0.5f; // distant limit for flying enemy below starting Y

    private float actualMoveSpeed;
    private float actualWanderRadius;
    private float actualPickInterval;

    private Vector2 startPosition;
    private Vector2 currentTarget;
    private float timer;
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f; // no gravity affect to flying enemy
        startPosition = transform.position;

        // Roll instance actual values once, differ from other use same prefab
        actualMoveSpeed = moveSpeed * Random.Range(1f - speedVariation, 1f + speedVariation);
        actualWanderRadius = wanderRadius * Random.Range(1f - radiusVariation, 1f + radiusVariation);
        actualPickInterval = pickNewTargetInterval * Random.Range(1f - intervalVariation, 1f + intervalVariation);

        PickNewTarget();
    }

    private void FixedUpdate()
    {
        timer += Time.fixedDeltaTime;

        float distanceToTarget = Vector2.Distance(transform.position, currentTarget);
        if (timer >= actualPickInterval || distanceToTarget <= arrivalThreshold)
        {
            PickNewTarget();
            timer = 0f;
        }

        Vector2 direction = (currentTarget - (Vector2)transform.position).normalized;
        rb.linearVelocity = direction * actualMoveSpeed;

        // Flip sprite to face movement direction
        if (Mathf.Abs(direction.x) > 0.01f)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (direction.x > 0 ? 1 : -1);
            transform.localScale = scale;
        }
    }

    private void PickNewTarget()
    {
        // Random point with circle around the starting position
        Vector2 randomOffset = Random.insideUnitCircle * actualWanderRadius;
        Vector2 candidate = startPosition + randomOffset;

        // Limit flying enemy never move below allowed minimum height
        float minY = startPosition.y + minHeightAboveStart;
        if (candidate.y < minY)
        {
            candidate.y = minY;
        }

        currentTarget = candidate;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (PlatformerGameManager.Instance != null && PlatformerGameManager.Instance.LevelOver) return;

        if (!other.CompareTag("Player")) return;

        if (PlatformerGameManager.Instance != null)
        {
            PlatformerGameManager.Instance.Lose("hit by a flying enemy");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? (Vector3)startPosition : transform.position;
        float radiusToShow = Application.isPlaying ? actualWanderRadius : wanderRadius;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, radiusToShow);

        // Show the minimum height line
        Gizmos.color = Color.yellow;
        float minY = center.y + minHeightAboveStart;
        Gizmos.DrawLine(new Vector3(center.x - radiusToShow, minY, 0), new Vector3(center.x + radiusToShow, minY, 0));
    }
}
