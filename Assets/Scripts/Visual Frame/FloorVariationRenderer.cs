using System.Collections;
using UnityEngine;

// After GridVisualiser spawns the floor tiles (named "Floor_x_y"), this
// randomly swaps each one's sprite for a bit of visual variety, instead of
// every floor tile looking identical. Doesn't touch GridVisualiser itself.
public class FloorVariationRenderer : MonoBehaviour
{
    [SerializeField] private Sprite[] floorVariants;
    [SerializeField] private GridVisulizer gridVisualiser; // note: matches the existing (misspelled) class name

    private void Start()
    {
        StartCoroutine(ApplyVariationNextFrame());
    }

    private IEnumerator ApplyVariationNextFrame()
    {
        // wait one frame so GridVisualiser's own Start() has already run
        // and spawned the floor tiles before we try to find them
        yield return null;

        if (floorVariants == null || floorVariants.Length == 0)
        {
            Debug.LogWarning("FloorVariationRenderer: no floor variant sprites assigned.");
            yield break;
        }

        if (gridVisualiser == null)
        {
            Debug.LogWarning("FloorVariationRenderer: no GridVisualiser reference assigned.");
            yield break;
        }

        foreach (Transform child in gridVisualiser.transform)
        {
            if (!child.name.StartsWith("Floor_"))
                continue;

            SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
            if (sr == null)
                continue;

            Sprite randomVariant = floorVariants[Random.Range(0, floorVariants.Length)];
            sr.sprite = randomVariant;
        }
    }
}
