using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// Draws the HUD panel in the top left corner as rows of pictures.
//   First row:  the coins, then the key.
//   Second row: the flies.
// A picture is bright once that item has been collected and dim until then.
//
// Everything is built in code while the game runs, so there is nothing to lay out
// by hand. The panel grows to fit the pictures but never gets smaller than Min Panel Size.
//
// Setup: add this script to any object in the scene (the GameManager object is a good place)
// and drag in the three sprites. If a sprite is left empty, a coloured circle is used instead,
// so the HUD always shows something.
public class GameHud : MonoBehaviour
{
    [Header("Pictures")]
    [SerializeField] private Sprite coinSprite;
    [SerializeField] private Sprite keySprite;
    [SerializeField] private Sprite flySprite;

    [Header("Panel")]
    [SerializeField] private Color panelColor = new Color(0.18f, 0.23f, 0.10f, 0.8f);   // dark green, slightly see through
    [SerializeField] private Vector2 minPanelSize = new Vector2(222f, 60f);
    [SerializeField] private Vector2 offsetFromCorner = new Vector2(20f, -20f);
    [SerializeField] private float paddingX = 12f;
    [SerializeField] private float paddingY = 8f;

    [Header("Pictures layout")]
    [SerializeField] private float iconSize = 20f;
    [SerializeField] private float iconSpacing = 4f;
    [SerializeField] private float groupGap = 12f;      // gap between the coins and the key
    [SerializeField] private float rowSpacing = 4f;
    [SerializeField] private float dimAmount = 0.25f;   // how faint a picture is until it is collected

    // One set of pictures, for example all the coins.
    private class Group
    {
        public Sprite sprite;
        public Color tint = Color.white;
        public readonly List<Image> icons = new List<Image>();
        public int lastTotal = -1;
        public int lastCollected = -1;
    }

    private RectTransform panelRect;
    private Group coins;
    private Group keys;
    private Group flies;
    private bool layoutDirty = true;

    private void Start()
    {
        Canvas canvas = FindCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("GameHud: there is no Canvas in the scene, so the HUD cannot be drawn.");
            enabled = false;
            return;
        }

        // If a sprite is missing, use a coloured circle so the HUD still shows something.
        Sprite fallback = MakeCircleSprite(16);
        coins = new Group { sprite = coinSprite != null ? coinSprite : fallback, tint = coinSprite != null ? Color.white : new Color(1f, 0.82f, 0.2f) };
        keys = new Group { sprite = keySprite != null ? keySprite : fallback, tint = keySprite != null ? Color.white : new Color(0.7f, 0.85f, 1f) };
        flies = new Group { sprite = flySprite != null ? flySprite : fallback, tint = flySprite != null ? Color.white : new Color(0.85f, 0.85f, 0.85f) };

