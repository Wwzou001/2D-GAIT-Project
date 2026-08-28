using UnityEngine;
using TMPro;

public class DecisionTimeLabel : MonoBehaviour
{
    [SerializeField] private MctsEnemyController enemyController;
    [SerializeField] private TMP_Text label;

    void Awake()
    {
        if (label == null)
            label = GetComponent<TMP_Text>();
    }

    void Update()
    {
        if (enemyController == null || label == null) return;

        label.text = $"{enemyController.LastDecisionTimeMs:F2} ms ({enemyController.SimulationsPerMove} sims)";
    }
}
