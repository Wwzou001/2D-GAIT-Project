using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class CrossMoveFlyingEnemy : MonoBehaviour
{
    public enum Axis { Vertical, Horizontal };

    // Movement
    [SerializeField] private Axis moveAxis = Axis.Vertical;
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float travelDistance = 2f; // how far it travels from the starting position, each direction

    [SerializeField] private bool startDirectionPositive = true; // true = starts moving up/right, false = starts moveing down/left

    // Per-instance variation
    [SerializeField, Range(0f, 0.5f)] private float speedVariation = 0.15f;

    private float rangeMin;
    private float rangeMax;

    private float actualSpeed;
    private Vector2 startPosition;
    private int direction = 1; // 1 = up/right, -1 = down/left
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f; // flying enemies not affected by gravity
        startPosition = transform.position;
        actualSpeed = moveSpeed * Random.Range(1f - speedVariation, 1f + speedVariation);

        if (startDirectionPositive)
        {
            rangeMin = 0f;
            rangeMax = travelDistance;
            direction = 1;
        }
        else
        {
            rangeMin = -travelDistance;
            rangeMax = 0f;
            direction = -1;
        }
    }

    private void FixedUpdate()
    {
        Vector2 currentPos = rb.position;
        Vector2 moveVector = moveAxis == Axis.Vertical ? Vector2.up : Vector2.right;

        float distanceFromStart = Vector2.Dot(currentPos - startPosition, moveVector);

        if (direction > 0 && distanceFromStart >= rangeMax)
        {
            direction = -1;
        }
        else if (direction < 0 && distanceFromStart <= rangeMin)
        {
            direction = 1;
        }

        rb.linearVelocity = moveVector * (direction * actualSpeed);

        // Flip sprite to face movement direction on the horizontal axis
        if (moveAxis == Axis.Horizontal)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (direction > 0 ? 1 : -1);
            transform.localScale = scale;
        }
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
        Vector3 moveVector = moveAxis == Axis.Vertical ? Vector3.up : Vector3.right;

        float min = Application.isPlaying ? rangeMin : (startDirectionPositive ? 0f : -travelDistance);
        float max = Application.isPlaying ? rangeMax : (startDirectionPositive ? travelDistance : 0f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(center + moveVector * min, center + moveVector * max);
        Gizmos.DrawWireSphere(center + moveVector * min, 0.1f);
        Gizmos.DrawWireSphere(center + moveVector * max, 0.1f);
    }
}
