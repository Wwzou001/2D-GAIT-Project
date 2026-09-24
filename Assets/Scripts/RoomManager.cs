using UnityEngine;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }

    [SerializeField] private GridMover player;

    private int currentRoom = 0;

    public int CurrentRoom => currentRoom;

    private void Awake()
    {
        Instance = this;
    }

    public void EnterNextRoom()
    {
        currentRoom++;

        Debug.Log($"Entering Room {currentRoom}");

        GenerateCurrentRoom();

        // spawn player at bottom of new room
        Vector2Int spawnPosition = new Vector2Int(
            GridSystem.Instance.DoorX,
            0
        );

        player.SetGridPosition(spawnPosition);
    }

    public void EnterPreviousRoom()
    {
        if (currentRoom <= 0)
            return;

        currentRoom--;

        Debug.Log($"Returning to Room {currentRoom}");

        GenerateCurrentRoom();

        // Player entered from the top, so place them on the top of the room.
        Vector2Int spawnPosition = new Vector2Int(
            GridSystem.Instance.DoorX,
            GridSystem.Instance.Height - 1
        );

        player.SetGridPosition(spawnPosition);
    }

    private void GenerateCurrentRoom()
    {
        GridSystem.Instance.RegenerateLayout();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartNewRoom();
        }
    }
}