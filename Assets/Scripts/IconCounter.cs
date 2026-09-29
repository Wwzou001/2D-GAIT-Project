using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// Shows a row of pictures instead of a number, one picture per item.
// Pictures for things already collected are bright, the rest are dimmed.
// Example with 4 coins and 2 collected: two bright coins and two dim coins.
//
// Put this on an empty object inside the HUD panel. It creates the pictures
// itself while the game runs, so you only need to assign the sprite.
// GameManager calls SetCount() whenever a number changes.
[RequireComponent(typeof(HorizontalLayoutGroup))]
public class IconCounter : MonoBehaviour
{
    [SerializeField] private Sprite icon;
    [SerializeField] private float iconSize = 40f;
    [SerializeField] private float spacing = 6f;
    [SerializeField] private Color collectedColor = Color.white;
    [SerializeField] private Color missingColor = new Color(1f, 1f, 1f, 0.25f);

    // All the pictures created so far. They are reused instead of destroyed.
    private readonly List<Image> icons = new List<Image>();

    private void Awake()
    {
        // Set the layout here so it works no matter what the defaults are.
        HorizontalLayoutGroup layout = GetComponent<HorizontalLayoutGroup>();
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = spacing;
    }

    // collected: how many are done. total: how many pictures to show.
    public void SetCount(int collected, int total)
    {
        // Make more pictures if there are not enough yet.
        while (icons.Count < total)
        {
            icons.Add(CreateIcon());
        }

        for (int i = 0; i < icons.Count; i++)
        {
            bool inUse = i < total;
            icons[i].gameObject.SetActive(inUse);

            if (inUse)
            {
                // The first "collected" pictures are bright, the rest are dim.
                icons[i].color = i < collected ? collectedColor : missingColor;
            }
        }
    }

    private Image CreateIcon()
    {
        GameObject go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(iconSize, iconSize);

        Image image = go.GetComponent<Image>();
        image.sprite = icon;
        image.preserveAspect = true;    // do not stretch the picture
        image.raycastTarget = false;    // pictures should not block mouse clicks

        return image;
    }
}