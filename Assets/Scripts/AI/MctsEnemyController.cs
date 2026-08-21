using UnityEngine;

public class MctsEnemyController : MonoBehaviour
{
    [SerializeField] private GridMover enemyMover;
    [SerializeField] private GridMover playerMover;

    [SerializeField] private int simulationsPerMove = 300;
    [SerializeField] private int rolloutDepth = 15;
    [SerializeField] private float moveInterval = 0.6f;
    [SerializeField] private bool logDecisions = true;

    private MctsAgent agent;
    private float timer;

    private void Awake()
    {
        if (enemyMover == null)
            enemyMover = GetComponent<GridMover>();

        agent = new MctsAgent(simulationsPerMove, rolloutDepth);
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.GameOver)
            return;

        timer += Time.deltaTime;
        if (timer < moveInterval) return;
        timer = 0f;

        Direction move = agent.ChooseMove(enemyMover.GridPosition, playerMover.GridPosition, out string log);

        if (logDecisions)
            Debug.Log(log);

        enemyMover.TryMove(move);
    }
}