        BuildPanel(canvas.transform);
    }

    // Picks the main screen canvas.
    private static Canvas FindCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);

        foreach (Canvas c in canvases)
        {
            if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace)
                return c;
        }

        return canvases.Length > 0 ? canvases[0] : null;
    }

    private void BuildPanel(Transform canvasTransform)
    {
        GameObject panel = new GameObject("HudPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasTransform, false);

        // Pinned to the top left corner of the screen.
        panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = offsetFromCorner;
        panelRect.sizeDelta = minPanelSize;

        Image background = panel.GetComponent<Image>();
        background.sprite = MakeRoundedSprite(16, 5);
        background.type = Image.Type.Sliced;
        background.color = panelColor;
        background.raycastTarget = false;   // never block clicks on buttons underneath
    }

    private void Update()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || panelRect == null)
            return;

        Refresh(coins, manager.CoinsCollected, manager.TotalCoins);
        Refresh(keys, manager.KeysCollected, manager.TotalKeys);
        Refresh(flies, manager.FliesShot, manager.TotalFlies);

        if (layoutDirty)
        {
            ApplyLayout();
            layoutDirty = false;
        }
    }

    // Makes the right number of pictures and colours them bright or dim.
    private void Refresh(Group group, int collected, int total)
    {
        total = Mathf.Max(total, 0);
        collected = Mathf.Clamp(collected, 0, total);

        if (total == group.lastTotal && collected == group.lastCollected)
            return;   // nothing changed

        if (total != group.lastTotal)
            layoutDirty = true;

        group.lastTotal = total;
        group.lastCollected = collected;

        while (group.icons.Count < total)
        {
            group.icons.Add(CreateIcon(group));
        }

        for (int i = 0; i < group.icons.Count; i++)
        {
            Image icon = group.icons[i];
            bool inUse = i < total;
            icon.gameObject.SetActive(inUse);

            if (inUse)
            {
                Color color = group.tint;
                color.a = i < collected ? 1f : dimAmount;   // the first "collected" pictures are bright
                icon.color = color;
            }
        }
    }

    private Image CreateIcon(Group group)
    {
        GameObject go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(panelRect, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(iconSize, iconSize);

        Image image = go.GetComponent<Image>();
        image.sprite = group.sprite;
        image.preserveAspect = true;   // do not stretch the picture
        image.raycastTarget = false;

        return image;
    }

    // Puts every picture on its own row and sizes the panel to fit.
    private void ApplyLayout()
    {
        int coinCount = Mathf.Max(coins.lastTotal, 0);
        int keyCount = Mathf.Max(keys.lastTotal, 0);
        int flyCount = Mathf.Max(flies.lastTotal, 0);

        // Each category gets its own row. A category with nothing to show
        // is skipped, so the panel doesn't leave a blank gap.
        int nextRow = 0;
        float contentWidth = 0f;

        if (coinCount > 0)
        {
            PlaceGroup(coins, paddingX, nextRow);
            contentWidth = Mathf.Max(contentWidth, GroupWidth(coinCount));
            nextRow++;
        }

        if (flyCount > 0)
        {
            PlaceGroup(flies, paddingX, nextRow);
            contentWidth = Mathf.Max(contentWidth, GroupWidth(flyCount));
            nextRow++;
        }

        if (keyCount > 0)
        {
            PlaceGroup(keys, paddingX, nextRow);
            contentWidth = Mathf.Max(contentWidth, GroupWidth(keyCount));
            nextRow++;
        }

        int rows = nextRow;
        float contentHeight = rows * iconSize + Mathf.Max(rows - 1, 0) * rowSpacing;

        float width = Mathf.Max(minPanelSize.x, contentWidth + 2f * paddingX);
        float height = Mathf.Max(minPanelSize.y, contentHeight + 2f * paddingY);
        panelRect.sizeDelta = new Vector2(width, height);
    }

    private float GroupWidth(int count)
    {
        if (count <= 0)
            return 0f;

        return count * iconSize + (count - 1) * iconSpacing;
    }

    private void PlaceGroup(Group group, float startX, int row)
    {
        float y = -(paddingY + row * (iconSize + rowSpacing));

        for (int i = 0; i < group.icons.Count; i++)
        {
            RectTransform rect = (RectTransform)group.icons[i].transform;
            rect.anchoredPosition = new Vector2(startX + i * (iconSize + iconSpacing), y);
        }
    }

    // A white circle, used when a sprite has not been assigned.
    private static Sprite MakeCircleSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;

        float radius = size / 2f;
        Vector2 centre = new Vector2(radius, radius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centre);
                texture.SetPixel(x, y, distance <= radius ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // A small white rounded square that stretches cleanly to any size (a 9 slice).
    private static Sprite MakeRoundedSprite(int size, int radius)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pixel = new Vector2(x + 0.5f, y + 0.5f);
                Vector2 nearest = new Vector2(
                    Mathf.Clamp(pixel.x, radius, size - radius),
                    Mathf.Clamp(pixel.y, radius, size - radius));

                float distance = Vector2.Distance(pixel, nearest);
                float alpha = Mathf.Clamp01(radius + 0.5f - distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }
}
