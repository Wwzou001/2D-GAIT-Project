using UnityEngine;

public class NextStepButtonVisibility: MonoBehaviour
{
    [SerializeField] private MctsEnemyController enemyController;

    void Update()
    {
        if (enemyController == null) return;

        gameObject.SetActive(enemyController.StepMode);
    }
}
