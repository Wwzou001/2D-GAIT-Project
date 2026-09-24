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
        private GridMover mover;

        private Vector2Int pointA = new Vector2Int(0, 4);
        private Vector2Int pointB = new Vector2Int(8, 4);

        private Vector2Int currentTarget;

        private float nextMoveTime;
        private float moveInterval = 0.6f;

        public Patrol(GridMover mover)
        {
            this.mover = mover;
            currentTarget = pointA;
            nextMoveTime = Time.time + moveInterval;
        }


        //move towards the patrol nodes, need to change this to be moving to random square most likely?
        public BehaviorTree.Node.State Process()
        {

            if (Time.time < nextMoveTime)
            {
                return BehaviorTree.Node.State.Running;
            }



            //if we've reached the current patrol point then switch to the other one.
            if (mover.GridPosition == currentTarget)
            {
                currentTarget = currentTarget == pointA
                    ? pointB
                    : pointA;
            }


            // Perform ONE movement toward the remembered target.
            bool moved = MoveTowards(currentTarget);

            nextMoveTime = Time.time + moveInterval;

            if (moved)
            {
                return BehaviorTree.Node.State.Success;
            }

            return BehaviorTree.Node.State.Failure;
        }

        //change to a* later
        private bool MoveTowards(Vector2Int target)
        {
            Vector2Int current = mover.GridPosition;

            if (current.x < target.x)
            {
                return mover.TryMove(Direction.Right);
            }
            else if (current.x > target.x)
            {
                return mover.TryMove(Direction.Left);
            }
            else if (current.y < target.y)
            {
                return mover.TryMove(Direction.Up);
            }
            else if (current.y > target.y)
            {
                return mover.TryMove(Direction.Down);
            }

            return false;
        }

        public void Reset()
        {
        }
    }


    //check in 2 tiles around the enemy in diamond shape
    public class CheckForPlayer : Behaviors
    {
        private GridMover enemyMover;
        private GridMover playerMover;

        private int detectionRange = 2;

        public CheckForPlayer(GridMover enemyMover, GridMover playerMover)
        {
            this.enemyMover = enemyMover;
            this.playerMover = playerMover;
        }

        public BehaviorTree.Node.State Process()
        {
            Vector2Int difference =
                playerMover.GridPosition - enemyMover.GridPosition;

            int distance = Mathf.Abs(difference.x) + Mathf.Abs(difference.y);

            if (distance <= detectionRange)
            {
                return BehaviorTree.Node.State.Success;
            }

            return BehaviorTree.Node.State.Failure;
        }

        public void Reset()
        {
        }
    }



    //very low logic move towards player, will get stuck on obstacles need to implement a* probably
    public class MoveTowardsPlayer : Behaviors
    {
        private GridMover enemyMover;
        private GridMover playerMover;

        private float nextMoveTime;
        private float moveInterval = 0.6f;

        public MoveTowardsPlayer(GridMover enemyMover, GridMover playerMover)
        {
            this.enemyMover = enemyMover;
            this.playerMover = playerMover;
            nextMoveTime = Time.time + moveInterval;
        }

        public BehaviorTree.Node.State Process()
        {

            if (Time.time < nextMoveTime)
            {
                return BehaviorTree.Node.State.Running;
            }

            Vector2Int enemyPos = enemyMover.GridPosition;
            Vector2Int playerPos = playerMover.GridPosition;

            if (enemyPos == playerPos)
            {
                nextMoveTime = Time.time + moveInterval;
                return BehaviorTree.Node.State.Success;
            }

            bool moved = false;

            if (enemyPos.x < playerPos.x)
            {
                moved = enemyMover.TryMove(Direction.Right);
            }
            else if (enemyPos.x > playerPos.x)
            {
                moved = enemyMover.TryMove(Direction.Left);
            }
            else if (enemyPos.y < playerPos.y)
            {
                moved = enemyMover.TryMove(Direction.Up);
            }
            else if (enemyPos.y > playerPos.y)
            {
                moved = enemyMover.TryMove(Direction.Down);
            }

            nextMoveTime = Time.time + moveInterval;

            return moved
                ? BehaviorTree.Node.State.Success
                : BehaviorTree.Node.State.Failure;
        }

        public void Reset()
        {
        }
    }


    //Unfortunately does not work since i made the nodes reactive :( since patrol will always happen it takes priority over waiting the 2 seconds
    //Changing this to a "rest" cause i like the idea, just waits 2 seconds before moving
    public class Rest : Behaviors
    {
        private float timer;

        public BehaviorTree.Node.State Process()
        {
            timer += Time.deltaTime;

            if (timer >= 3f)
            {
                Debug.Log("REST SUCCESS");
                return BehaviorTree.Node.State.Success;
            }

            return BehaviorTree.Node.State.Running;
        }

        public void Reset()
        {
            timer = 0f;
        }
    }


    



