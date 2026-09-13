using UnityEngine;


// Central switch for a single slot (A or B) between Human control and AI control (MCS or MCTS).
public class MCTSSlotController : MonoBehaviour
{
    public enum ControlMode { Human, MCS, MCTS }

    [SerializeField] private MctsEnemyController aiController;
    [SerializeField] private MCTSPlayerMovement humanController;

    [SerializeField] private ControlMode currentMode = ControlMode.Human;

    public ControlMode CurrentMode => currentMode;

    private void Awake()
    {
        ApplyMode(currentMode);
    }

    // Called by P1/P2 UI dropdown when the user pick a different control mode for this slot
    public void SetControlMode(ControlMode mode)
    {
        if (mode == currentMode) return;

        currentMode = mode;
        ApplyMode(mode);
    }

    // Overload for UI Dropdown.onValueChanged, which passes an int index rather than the enum directly
    public void SetControlMode(int modeIndex)
    {
        SetControlMode((ControlMode)modeIndex);
    }

    private void ApplyMode(ControlMode mode)
    {
        bool useHuman = mode == ControlMode.Human;

        if (humanController != null)
        {
            humanController.enabled = useHuman;
        }

        if (aiController != null)
        {
            aiController.enabled = !useHuman;

            // If switch to AI mode, make sure controller is using MCS or MCTS
            if (!useHuman)
            {
                var algorithm = mode == ControlMode.MCS
                    ? MctsEnemyController.AlgorithmType.MCS
                    : MctsEnemyController.AlgorithmType.MCTS;
                aiController.SetAlgorithms(algorithm);
            }
        }
    }
}
