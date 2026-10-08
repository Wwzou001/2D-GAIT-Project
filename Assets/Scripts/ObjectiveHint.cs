using UnityEngine;
using UnityEngine.UI;
using TMPro;
 
// Shows a short instruction in the top right corner of the screen, inside a
// small dark panel so it never gets lost over the room.
// It builds its own small canvas, so nothing else needs to be set up.
// The text changes as the game goes on:
//   spiders left   -> collect all the spiders to make the key appear
//   key on the map -> pick up the key
//   key collected  -> reach the door
public class ObjectiveHint : MonoBehaviour
{
    [SerializeField] private float fontSize = 18f;
    [SerializeField] private Vector2 margin = new Vector2(16f, 16f);
    [SerializeField] private float panelWidth = 300f;
    [SerializeField] private float padding = 12f;
    [SerializeField] private Color panelColor = new Color(0.2f, 0.17f, 0.16f, 0.85f);
 
    private RectTransform panel;
    private TextMeshProUGUI label;
    private string lastText = null;
 
    private void Start()
    {
        BuildLabel();
    }
 
    private void Update()
    {
        if (label == null || GameManager.Instance == null) return;
 
        GameManager gm = GameManager.Instance;
        string text;
 
        if (gm.GameOver)
        {
            text = "";
        }
        else if (gm.KeysCollected > 0)
        {
            text = "You have the key.\nReach the door to win!";
        }
        else if (gm.TotalFlies > 0 && gm.FliesShot < gm.TotalFlies)
        {
            text = $"Collect all the spiders to make the key appear, then reach the door to win.\nSpiders: {gm.FliesShot} / {gm.TotalFlies}";
        }
        else
        {
            text = "The key has appeared!\nPick it up, then reach the door to win.";
        }
 
        if (text == lastText) return;
        lastText = text;
 
        label.text = text;
        panel.gameObject.SetActive(text.Length > 0);
 
        // Make the panel exactly as tall as the text needs.
        float textHeight = label.GetPreferredValues(text, panelWidth - padding * 2f, 0f).y;
        panel.sizeDelta = new Vector2(panelWidth, textHeight + padding * 2f);
    }
 
    private void BuildLabel()
    {
        GameObject canvasObject = new GameObject("ObjectiveHintCanvas");
        canvasObject.transform.SetParent(transform, false);
 
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
 
        // Scale with the screen so it stays the same size relative to the rest of the UI.
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
 
        // Dark panel in the top right corner.
        GameObject panelObject = new GameObject("ObjectiveHintPanel");
        panelObject.transform.SetParent(canvasObject.transform, false);
 
        Image background = panelObject.AddComponent<Image>();
        background.color = panelColor;
        background.raycastTarget = false;
 
        panel = background.rectTransform;
        panel.anchorMin = new Vector2(1f, 1f);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-margin.x, -margin.y);
        panel.sizeDelta = new Vector2(panelWidth, 80f);
 
        // The text sits inside the panel with some padding.
        GameObject textObject = new GameObject("ObjectiveHintText");
        textObject.transform.SetParent(panelObject.transform, false);
 
        label = textObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.color = Color.white;
        label.enableWordWrapping = true;
        label.raycastTarget = false;
 
        RectTransform rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }
}