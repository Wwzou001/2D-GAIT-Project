using UnityEngine;
using TMPro;

public class SimulationCountLabel : MonoBehaviour
{
    [SerializeField] private MctsEnemyController enemyController;
    [SerializeField] private TMP_Text label;

    void Awake()
    {
        if (label == null)
        {
            label = GetComponent<TMP_Text>();
        }
    }

    void Update()
    {
        if (enemyController == null || label == null) return;

        label.text = $"Simulations: {enemyController.SimulationsPerMove}";
    }
}
