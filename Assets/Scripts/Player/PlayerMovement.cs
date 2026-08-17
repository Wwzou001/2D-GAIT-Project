using UnityEngine;
using UnityEngine.InputSystem; 

public class PlayerMovement : MonoBehaviour
{

    public float moveSpeed = 4f;
    private Rigidbody2D rb;
    private Vector2 movementInput;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
        rb = GetComponent<Rigidbody2D>(); 
    }

    // Update is called once per frame
    void Update()
    {
        rb.linearVelocity = movementInput * moveSpeed;
    }

    public void Move(InputAction.CallbackContext context){
        
        movementInput = context.ReadValue<Vector2>();
    }

    


}
