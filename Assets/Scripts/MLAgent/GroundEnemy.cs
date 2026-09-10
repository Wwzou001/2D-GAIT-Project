using UnityEngine;

public class GroundEnemy : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;

    [SerializeField] private float patrolDistance = 5f;
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


    private void FIxedUpdate()
    {
        
    }
}
