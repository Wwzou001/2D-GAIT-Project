using UnityEngine;
using UnityEngine.InputSystem;

public class MCTSPlayerMovement : MonoBehaviour
{
    private MCTSGridMover gridMover;

    // How many move can make before this end turn
    private int movesRemainingThisTurn = 0;

    private void Awake()
    {
        gridMover = GetComponent<MCTSGridMover>();
    }

    public void Move(InputAction.CallbackContext context)
    {
        if (!context.performed)
        {
            return;
        }


        // Not player turn yet, ignore player input
        if (MCTSGameManager.Instance != null && !MCTSGameManager.Instance.IsPlayerTurn)
        {
            return;
        }

        if (movesRemainingThisTurn <= 0)
        {
            movesRemainingThisTurn = gridMover.MoveDistance;
        }

        Vector2 movementInput = context.ReadValue<Vector2>();

        if (movementInput == Vector2.zero)
        {
            return;
        }

        Direction? direction = null;

        if (movementInput.y > 0)
        { 
            direction = Direction.Up;
        }
        else if (movementInput.y < 0)
        {
            direction = Direction.Down;
        }
        else if (movementInput.x > 0)
        {
            direction = Direction.Right;
        }
        else if (movementInput.x < 0)
        {
            direction = Direction.Left;
        }

        if (direction == null)
        {
            return;
        }

        bool moved = gridMover.TryMove(direction.Value);

        // Only end turn if player acturally move, bumping into obstacle not cost player movement
        if (moved)
        {
            movesRemainingThisTurn--;

            if (movesRemainingThisTurn <= 0 && MCTSGameManager.Instance != null)
            {
                MCTSGameManager.Instance.EndPlayerTurn();
            }
        }
    }
}
