using UnityEngine;
using System.Collections.Generic;
using System.Text;

using UnityEditor;

public class MCTSBalanceTester : EditorWindow
{
    private int matchesPerCombo = 50;
    private int maxTurnsPerMatch = 200; // safety cap so stuck match doesn't loop forever

    // Each row: (MCS simulations, MCTS iterations)
    private List<(int mcs, int mcts)> combos = new List<(int, int)>
    {
        (50, 1), (200, 50), (1000, 100), (1000, 300), (1000, 500)
    };

    private string resultsText = "";
    private Vector2 scrollPos;

    [MenuItem("Tools/MCTS Balance Tester")]
    public static void ShowWindow()
    {
        GetWindow<MCTSBalanceTester>("MCTS Balance Tester");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Open the MCTS DungeonEscape scene first (needs an existing GridSystem in the scene). " +
            "This runs matches instantly, without Play mode.", MessageType.Info );

        GUILayout.Space(10);
        GUILayout.Label("Test setup", EditorStyles.boldLabel);
        matchesPerCombo = EditorGUILayout.IntField("Matches per combo", matchesPerCombo);
        maxTurnsPerMatch = EditorGUILayout.IntField("Max turns per match", maxTurnsPerMatch);

        GUILayout.Space (10);
        GUILayout.Label("Combos (MCS sims, MCTS iterations) -- MCS is always Collector, MCTS is always Hunter", EditorStyles.boldLabel);
        for (int i = 0; i < combos.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            int mcs = EditorGUILayout.IntField("MCS", combos[i].mcs);
            int mcts = EditorGUILayout.IntField("MCTS", combos[i].mcts);
            combos[i] = (mcs, mcts);
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                combos.RemoveAt(i);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("Add combo"))
        {
            combos.Add((100, 100));
        }

        GUILayout.Space(10);
        if (GUILayout.Button("Run all combos", GUILayout.Height(30)))
        {
            RunAllCombos();
        }

        GUILayout.Space(10);
        GUILayout.Label("Results (copy this into a spreadsheet)", EditorStyles.boldLabel);
        scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(250));
        EditorGUILayout.TextArea(resultsText, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void RunAllCombos()
    {
        if (GridSystem.Instance == null)
        {
            GridSystem found = Object.FindFirstObjectByType<GridSystem>();
            if (found == null)
            {
                resultsText = "ERROR: No GridSystem found. Open the MCTS DungeonEscape scene first.";
                Debug.LogError("[BalanceTester] " + resultsText);
                return;
            }
            found.RegenerateLayout(spawnColliders: false);
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("MCS_sims\tMCTS_iters\tMCS_(Collector)_win_rate\tMCTS_(Hunter)_win_rate\tAvg_turns");

        foreach (var combo in combos)
        {
            var (collectorWins, hunterWins, avgTurns) = RunCombo(combo.mcs, combo.mcts);
            int total = collectorWins + hunterWins;
            float collectorRate = total > 0 ? (float)collectorWins / total * 100f : 0f;
            float hunterRate = total > 0 ? (float)hunterWins / total * 100f : 0f;

            sb.AppendLine($"{combo.mcs}\t{combo.mcts}\t{collectorRate:F1}%\t{hunterRate:F1}%\t{avgTurns:F1}");
            Debug.Log($"[BalanceTester] MCS={combo.mcs} vs MCTS={combo.mcts}: Collector(MCS) won {collectorRate:F1}%, Hunter(MCTS) won {hunterRate:F1}% over {total} matches, avg {avgTurns:F1} turns");
        }

        resultsText = sb.ToString();
    }

    // Run matchesPerCombo matches for one combo and return win counts and average match length
    private(int collectorWins, int hunterWins, float avgTurns) RunCombo(int mcsSims, int mctsIters)
    {
        int collectorWins = 0;
        int hunterWins = 0;
        int totalTurns = 0;

        for (int match = 0; match < matchesPerCombo; match++)
        {
            // Call GridSystem.Instance.RegenerateLayout() to make sure every match uses a fresh random obstacle and coin layout
            GridSystem.Instance.RegenerateLayout(spawnColliders: false);
            var collectorAgent = new MCSAgent(mcsSims, 15, isHunter: false);
            var hunterAgent = new MCTSAgent(mctsIters, 15, isHunter: true);

            Vector2Int collectorPos = GridSystem.Instance.playerStart;
            Vector2Int hunterPos = GridSystem.Instance.npcStart;

            bool gameOver = false;
            bool collectorWon = false;
            int turn = 0;

            while (!gameOver && turn < maxTurnsPerMatch)
            {
                // Collector's turn
                Direction collectorMove = collectorAgent.ChooseMove(collectorPos, hunterPos, false, out _);
                collectorPos = ApplyMove(collectorPos, collectorMove);

                if (GridSystem.Instance.IsCoin(collectorPos))
                {
                    GridSystem.Instance.CollectCoin(collectorPos);
                }

                if (collectorPos == hunterPos)
                {
                    gameOver = true;
                    collectorWon = false;
                    break;
                }
                if (GridSystem.Instance.RemainingCoins() == 0)
                {
                    gameOver = true;
                    collectorWon = true;
                    break;
                }

                // Hunter's turn
                Direction hunterMove = hunterAgent.ChooseMove(hunterPos, collectorPos, false, out _);
                hunterPos = ApplyMove(hunterPos, hunterMove);

                if (collectorPos == hunterPos)
                {
                    gameOver = true;
                    collectorWon = false;
                    break;
                }

                turn++;
            }

            totalTurns += turn;
            if (collectorWon) 
            { 
                collectorWins++; 
            }
            else
            {
                hunterWins++;
            }
        }

        float avgTurns = matchesPerCombo > 0 ? (float)totalTurns / matchesPerCombo : 0f;
        return (collectorWins, hunterWins, avgTurns);
    }

    private Vector2Int ApplyMove(Vector2Int from, Direction dir)
    {
        Vector2Int offset = dir switch
        {
            Direction.Up => new Vector2Int(0, 1),
            Direction.Down => new Vector2Int(0, -1),
            Direction.Left => new Vector2Int(-1, 0),
            Direction.Right => new Vector2Int(1, 0),
            _ => Vector2Int.zero
        };

        Vector2Int target = from + offset;
        if (!GridSystem.Instance.IsInBounds(target)) return from;
        if (GridSystem.Instance.IsObstacle(target)) return from;
        return target;
    }
}
