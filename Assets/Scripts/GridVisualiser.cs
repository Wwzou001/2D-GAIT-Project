using UnityEngine;
using System.Collections.Generic;

public class GridVisulizer : MonoBehaviour
{   
    [SerializeField] private GridSystem gridSystem;
    [SerializeField] private Sprite floorSprite;
    [SerializeField] private Sprite obstacleSprite;
    [SerializeField] private Sprite coinSprite;

    [SerializeField] private Sprite fountainSprite;

    [SerializeField] private Color floorColor = new Color(0.85f, 0.85f, 0.85f);
    [SerializeField] private Color obstacleColor = new Color(0.35f, 0.35f, 0.35f);
    [SerializeField] private Color coinColor = Color.yellow;
    [SerializeField] private Color fountainColor = new Color(0.3f, 0.6f, 1f);

    [SerializeField] private int floorSortingOrder = 0;
    [SerializeField] private int obstacleSortingOrder = 1;
    [SerializeField] private int coinSortingOrder = 1;
    [SerializeField] private int fountainSortingOrder = 1;

    // new obstacle
    [SerializeField] private Sprite slowSprite;
    [SerializeField] private Color slowColor = Color.white;
    [SerializeField] private int slowSortingOrder = 1;

// key 
    [SerializeField] private Sprite keySprite;
    [SerializeField] private Color keyColor = Color.white;
    [SerializeField] private int keySortingOrder = 1;

private Dictionary<Vector2Int, GameObject> activeKeySprites = new Dictionary<Vector2Int, GameObject>();

    private readonly Dictionary<Vector2Int, GameObject> coinObjects = new Dictionary<Vector2Int, GameObject>();

    //For the multi room scenes, grid visualiser needs to know what gridsystem its paired with, if null then just assume we are using an old scene with only 1 room
    private void Awake()
    {
        //first try to find a GridSystem on this object
        if (gridSystem == null)
        {
            gridSystem = GetComponent<GridSystem>();
        }

        //then try to find one on the parent room
        if (gridSystem == null)
        {
            gridSystem = GetComponentInParent<GridSystem>();
        }

        //legacy fallback for one room scenes
        if (gridSystem == null)
        {
            gridSystem = GridSystem.Instance;
        }
    }

    void Start()
    {
        if (GridSystem.Instance == null)
        {
            Debug.LogError("GridVisualiser: no GridSystem found in the scene." + "Add a GridSystem object before");
            return;
        }
        BuildGridVisuals();
        //SpawnAllKeys();

        GridSystem.Instance.CoinCollected += HandleCoinCollected;
        GridSystem.Instance.KeyCollected += HandleKeyCollected;  
        GridSystem.Instance.KeySpawned += HandleKeySpawned; 
    }

    private void OnDestroy()
    {
        if (GridSystem.Instance != null)
        {
            GridSystem.Instance.CoinCollected -= HandleCoinCollected;
            GridSystem.Instance.KeyCollected -= HandleKeyCollected;
            GridSystem.Instance.KeySpawned -= HandleKeySpawned;
        }
    }


    private void HandleKeySpawned(Vector2Int pos)
    {
        if (keySprite == null)
        {
            Debug.LogWarning("CoinVisualizer: no key sprite assigned in the Inspector.");
            return;
        }
        SpawnKeySprite(pos);
    }

//  // key spawn and collect
//     private void SpawnAllKeys()
//     {
//         if (keySprite == null)
//         {
//             Debug.LogWarning("CoinVisualizer: no key sprite assigned in the Inspector.");
//             return;
//         }

//         int width = GridSystem.Instance.Width;
//         int height = GridSystem.Instance.Height;

//         for (int x = 0; x < width; x++)
//         {
//             for (int y = 0; y < height; y++)
//             {
//                 Vector2Int pos = new Vector2Int(x, y);
//                 if (GridSystem.Instance.IsKey(pos))
//                 {
//                     SpawnKeySprite(pos);
//                 }
//             }
//         }
//     }

    private void SpawnKeySprite(Vector2Int gridPos)
    {
        Vector3 worldPos = GridSystem.Instance.GridToWorld(gridPos);

        GameObject go = new GameObject($"Key_{gridPos.x}_{gridPos.y}");
        go.transform.SetParent(transform);
        go.transform.position = worldPos;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = keySprite;
        sr.color = keyColor;
        sr.sortingOrder = keySortingOrder;

        activeKeySprites[gridPos] = go;
    }

    void HandleCoinCollected(Vector2Int pos)
    {
        if (coinObjects.TryGetValue(pos, out GameObject coinObj))
        {
            Destroy(coinObj);
            coinObjects.Remove(pos);
        }
    }

    void HandleKeyCollected(Vector2Int pos)
    {
        if (activeKeySprites.TryGetValue(pos, out GameObject keyObj))
        {
            Destroy(keyObj);
            activeKeySprites.Remove(pos);
        }
    }

    void BuildGridVisuals()
    {
        for (int x = 0; x < GridSystem.Instance.Width; x++)
        {
            for (int y = 0; y < GridSystem.Instance.Height; y++)
            {
                Vector2Int cellPos = new Vector2Int(x, y);
                Vector3 worldPos = GridSystem.Instance.GridToWorld(cellPos);

                // every cell get a tile first
                SpawnSprite(floorSprite, floorColor, worldPos, floorSortingOrder, $"Floor_{x}_{y}");

                // layer an obstacle, coin or fountain on top
                if (GridSystem.Instance.IsFountain(cellPos))
                {
                    // Fall back to coin if no fountain sprite assigned
                    Sprite spriteToUse = fountainSprite != null ? fountainSprite : coinSprite;
                    SpawnSprite(spriteToUse, fountainColor, worldPos, fountainSortingOrder, $"Fountain_{x}_{y}");
                }
                    
                else if (GridSystem.Instance.IsObstacle(cellPos))
                {
                    SpawnSprite(obstacleSprite, obstacleColor, worldPos, obstacleSortingOrder, $"Obstacle_{x}_{y}");
                }
                    
                else if (GridSystem.Instance.IsCoin(cellPos))
                {
                    GameObject coinObj = SpawnSprite(coinSprite, coinColor, worldPos, coinSortingOrder, $"Coin_{x}_{y}");
                    if (coinObj != null)
                    {
                        coinObjects[cellPos] = coinObj;
                    }
                }
                // new obstacle
                else if (GridSystem.Instance.IsSlow(cellPos))  // add this
                {
                    SpawnSprite(slowSprite, slowColor, worldPos, slowSortingOrder, $"Slow_{x}_{y}");
                }
            }
        }
    }

    GameObject SpawnSprite(Sprite sprite, Color color, Vector3 worldPos, int sortingOrder, string name)
    {
        if (sprite == null)
        {
            Debug.LogWarning($"GridVisualiser: no sprite assigned for {name}" + "Assign Floor/Obstacle/Coin sprites in the Inspector");
            return null;
        }

        GameObject go = new GameObject(name);
        go.transform.SetParent(transform);
        go.transform.position = worldPos;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;

        return go;
    }
}
