using UnityEngine;

public class MCTSGridMover : MonoBehaviour
{
    public Vector2Int GridPosition { get; private set; }

    public enum StartCorner { playerStart, npcStart}
    [SerializeField] private StartCorner startCorner = StartCorner.playerStart;

    [SerializeField] private bool canCollectCoins = true;

    [SerializeField] private bool canUseFountainBuff = true; // only player have this on
    [SerializeField] private int buffDurationTurns = 3;
    private int buffTurnsRemaining = 0;

    // Normal 1 cell per turn, become 2 cell per turn when buff is active
    public int MoveDistance => buffTurnsRemaining > 0 ? 2 : 1;
    public bool HasFountainBuff => buffTurnsRemaining > 0;

    [SerializeField] private bool showShadow = true;
    [SerializeField] private Sprite shadowSprite;
    [SerializeField] private Color shadowColor = new Color(1f, 1f, 1f, 0.35f); // semi transparent
    [SerializeField] private int shadowSortingOrder = 1; // below the character

    private GameObject shadowObject;
    private SpriteRenderer shadowRenderer;

    void Start()
    {
        GridPosition = startCorner == StartCorner.playerStart ? GridSystem.Instance.playerStart : GridSystem.Instance.npcStart;
        transform.position = GridSystem.Instance.GridToWorld(GridPosition);

        if (showShadow)
        {
            SetUpShadow();
        }
    }

    void SetUpShadow()
    {
        shadowObject = new GameObject(gameObject.name + "_Shadow");
        shadowObject.transform.SetParent(transform.parent);

        shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();

        Sprite spriteToUse = shadowSprite;
        if (spriteToUse == null )
        {
            SpriteRenderer ownRenderer = GetComponent<SpriteRenderer>();
            if ( ownRenderer != null )
            {
                spriteToUse = ownRenderer.sprite;
            }
        }
        shadowRenderer.sprite = spriteToUse;
        shadowRenderer.color = shadowColor;
        shadowRenderer.sortingOrder = shadowSortingOrder;

        shadowObject.SetActive(false); // nothing to show before first move
    }

    public bool TryMove(Direction dir)
    {
        // when game over, nobody should able to move
        if (MCTSGameManager.Instance != null && MCTSGameManager.Instance.GameOver)
        {
            return false;
        }

        Vector2Int targetPos = GridPosition + DirectionToOffset(dir);

        if (!GridSystem.Instance.IsInBounds(targetPos))
            return false; 

        if (GridSystem.Instance.IsObstacle(targetPos))
            return false; 

        Vector2Int previousPos = GridPosition; // remeber for shadow

        GridPosition = targetPos;
        transform.position = GridSystem.Instance.GridToWorld(GridPosition);

        if (showShadow && shadowObject != null)
        {
            shadowObject.transform.position = GridSystem.Instance.GridToWorld(previousPos);
            shadowObject.SetActive(true);
        }

        OnEnterCell(targetPos);

        // Refresh buff turn near a fountain otherwise count down to expiry
        if (canUseFountainBuff)
        {
            if (GridSystem.Instance.IsNearFountain(GridPosition))
            {
                buffTurnsRemaining = buffDurationTurns;
            }
            else if (buffTurnsRemaining > 0)
            {
                buffTurnsRemaining--;
            }
        }

        if (MCTSGameManager.Instance != null)
        {
            MCTSGameManager.Instance.CheckGameStatus();
        }

        return true;
    }

    private void OnEnterCell(Vector2Int pos)
    {
        if (canCollectCoins && GridSystem.Instance.IsCoin(pos))
        {
            GridSystem.Instance.CollectCoin(pos);
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
}