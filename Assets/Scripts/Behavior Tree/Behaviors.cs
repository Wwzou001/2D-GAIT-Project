using System.Collections.Generic;
using UnityEngine;


    public interface Behaviors
    {
        BehaviorTree.Node.State Process();
        void Reset();
    }



    // TWO TEST "BEHAVIORS" SO I CAN TELL IF THE NODES ARE CORRECTLY DONE

    //Will swap between patrolling and chasing ever 3 seconds, wont move yet

    public class Patrol : Behaviors
    {
        private float timer;

        public BehaviorTree.Node.State Process()
        {
            timer += Time.deltaTime;


            if (timer >= 3f)
            {
                Debug.Log("PATROL SUCCESS");
                return BehaviorTree.Node.State.Success;
            }

            return BehaviorTree.Node.State.Running;
        }

        public void Reset()
        {
            Debug.Log("PATROL RESET");
            timer = 0f;
        }
    }



    public class Chase : Behaviors
    {
        private float timer;

        public BehaviorTree.Node.State Process()
        {
            timer += Time.deltaTime;

            if (timer >= 3f)
            {
                Debug.Log("CHASE SUCCESS");
                return BehaviorTree.Node.State.Success;
            }

            return BehaviorTree.Node.State.Running;
        }

        public void Reset()
        {
            timer = 0f;
        }
    }


    



