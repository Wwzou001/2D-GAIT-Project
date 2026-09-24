using System.Collections.Generic;
using UnityEngine;

public class BehaviorTree
{

    public static Node lastNodeRan;

    //node class, none of the "nodes" should use this its just the parent class, everything should either be leaf or selector/sequence
    public class Node
    {

        public enum State
        {
            Inactive,
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

        public State currentState { get; protected set; } = State.Inactive;
        public State displayState { get; protected set; } = State.Inactive;


        public virtual List<Node> GetActivePath()
        {
            List<Node> path = new List<Node>();

            path.Add(this);

            if (children.Count == 0)
                return path;

            if (currentChild >= children.Count)
                return path;

            path.AddRange(children[currentChild].GetActivePath());

            return path;
        }


        public void AddChild(Node child) => children.Add(child);

        public virtual State Process() => children[currentChild].Process();


        
        //UI updater (visual states and display states are different)
        public virtual void UpdateDisplay(float deltaTime)
        {
            if (!BehaviorTreeWalkthrough.Enabled)
            {
                displayState = currentState;
            }

            foreach (Node child in children)
            {
                child.UpdateDisplay(deltaTime);
            }
        }

        public virtual void ResetDisplay()
        {
            displayState = State.Inactive;

            foreach (Node child in children)
            {
                child.ResetDisplay();
            }
        }

        public void SetDisplayState(State state)
        {
            displayState = state;
        }

       

        public virtual void Reset(){
            currentChild = 0;

            currentState = State.Inactive;
            displayState = State.Inactive;
            
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

        //Normally "return Behavior.Process()" is fine but to build the UI I also need to store the last known node and its completion state.
        public override State Process()
        {
            if (BehaviorTreeWalkthrough.Enabled)
            {
                BehaviorTreeWalkthrough.EnterNode(this);
            }

            currentState = behavior.Process();

            if (currentState == State.Success ||
                currentState == State.Failure)
            {
                BehaviorTree.lastNodeRan = this;
            }

            if (BehaviorTreeWalkthrough.Enabled)
            {
                BehaviorTreeWalkthrough.ExitNode(
                    this,
                    currentState
                );
            }
            else
            {
                displayState = currentState;
            }

            return currentState;
        }

    public override void Reset()
    {
        currentState = State.Inactive;
        displayState = State.Inactive;

        behavior.Reset();
    }


  
}





    // Selector nodes will attempt to execute all children nodes - if any succeed then the selector stops trying to execute child nodes and succeeds
    public class Selector : Node {



        public Selector(string name = "Selector") : base(name){}

        public override State Process()
        {   

            if (BehaviorTreeWalkthrough.Enabled)
            {
                BehaviorTreeWalkthrough.EnterNode(this);
            }

            if (children.Count == 0)
            {
                currentState = State.Failure;
                BehaviorTreeWalkthrough.ExitNode(this, currentState);
                return currentState;
            }

            for (int i = 0; i < children.Count; i++){
                currentChild = i;

                State state = children[i].Process();


                switch (state){

                    //if child is still running then the selector is still running
                    case State.Running:
                        currentState = State.Running;
                        for (int j = i + 1; j < children.Count; j++){
                            children[j].Reset();
                        }
                        BehaviorTreeWalkthrough.ExitNode(this, currentState);
                        return currentState;

                    //if the child is successful then I am successful and I do not need to keep running child nodes
                    case State.Success:
                        currentState = State.Success;

                        for (int j = i + 1; j < children.Count; j++){
                            children[j].Reset();
                        }
                        BehaviorTreeWalkthrough.ExitNode(this, currentState);
                        return currentState;

                    //Child Failed so I will attempt to run the next child, therefore i am still running
                    case State.Failure:
                        break;
                }

            }
            //If the selector node runs out of child nodes and none are successful then the selector node fails
            currentState = State.Failure;
            BehaviorTreeWalkthrough.ExitNode(this, currentState);
            return currentState;

        }

    }
    


    //Sequence nodes will execute all child nodes processes - if any fail the sequence node fails

    public class Sequence : Node {



        public Sequence(string name = "Sequence") : base(name){}


        //The Process for a sequence node is to check all of the childrens status'
        // If any children fail the sequence node will fail as well

        public override State Process()
        {

            if (BehaviorTreeWalkthrough.Enabled)
            {
                BehaviorTreeWalkthrough.EnterNode(this);
            }

            if (children.Count == 0)
            {
                currentState = State.Success;
                BehaviorTreeWalkthrough.ExitNode(this, currentState);
                return currentState;
            }

            for (int i = 0; i < children.Count; i++)
            {
                currentChild = i;

                State state = children[i].Process();


                switch (state)
                {

                    //Child is still running so i am still running
                    case State.Running:
                        currentState = State.Running;
                        for (int j = i + 1; j < children.Count; j++){
                            children[j].Reset();
                        }
                        BehaviorTreeWalkthrough.ExitNode(this, currentState);
                        return currentState;

                    //Child has failed so i have failed
                    case State.Failure:
                        
                        currentState = State.Failure;
                        for (int j = i + 1; j < children.Count; j++){
                            children[j].Reset();
                        }
                        BehaviorTreeWalkthrough.ExitNode(this, currentState);
                        return currentState;

                    //Child succeeded so i will check the next child
                    case State.Success:
                        break;
                }
            }
            //After every child is checked we Reset
            currentState = State.Success;
            BehaviorTreeWalkthrough.ExitNode(this, currentState);

            return currentState;

        }


    }

}


