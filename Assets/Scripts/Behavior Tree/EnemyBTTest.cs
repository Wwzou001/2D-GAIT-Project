using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class EnemyBTTest : MonoBehaviour
{   

    private BehaviorTree.Selector root;

    [SerializeField]
    private TMP_Text stateText;

    [SerializeField]
    private TMP_Text walkthroughText;

    [SerializeField]
    private GridMover playerMover;


    [SerializeField]
    private float walkthroughBeforeDelay = 0.75f;

    [SerializeField]
    private float walkthroughAfterDelay = 0.5f;

    
    private bool resetTreeBeforeNextRun = false;



    private void Start()
    {
        BuildTree();

        BehaviorTreeWalkthrough.BeforeDelay =
            walkthroughBeforeDelay;

        BehaviorTreeWalkthrough.AfterDelay =
            walkthroughAfterDelay;

        //start with walkthrough mode off
        BehaviorTreeWalkthrough.Enabled = false;

        //hide walkthrough text at startup
        walkthroughText.enabled = false;

        UpdateStateText();
    }


    private void Update()
    {
        if (BehaviorTreeWalkthrough.IsPlaying)
        {
            BehaviorTreeWalkthrough.Update(Time.deltaTime);

            UpdateStateText();
            return;
        }


        if (resetTreeBeforeNextRun)
        {

            root.Reset();

            resetTreeBeforeNextRun = false;

            UpdateStateText();

            return;
        }

        BehaviorTree.Node.State result = root.Process();

        if (BehaviorTreeWalkthrough.Enabled)
        {
            BehaviorTreeWalkthrough.Begin();
        }
        else
        {
            root.UpdateDisplay(Time.deltaTime);
        }

        if (result == BehaviorTree.Node.State.Success ||
            result == BehaviorTree.Node.State.Failure)
        {
            Debug.Log("Reseting Tree");

            resetTreeBeforeNextRun = true;
        }

        UpdateStateText();
    }


    public void ToggleWalkthroughMode()
    {
        BehaviorTreeWalkthrough.Enabled =
            !BehaviorTreeWalkthrough.Enabled;

        //show/hide text based on walkthrough mode
        walkthroughText.enabled =
            BehaviorTreeWalkthrough.Enabled;


        BehaviorTreeWalkthrough.BeforeDelay =
            walkthroughBeforeDelay;

        BehaviorTreeWalkthrough.AfterDelay =
            walkthroughAfterDelay;


        BehaviorTreeWalkthrough.Clear();

        root?.Reset();


        if (!BehaviorTreeWalkthrough.Enabled)
        {
            root?.UpdateDisplay(0f);
        }

        UpdateStateText();
    }


    // Building the entire behavoir tree by creating a sequence then adding the children
    // Needs to be properly done to better reflect the planned behavior tree
    // Needs to have selectors nodes
    private void BuildTree()
    {   

         GridMover enemyMover = GetComponent<GridMover>();

        root = new BehaviorTree.Selector("Enemy");

        //enemy will chase the player if they are in range (this is the first sequence attached to the root so it always happens if possible)
        BehaviorTree.Sequence chaseSequence = new BehaviorTree.Sequence("Chase Sequence");

        chaseSequence.AddChild(new BehaviorTree.Leaf(new CheckForPlayer(enemyMover, playerMover), "Check For Player"));

        chaseSequence.AddChild(new BehaviorTree.Leaf(new MoveTowardsPlayer(enemyMover, playerMover), "Move Towards Player"));

        //patrol sequence for now this is the default state
        BehaviorTree.Sequence patrolSequence = new BehaviorTree.Sequence("Patrol Sequence");

        patrolSequence.AddChild(new BehaviorTree.Leaf(new Patrol(enemyMover), "Patrol"));


        //add branches to root
        root.AddChild(chaseSequence);
        root.AddChild(patrolSequence);
    }
        
    

    private void UpdateStateText()
    {
        stateText.text = BuildTreeText(root, 0);
    }


    private string BuildTreeText(BehaviorTree.Node node, int depth)
    {
        string text = "";

        string indent = new string('-', depth);

        text += $"{indent}{node.name} ({GetStateText(node.displayState)})\n";

        foreach (BehaviorTree.Node child in node.children)
        {
            text += BuildTreeText(child, depth + 1);
        }

        return text;
    }


    private string GetStateText(BehaviorTree.Node.State state)
    {
        switch (state)
        {
            case BehaviorTree.Node.State.Inactive:
                return "<color=grey>Inactive</color>";

            case BehaviorTree.Node.State.Running:
                return "<color=yellow>Running</color>";

            case BehaviorTree.Node.State.Success:
                return "<color=green>Success</color>";

            case BehaviorTree.Node.State.Failure:
                return "<color=red>Failure</color>";
        }

        return state.ToString();
    }
   
}

