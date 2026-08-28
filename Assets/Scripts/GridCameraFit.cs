using UnityEngine;


[RequireComponent (typeof(Camera))]
public class GridCameraFit : MonoBehaviour
{
    [SerializeField] private float padding = 1f;
    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }
    void Start()
    {
        FitToGrid();
    }

    // Update is called once per frame
    public void FitToGrid()
    {
        if (GridSystem.Instance == null || cam == null) return;

        int width = GridSystem.Instance.Width;
        int height = GridSystem.Instance.Height;

        // Centre of the grid
        float centreX = (width - 1) / 2f;
        float centreY = (height - 1) / 2f;
        transform.position = new Vector3(centreX, centreY, transform.position.z);

        // Screen aspect ratio
        float verticalNeeded = (height / 2f) + padding;
        float horizontalNeeded = (width / 2f) + padding;
        float sizeForHorizontal = horizontalNeeded / cam.aspect;

        cam.orthographicSize = Mathf.Max(verticalNeeded, sizeForHorizontal);
    }
}
