using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]

public class PlatformerPlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 6f;

    [SerializeField] private float jumpForce = 12f; // no charge or hold to jump higher
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.15f;

    // Fixed distance jump (no air control)
    [SerializeField] private bool useFixedDistanceJump = false;
    [SerializeField] private float fixedJumpDistanceX = 2f;
    [SerializeField] private float fixedJumpHeight = 1.5f;
    [SerializeField] private float fixedJumpDuration = 0.35f;
    [SerializeField] private LayerMask jumpBlockLayer;
    [SerializeField] private Vector2 landingCheckSize = new Vector2(0.7f, 0.7f);

    [SerializeField] private float fallSpeed = 10f; // descent speed when jump off from higher level
    [SerializeField] private float maxFallCheckTime = 2f; // safety cap

    private bool isJumping;
    public bool UsesFixedDistanceJump => useFixedDistanceJump;

    private Rigidbody2D rb;
    private bool isGrounded;
    private float horizontalInput;

    public bool ExternallyControlled { get; set; } = false;

    public bool IsGrounded => isGrounded;
    public Vector2 Velocity => rb.linearVelocity;
    public Transform GroundCheckPoint => groundCheck;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Move(InputAction.CallbackContext context)
    {
       Vector2 input = context.ReadValue<Vector2>();
        horizontalInput = input.x;
    }

    public void Jump(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        TryJump();
    }

    private void FixedUpdate()
    {
        // Stand on an obstacle count as gournded
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer)
            || Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, jumpBlockLayer);

        if (!ExternallyControlled && !isJumping)
        { 
            MoveHorizontal(horizontalInput); 
        }

        if (!useFixedDistanceJump && rb.linearVelocity.y > jumpForce)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
    }

    // Full air control, no momentum penalty
    public void MoveHorizontal(float direction)
    {
        if (isJumping) return;

        rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);

        // Flip sprite to moving direction
        if (Mathf.Abs(direction) > 0.01f)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (direction > 0 ? 1 : -1);
            transform.localScale = scale;
        }
    }

    // Same force for each jump
    public bool TryJump()
    {
        if (!isGrounded || isJumping) return false;

        if (useFixedDistanceJump)
        {
            float facing = Mathf.Sign(transform.localScale.x);
            Vector2 landingSpot = (Vector2)transform.position + new Vector2(facing * fixedJumpDistanceX, 0f);

            StartCoroutine(FixedDistanceJumpRoutine(landingSpot));
        }
        else
        { 
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce); 
        }
        return true;
    }

    private System.Collections.IEnumerator FixedDistanceJumpRoutine(Vector2 landingSpot)
    {
        isJumping = true;
        rb.linearVelocity = Vector2.zero;
        Vector2 start = rb.position;
        Vector2 currentPos = start;
        float elapsed = 0f;

        while (elapsed < fixedJumpDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / fixedJumpDuration);
            Vector2 horizontalPos = Vector2.Lerp(start, landingSpot, t);
            float heightOffset = Mathf.Sin(t * Mathf.PI) * fixedJumpHeight;
            Vector2 nextPos = horizontalPos + new Vector2(0f, heightOffset);

            Vector2 delta = nextPos - currentPos;
            float distance = delta.magnitude;

            // Skip collision check during very start of arc
            bool takeoffWindow = t < 0.15f;

            if (!takeoffWindow && distance > 0f)
            {
                RaycastHit2D hit = Physics2D.BoxCast(currentPos, landingCheckSize, 0f, delta.normalized, distance, jumpBlockLayer | groundLayer);
                if (hit.collider != null)
                {
                    isJumping = false;
                    yield break;
                }
            }

            rb.MovePosition(nextPos);
            currentPos = nextPos;
            rb.linearVelocity = Vector2.zero; // gravity keeps accumulating into linearVelocity every physics step
            yield return new WaitForFixedUpdate();
        }

        // Jump off from higher level
        float facingAtLaunch = Mathf.Sign(landingSpot.x - start.x);
        if (facingAtLaunch == 0f) facingAtLaunch = 1f;
        float driftSpeed = fixedJumpDistanceX / fixedJumpDuration * 0.4f; 

        float fallCheckTime = 0f;

        while (fallCheckTime < maxFallCheckTime)
        {
            bool groundBelow = Physics2D.OverlapCircle(currentPos, groundCheckRadius, groundLayer)
                || Physics2D.OverlapCircle(currentPos, groundCheckRadius, jumpBlockLayer);
            if (groundBelow)
            {
                break;
            }

            Vector2 fallStep = new Vector2(facingAtLaunch * driftSpeed, -fallSpeed) * Time.fixedDeltaTime;

            float step = fallStep.magnitude;
            RaycastHit2D groundHit = Physics2D.BoxCast(currentPos, landingCheckSize, 0f, fallStep.normalized, step, groundLayer);
            RaycastHit2D obstacleHit = Physics2D.BoxCast(currentPos, landingCheckSize, 0f, fallStep.normalized, step, jumpBlockLayer);
            RaycastHit2D hit = groundHit.collider != null ? groundHit : obstacleHit;

            if (hit.collider != null)
            {
                currentPos = hit.point + Vector2.up * (landingCheckSize.y / 2f);
                rb.MovePosition(currentPos);
                break;
            }

            fallCheckTime += Time.fixedDeltaTime;
            currentPos += fallStep;
            rb.MovePosition(currentPos);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForFixedUpdate();
        }

        rb.linearVelocity = Vector2.zero;

        isJumping = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        { 
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        if (useFixedDistanceJump)
        {
            float facing = Application.isPlaying ? Mathf.Sign(transform.localScale.x) : 1f;
            Vector2 landingSpot = (Vector2)transform.position + new Vector2(facing * fixedJumpDistanceX, 0f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(landingSpot, landingCheckSize);
        }
    }
}
