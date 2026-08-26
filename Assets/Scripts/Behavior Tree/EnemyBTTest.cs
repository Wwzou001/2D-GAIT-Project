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

    private void BuildTree()
    {
        root = new BehaviorTree.Sequence("Enemy");

        root.AddChild(new BehaviorTree.Leaf(new Patrol(),"Patrol"));

        root.AddChild(new BehaviorTree.Leaf(new Chase(),"Chase"));
        
    }

    private void UpdateStateText()
    {
        BehaviorTree.Node activeNode = root.GetActiveNode();

        stateText.text = $"{activeNode.name}";
    }
}
