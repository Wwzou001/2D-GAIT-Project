using UnityEngine;
using TMPro;

// Connects one slot's (P1 or P2) mode dropdown to MCTSSlotController, and
// shows/hides that slot's simulation count slider based on the selection
// (hidden when Human, shown for MCS/MCTS).
public class SlotConfigUI : MonoBehaviour
{
    [SerializeField] private MCTSSlotController slotController;
    [SerializeField] private TMP_Dropdown modeDropdown; // options must be ordered Human, MCS, MCTS
    [SerializeField] private GameObject simulationSlider; // the slider's GameObject (parent if grouped with a label)

    private void Start()
    {
        // make sure the dropdown and slider visibility match whatever
        // mode the slot actually starts in
        if (modeDropdown != null)
        {
            modeDropdown.value = (int)slotController.CurrentMode;
        }

        UpdateSliderVisibility(slotController.CurrentMode);
    }

    // Wire this to the Dropdown's OnValueChanged (Int32) in the Inspector
    public void OnModeChanged(int index)
    {
        slotController.SetControlMode(index);
        UpdateSliderVisibility((MCTSSlotController.ControlMode)index);
    }

    private void UpdateSliderVisibility(MCTSSlotController.ControlMode mode)
    {
        if (simulationSlider != null)
        {
            simulationSlider.SetActive(mode != MCTSSlotController.ControlMode.Human);
        }
    }
}