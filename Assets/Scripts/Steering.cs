using UnityEngine;

namespace SteeringBehaviours
{
    public class Steering
    {
        // Converts a desired velocity into a physical steering force
        // respecting the object mass and acceleration limits
        public static Vector2 VelocityToForce(Vector2 desiredVelocity, Rigidbody2D rb, float accelTime, float maxAccel)
        {
            Vector2 accel = (desiredVelocity - rb.linearVelocity) / accelTime;

            if (accel.magnitude > maxAccel)
            {
                accel = accel.normalized * maxAccel;
            }

            return rb.mass * accel; // F = ma
        }

        public static Vector2 Seek(Vector2 currentPos, Vector2 targetPos, float maxSpeed)
        {
            Vector2 direction = (targetPos - currentPos).normalized;
            return direction * maxSpeed;
        }

        public static Vector2 Arrive(Vector2 currentPos, Vector2 targetPos, float slowRadius, float maxSpeed)
        {
            Vector2 toTarget = targetPos - currentPos;
            float distance = toTarget.magnitude;

            if (distance < 0.01f)
                return Vector2.zero;

            float speed = maxSpeed;
            if (distance < slowRadius)
            {
                speed = maxSpeed * (distance / slowRadius); // closer = slower
            }

            return toTarget.normalized * speed;
        }

        public static Vector2 Flee(Vector2 currentPos, Vector2 threatPos, float maxSpeed)
        {
            Vector2 direction = (currentPos - threatPos).normalized;
            return direction * maxSpeed;
        }
    }
}
