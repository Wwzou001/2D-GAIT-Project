using UnityEngine;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }

    [Header("Room")]
    [SerializeField] private Room roomPrefab;

    [Header("Characters")]
    [SerializeField] private GridMover player;

    [Header("Managers")]
    [SerializeField] private FullGameManager fullGameManager;


    private Room currentRoomInstance;
    private int currentRoom = 0;

    public int CurrentRoom => currentRoom;
    public Room CurrentRoomInstance => currentRoomInstance;


    private void Awake()
    {
        Instance = this;
    }


    private void Start()
    {
        GenerateCurrentRoom();

        GridSystem grid =
            currentRoomInstance.GridSystem;

        Vector2Int spawnPosition =
            grid.playerStart;

        player.SetGridPosition(
            spawnPosition,
            grid
        );
    }


    public void EnterPreviousRoom()
    {
        if (currentRoom <= 0)
            return;

        currentRoom--;

        GenerateCurrentRoom();

        GridSystem newGrid = currentRoomInstance.GridSystem;

        Vector2Int spawnPosition = new Vector2Int(
            newGrid.DoorX,
            newGrid.Height - 1
        );

        player.SetGridPosition(
            spawnPosition,
            newGrid
        );
    }

    private void GenerateCurrentRoom()
    {
        //keep reference to old room
        Room oldRoom = currentRoomInstance;

        //create new room
        currentRoomInstance = Instantiate(roomPrefab, Vector3.zero, Quaternion.identity);

        GridSystem newGrid = currentRoomInstance.GridSystem;

        currentRoomInstance.EnterRoom();

        if (fullGameManager != null)
        {
            fullGameManager.SetCurrentRoom(
                newGrid,
                currentRoomInstance.Enemy
            );
        }

        //destroy old room
        if (oldRoom != null)
        {
            Destroy(oldRoom.gameObject);
        }

        Debug.Log($"Generated Room {currentRoom}");
    }

    public void EnterNextRoom()
    {
        currentRoom++;

        GenerateCurrentRoom();

        GridSystem newGrid = currentRoomInstance.GridSystem;

        Vector2Int spawnPosition = new Vector2Int(
            newGrid.DoorX,
            0
        );

        player.SetGridPosition(
            spawnPosition,
            newGrid
        );

    }


    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}