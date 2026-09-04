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

    private Rigidbody2D rb;
    private bool isGrounded;
    private float horizontalInput;

    public bool IsGrounded => isGrounded;
    public Vector2 Velocity => rb.linearVelocity;

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
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        MoveHorizontal(horizontalInput);
    }

    // Full air control, no momentum penalty
    public void MoveHorizontal(float direction)
    {
        rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
    }

    // Same force for each jump
    public void TryJump()
    {
        if (!isGrounded) return;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
