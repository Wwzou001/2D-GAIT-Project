using System.Collections.Generic;
using UnityEngine;

public class BehaviorTree
{



    //node class, none of the "nodes" should use this its just the parent class, everything should either be leaf or selector/sequence
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





    // Selector nodes will attempt to execute all children nodes - if any succeed then the selector stops trying to execute child nodes and succeeds
    public class Selector : Node {

        public override Status Process(){

            if (currentChild < children.Count){
                
                State state = children[currentChild].Process();

                switch (state){

                    //if child is still running then the selector is still running
                    case State.Running:
                        return State.Running;

                    //if the child is successful then I am successful and I do not need to keep running child nodes
                    case State.Success:
                        Reset();
                        return State.Success;

                    //Child Failed so I will attempt to run the next child, therefore i am still running
                    default:
                        currentChild++
                        return Status.Running;
                }
            }
            //If the selector node runs out of child nodes and none are successful then the selector node fails
            Reset();
            return State.Failure;

        }
    }


    //Sequence nodes will execute all child nodes processes - if any fail the sequence node fails

    public class Sequence : Node {

        public Sequence(string name = "Sequence") : base(name){}


        //The Process for a sequence node is to check all of the childrens status'
        // If any children fail the sequence node will fail as well

        public override State Process(){
            

            if(currentChild < children.Count){

                State state = children[currentChild].Process();

                
                switch (state){

                    //Child is still running so i am still running
                    case State.Running:
                        return State.Running;

                    //Child has failed so i have failed
                    case State.Failure:
                        //Unsure if reset early if sequence fails? Will check Later
                        Reset();
                        return State.Failure;
                        
                    //Child succeeded so i will check the next child
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
