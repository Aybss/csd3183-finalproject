using UnityEngine;

public class GridVisualizer : MonoBehaviour
{
    public static GridVisualizer Instance { get; private set; }

    [Header("Colors")]
    public Color validColor = new Color(0f, 1f, 0f, 0.4f);
    public Color blockedColor = new Color(1f, 0f, 0f, 0.5f);

    [Header("Setup")]
    [Tooltip("How high above the floor position the highlight floats.")]
    public float yOffset = 0.05f;

    private GameObject _previewCube;
    private Material _previewMaterial;

    private void Awake()
    {
        Instance = this;

        _previewCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _previewCube.name = "GridPlacementPreviewVisual";
        _previewCube.transform.SetParent(transform);

        Destroy(_previewCube.GetComponent<BoxCollider>());

        Renderer renderer = _previewCube.GetComponent<MeshRenderer>();
        _previewMaterial = new Material(Shader.Find("UI/Default"));
        renderer.material = _previewMaterial;

        _previewCube.SetActive(false);
    }

    public void ClearPreview()
    {
        if (_previewCube != null)
        {
            _previewCube.SetActive(false);
        }
    }

    public void UpdatePreview(Vector2Int baseCell, Vector2Int footprint, bool isValid)
    {
        if (_previewCube == null) return;

        _previewCube.SetActive(true);
        _previewMaterial.color = isValid ? validColor : blockedColor;

        ObstacleGrid grid = ObstacleGrid.Instance;
        float cSize = grid.cellSize;

        float width = footprint.x * cSize;
        float length = footprint.y * cSize;
        float thickness = 0.01f;

        _previewCube.transform.localScale = new Vector3(width, thickness, length);

        // Position aligns perfectly to the cell corner boundaries
        Vector3 cellCorner = grid.originWorldPos + new Vector3(baseCell.x * cSize, 0f, baseCell.y * cSize);

        float targetX = cellCorner.x + (width * 0.5f);
        float targetZ = cellCorner.z + (length * 0.5f);
        float targetY = cellCorner.y + yOffset + (thickness * 0.5f);

        _previewCube.transform.position = new Vector3(targetX, targetY, targetZ);
    }
}