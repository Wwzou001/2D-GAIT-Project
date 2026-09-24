using UnityEngine;

public class Room : MonoBehaviour
{
    [SerializeField] private GridSystem gridSystem;
    [SerializeField] private GridMover enemy;

    public GridSystem GridSystem => gridSystem;
    public GridMover Enemy => enemy;

    private void Awake()
    {
        if (gridSystem == null)
        {
            gridSystem = GetComponent<GridSystem>();
        }

        if (enemy == null)
        {
            enemy = GetComponentInChildren<GridMover>();
        }
    }

    public void EnterRoom()
    {
        gridSystem.SetAsActiveGrid();
    }
}