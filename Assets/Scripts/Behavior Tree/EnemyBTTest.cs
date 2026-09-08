using UnityEngine;
using TMPro;

public class EnemyBTTest : MonoBehaviour
{
    private BehaviorTree.Sequence root;

    [SerializeField]
    private TMP_Text stateText;

    private void Start()
    {
        BuildTree();
    }

    private void Update()
    {
        root.Process();

        UpdateStateText();
    }


    // Building the entire behavoir tree by creating a sequence then adding the children
    // Needs to be properly done to better reflect the planned behavior tree
    // Needs to have selectors nodes
    private void BuildTree()
    {
        root = new BehaviorTree.Selector("Enemy");

        root.AddChild(new BehaviorTree.Leaf(new Chase(),"Chase"));
        root.AddChild(new BehaviorTree.Leaf(new Patrol(),"Patrol"));

        
        
    }

    private void UpdateStateText()
    {
        BehaviorTree.Node activeNode = root.GetActiveNode();

        stateText.text = $"{activeNode.name}";
    }
}
