using UnityEngine;

public enum Direction { Up, Down, Left, Right }

public class GridMover : MonoBehaviour
{
    public Vector2Int GridPosition { get; private set; }

    [SerializeField] private Vector2Int startPosition = Vector2Int.zero;

    [SerializeField] private bool canCollectCoins = true;
    [SerializeField] private bool canCollectKeys = true; 
    [SerializeField] private bool isPlayer = false; 

// new obstacle 
    [SerializeField] private float slowCooldownDuration = 1f;
    private float moveCooldownUntil = 0f;

    private bool IsOnCooldown => Time.time < moveCooldownUntil;

    void Start()
    {
        GridPosition = startPosition;
        transform.position = GridSystem.Instance.GridToWorld(GridPosition);
    }

    public bool TryMove(Direction dir)
    {
        //slowing down player
        if (IsOnCooldown)
            return false;

        // when game over, nobody should able to move
        if (GameManager.Instance != null && GameManager.Instance.GameOver)
        {
            return false;
        }

        Vector2Int targetPos = GridPosition + DirectionToOffset(dir);

        //player is trying to move into the door tile
        if (targetPos == GridSystem.Instance.TopDoorGridPosition)
        {
            //only players can enter
            if (!isPlayer)
                return false;

            //player needs the key
            if (GameManager.Instance != null &&
                !GameManager.Instance.HasKey)
            {
                Debug.Log("Door is locked. You need to find the key first.");
                return false;
            }
        }

        if (targetPos == GridSystem.Instance.BottomDoorGridPosition){
            
            //only players can enter
            if (!isPlayer)
                return false;

            // Can't go backwards from room 0
            if (RoomManager.Instance == null ||
                RoomManager.Instance.CurrentRoom <= 0)
            {
                return false;
            }
        }
        

        bool isDoor = targetPos == GridSystem.Instance.TopDoorGridPosition || targetPos == GridSystem.Instance.BottomDoorGridPosition;

        if (!GridSystem.Instance.IsInBounds(targetPos) && !isDoor)
            return false; // blocked: edge of the grid

        if (GridSystem.Instance.IsObstacle(targetPos))
            return false; // blocked: obstacle in the way

        GridPosition = targetPos;
        transform.position = GridSystem.Instance.GridToWorld(GridPosition);

        //slwoing down player
        if (GridSystem.Instance.IsSlow(targetPos))
        {
            moveCooldownUntil = Time.time + slowCooldownDuration;
        }

        OnEnterCell(targetPos);

        //moving to the next room
        if (isPlayer && targetPos == GridSystem.Instance.TopDoorGridPosition)
        {
            RoomManager.Instance.EnterNextRoom();
            return true;
        }

        //moving to the previous room
        if (isPlayer && targetPos == GridSystem.Instance.BottomDoorGridPosition)
        {
            RoomManager.Instance.EnterPreviousRoom();
            return true;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.CheckGameState();
        }

        return true;
    }

    private void OnEnterCell(Vector2Int pos)
    {
        if (canCollectCoins && GridSystem.Instance.IsCoin(pos))
        {
            GridSystem.Instance.CollectCoin(pos);
        }
        if (canCollectKeys && GridSystem.Instance.IsKey(pos))        
        {
            GridSystem.Instance.CollectKey(pos);    
        }
    }

    private Vector2Int DirectionToOffset(Direction dir)
    {
        switch (dir)
        {
            case Direction.Up: return new Vector2Int(0, 1);
            case Direction.Down: return new Vector2Int(0, -1);
            case Direction.Left: return new Vector2Int(-1, 0);
            case Direction.Right: return new Vector2Int(1, 0);
            default: return Vector2Int.zero;
        }
    }


    //used to set the players position when they enter a new room
    public void SetGridPosition(Vector2Int position)
    {
        GridPosition = position;
        transform.position = GridSystem.Instance.GridToWorld(position);
    }
}