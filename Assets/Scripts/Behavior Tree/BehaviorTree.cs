using System.Collections.Generic;
using UnityEngine;

public class BehaviorTree
{




    //Leaf Node has no Children So we only need to proccess and reset
    public class Leaf : Node{

        readonly Behaviors behavior;

        public Leaf(Behaviors behavior, string name = "Leaf"): base(name)
        {
            this.behavior = behavior;
        }

        public override State Process()
        {
            return behavior.Process();
        }


        public override void Reset(){
            behavior.Reset();
        }

  
    }


    public class Node
    {

        public enum State
        {
            Running,
            Success,
            Failure
        }

        public readonly string name;

        public readonly List<Node> children = new();
        protected int currentChild;

        public Node(string name = "Node"){
            this.name = name;
        }

        public virtual Node GetActiveNode()
        {
            if (children.Count == 0)
                return this;

            if (currentChild >= children.Count)
                return this;

            return children[currentChild].GetActiveNode();
        }


        public void AddChild(Node child) => children.Add(child);

        public virtual State Process() => children[currentChild].Process();

        public virtual void Reset(){
            currentChild = 0;
            foreach (var child in children){
                child.Reset();
            }
        }


    }


    public class Sequence : Node {

        public Sequence(string name = "Sequence") : base(name){}


        //The Process for a sequence node is to check all of the childrens status'
        // If any children fail the sequence node will fail as well

        public override State Process(){
            

            if(currentChild < children.Count){

                State state = children[currentChild].Process();

                switch (state){
                    case State.Running:
                        return State.Running;
                    case State.Failure:

                        return State.Failure;
                    default:
                        currentChild++;
                        return currentChild == children.Count ? State.Success : State.Running;

                }
            }

            //After every child is checked we Reset
            Reset();
            return State.Success;

        }
    }

}
