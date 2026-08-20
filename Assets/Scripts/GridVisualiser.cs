using UnityEngine;
using System.Collections.Generic;

public class GridVisulizer : MonoBehaviour
{
    [SerializeField] private Sprite floorSprite;
    [SerializeField] private Sprite obstacleSprite;
    [SerializeField] private Sprite coinSprite;

    [SerializeField] private Color floorColor = new Color(0.85f, 0.85f, 0.85f);
    [SerializeField] private Color obstacleColor = new Color(0.35f, 0.35f, 0.35f);
    [SerializeField] private Color coinColor = Color.yellow;

    [SerializeField] private int floorSortingOrder = 0;
    [SerializeField] private int obstacleSortingOrder = 1;
    [SerializeField] private int coinSortingOrder = 1;

    private readonly Dictionary<Vector2Int, GameObject> coinObjects = new Dictionary<Vector2Int, GameObject>();

    void Start()
    {
        if (GridSystem.Instance == null)
        {
            Debug.LogError("GridVisualiser: no GridSystem found in the scene." + "Add a GridSystem object before");
            return;
        }
        BuildGridVisuals();

        GridSystem.Instance.CoinCollected += HandleCoinCollected;
    }

    private void OnDestroy()
    {
        if (GridSystem.Instance != null)
        {
            GridSystem.Instance.CoinCollected -= HandleCoinCollected;
        }
    }

    void HandleCoinCollected(Vector2Int pos)
    {
        if (coinObjects.TryGetValue(pos, out GameObject coinObj))
        {
            Destroy(coinObj);
            coinObjects.Remove(pos);
        }
    }

    void BuildGridVisuals()
    {
        for (int x = 0; x < GridSystem.Width; x++)
        {
            for (int y = 0; y < GridSystem.Height; y++)
            {
                Vector2Int cellPos = new Vector2Int(x, y);
                Vector3 worldPos = GridSystem.Instance.GridToWorld(cellPos);

                // every cell get a tile first
                SpawnSprite(floorSprite, floorColor, worldPos, floorSortingOrder, $"Floor_{x}_{y}");

                // layer an obstacle or coin on top
                if (GridSystem.Instance.IsObstacle(cellPos))
                {
                    SpawnSprite(obstacleSprite, obstacleColor, worldPos, obstacleSortingOrder, $"Obstacle_{x}_{y}");
                }
                else if (GridSystem.Instance.IsCoin(cellPos))
                {
                    GameObject coinObj = SpawnSprite(coinSprite, coinColor, worldPos, coinSortingOrder, $"Coin_{x}_{y}");
                    if ( coinObj != null )
                    {
                        coinObjects[cellPos] = coinObj;
                    }
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
