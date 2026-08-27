using UnityEngine;

// Spawns a ring of wall sprites around the outside of the grid, purely for
// visual framing 
public class BorderRenderer : MonoBehaviour
{
    [SerializeField] private Sprite wallSprite;
    [SerializeField] private Color wallColor = Color.white;
    [SerializeField] private int sortingOrder = 1;

    private void Start()
    {
        if (GridSystem.Instance == null)
        {
            Debug.LogWarning("BorderRenderer: no GridSystem found in the scene.");
            return;
        }

        if (wallSprite == null)
        {
            Debug.LogWarning("BorderRenderer: no wall sprite assigned in the Inspector.");
            return;
        }

        BuildBorder();
    }

    private void BuildBorder()
    {
        int width = GridSystem.Instance.Width;
        int height = GridSystem.Instance.Height;

        // loop one cell outside the grid on every side (a ring around it)
        for (int x = -1; x <= width; x++)
        {
            for (int y = -1; y <= height; y++)
            {
                bool isInsideGrid = x >= 0 && x < width && y >= 0 && y < height;
                if (isInsideGrid)
                    continue; // skip the play area itself, only draw the ring around it

                bool isPartOfRing = x == -1 || x == width || y == -1 || y == height;
                if (!isPartOfRing)
                    continue;

                Vector3 worldPos = GridSystem.Instance.GridToWorld(new Vector2Int(x, y));
                SpawnWallTile(worldPos, $"BorderWall_{x}_{y}");
            }
        }
    }

    private void SpawnWallTile(Vector3 worldPos, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform);
        go.transform.position = worldPos;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = wallSprite;
        sr.color = wallColor;
        sr.sortingOrder = sortingOrder;
    }
}
