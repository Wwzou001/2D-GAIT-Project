using UnityEngine;
using System.Collections.Generic;

namespace SteeringBehaviours
{
    [System.Serializable]
    public class AvoidanceSettings
    {
        public bool Enabled;
        public LayerMask ObstacleLayer;
    }
    
    public class Steering
    {

        // ---------------------------------------------------------------
        // HELPERS : already implemented, no need to change these.
        // ---------------------------------------------------------------
        #region Helpers

        // Converts a desired velocity into a physical steering force
        public static Vector2 VelocityToForce(Vector2 desiredVelocity, Rigidbody2D rb, float accelTime, float maxAccel)
        {
            Vector2 accel = (desiredVelocity - rb.linearVelocity) / accelTime;

            if (accel.magnitude > maxAccel)
            {
                accel = accel.normalized * maxAccel;
            }

            return rb.mass * accel; // F = ma
        }

        // Rotates a 2D vector by the given angle 
         public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector2(
                v.x * Mathf.Cos(rad) - v.y * Mathf.Sin(rad),
                v.x * Mathf.Sin(rad) + v.y * Mathf.Cos(rad)
            );
        }

        // Keeps a flocking group within an area by pulling them back
        // toward the origin 
        public static Vector2 GetBoundaryForce(Vector2 currentPos, Vector2 boundaryDims)
        {
            Vector2 desiredVel = Vector2.zero;

            if (Mathf.Abs(currentPos.x) > boundaryDims.x)
                desiredVel -= new Vector2(currentPos.x, 0f);

            if (Mathf.Abs(currentPos.y) > boundaryDims.y)
                desiredVel -= new Vector2(0f, currentPos.y);

            return desiredVel;
        }

        #endregion

        // ---------------------------------------------------------------
        // OBSTACLEMETHODS: already implemented, no need to change these.
        // decide whether to call the plain version or the
        // obstacle avoiding version 
        // ---------------------------------------------------------------
        #region ObstacleMethods

        public static Vector2 Seek(Vector2 currentPos, Vector2 targetPos, float maxSpeed, AvoidanceSettings avoidance)
        {
            if (avoidance.Enabled)
            {
                return SeekWithAvoidance(currentPos, targetPos, maxSpeed, avoidance);
            }
            else
            {
                return SeekCore(currentPos, targetPos, maxSpeed);
            }
        }

        public static Vector2 SeekWithAvoidance(Vector2 currentPos, Vector2 targetPos, float maxSpeed, AvoidanceSettings avoidance)
        {
            Vector2 adjustedTarget = GetAvoidanceTarget(currentPos, targetPos, avoidance);
            return SeekCore(currentPos, adjustedTarget, maxSpeed);
        }

        public static Vector2 Arrive(Vector2 currentPos, Vector2 targetPos, float slowRadius, float maxSpeed, AvoidanceSettings avoidance)
        {
            if (avoidance.Enabled)
            {
                return ArriveWithAvoidance(currentPos, targetPos, slowRadius, maxSpeed, avoidance);
            }
            else
            {
                return ArriveCore(currentPos, targetPos, slowRadius, maxSpeed);
            }
        }

        public static Vector2 ArriveWithAvoidance(Vector2 currentPos, Vector2 targetPos, float slowRadius, float maxSpeed, AvoidanceSettings avoidance)
        {
            Vector2 adjustedTarget = GetAvoidanceTarget(currentPos, targetPos, avoidance);
            return ArriveCore(currentPos, adjustedTarget, slowRadius, maxSpeed);
        }

        public static Vector2 Flee(Vector2 currentPos, Vector2 threatPos, float maxSpeed, AvoidanceSettings avoidance)
        {
            if (avoidance.Enabled)
            {
                return FleeWithAvoidance(currentPos, threatPos, maxSpeed, avoidance);
            }
            else
            {
                return FleeCore(currentPos, threatPos, maxSpeed);
            }
        }

        public static Vector2 FleeWithAvoidance(Vector2 currentPos, Vector2 threatPos, float maxSpeed, AvoidanceSettings avoidance)
        {
            Vector2 offset = threatPos - currentPos;
            Vector2 fleeTarget = threatPos + offset;

            fleeTarget = GetAvoidanceTarget(currentPos, fleeTarget, avoidance);
            return FleeCore(currentPos, fleeTarget, maxSpeed);
        }

        #endregion

        // ---------------------------------------------------------------
        // COREMETHODS: IMPLEMENT THESE METHODS TO MAKE IT WORK
        // ---------------------------------------------------------------
        #region CoreMethods
        
        // TODO: implement Seek
        //overshoot target because there is no slow down
        public static Vector2 SeekCore(Vector2 currentPos, Vector2 targetPos, float maxSpeed)
        {
            Vector2 direction = (targetPos - currentPos).normalized;
            return direction * maxSpeed;
        }

        // TODO: implement Arrive
        //stop at target instead of overshooting
        public static Vector2 ArriveCore(Vector2 currentPos, Vector2 targetPos, float slowRadius, float maxSpeed)
        {
            Vector2 toTarget = targetPos - currentPos;
            float distance = toTarget.magnitude;

            // already basically there
            if (distance < 0.01f)
                return Vector2.zero;

            float speed = maxSpeed;
            if (distance < slowRadius)
            {
                speed = maxSpeed * (distance / slowRadius); // closer = slower
            }

            return toTarget.normalized * speed;
        }

        // TODO: implement Flee
        // points away from the threat instead of toward it
        public static Vector2 FleeCore(Vector2 currentPos, Vector2 threatPos, float maxSpeed)
        {
            Vector2 direction = (currentPos - threatPos).normalized;
            return direction * maxSpeed;
        }

        // TODO: implement obstacle avoidance
        // Check if the path to targetPos is blocked (Physics2D.CircleCast). If it is, try turning left/right 
       // (use Rotate() above) until u find a clear direction, and return that as the new target
        public static Vector2 GetAvoidanceTarget(Vector2 currentPos, Vector2 targetPos, AvoidanceSettings avoidance)
        {
            return targetPos;
        }

        // TODO: implement separation
        // Should push away from nearby neighbours so they don't clump together
        public static Vector2 GetSeparation(Vector2 currentPos, List<Transform> neighbours, float maxSpeed)
        {
            return Vector2.zero;
        }

        // TODO: implement cohesion
        // Should pull toward the average position of nearby neighbours
        public static Vector2 GetCohesion(Vector2 currentPos, List<Transform> neighbours, float maxSpeed)
        {
            return Vector2.zero;
        }

        // TODO: implement alignment
        // Should match the average heading/velocity of nearby neighbours
        public static Vector2 GetAlignment(List<Transform> neighbours, float maxSpeed)
        {
            return Vector2.zero;
        }

        #endregion
    }
}
