using UnityEngine;
using UnityEngine.InputSystem; 

public class PlayerMovement : MonoBehaviour
{

    
    private GridMover gridMover;
    private Vector2 movementInput;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
        
    }

    // Update is called once per frame
    void Update()
    {
       
    }
    void Awake(){
        gridMover = GetComponent<GridMover>();
    }

    public void Move(InputAction.CallbackContext context){
        
        
        if (!context.performed)
        {
            return;
        }

        Vector2 movementInput = context.ReadValue<Vector2>();

        if (movementInput == Vector2.zero)
        {
            return;
        }
                   

        
        if (movementInput.y > 0)
        {
            gridMover.TryMove(Direction.Up);
        }
        else if (movementInput.y < 0)
        {
            gridMover.TryMove(Direction.Down);
        }
        else if (movementInput.x < 0)
        {
            gridMover.TryMove(Direction.Left);
        }
        else if (movementInput.x > 0)
        {
            gridMover.TryMove(Direction.Right);
        }
    }

    


}
