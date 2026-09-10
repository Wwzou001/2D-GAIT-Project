using System.Collections.Generic;
using UnityEngine;
using SteeringBehaviours;
 
[RequireComponent(typeof(Rigidbody2D))]
public class FlyFSM : MonoBehaviour
{
    public enum FlyState { Flocking, Alone, Fleeing }
    public enum FlyEvent { JoinedFlock, LostFlock, ScaredByPlayer, PlayerGotFar }
 
    public FlyState State = FlyState.Alone;
 
// Flee From Player
    public Transform player;
    public float fleeTriggerRange = 2f;
    public float stopFleeingRange = 4f;
 
// Flocking Weights
    public float separationWeight = 1.5f;
    public float cohesionWeight = 1f;
    public float alignmentWeight = 1f;
    public float boundaryWeight = 1f;
 
// Movement Settings
    public float maxSpeed = 4f;
    public float maxAccel = 8f;
    public float accelTime = 0.25f;
 
// Flocking Range
    public float neighbourRadius = 3f;
 
// Boundary (keeps the flock roughly centered)
    public Vector2 boundaryCenter = Vector2.zero;
    public Vector2 boundaryDims = new Vector2(6f, 6f);
 
    public AvoidanceSettings avoidance = new AvoidanceSettings
    {
        Enabled = true,
        LookAheadDistance = 2f,
        CheckRadius = 0.4f,
        AngleStep = 10f
    };
 
    private Rigidbody2D rb;
    private List<Transform> neighbours = new List<Transform>();
 
    private static List<FlyFSM> AllFlies = new List<FlyFSM>();
 
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }
 
    private void OnEnable()
    {
        AllFlies.Add(this);
    }
 
    private void OnDisable()
    {
        AllFlies.Remove(this);
    }
 
    private void FixedUpdate()
    {
        UpdateNeighbours();
        CheckForStateChangingEvents();
        RunCurrentState();
    }
 
    private void UpdateNeighbours()
    {
        neighbours.Clear();
 
        foreach (FlyFSM other in AllFlies)
        {
            if (other == this)
                continue;
 
            float distance = Vector2.Distance(transform.position, other.transform.position);
            if (distance <= neighbourRadius)
            {
                neighbours.Add(other.transform);
            }
        }
    }
 
    private void CheckForStateChangingEvents()
    {
        // player proximity takes priority over flock membership
        if (player != null)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, player.position);
 
            if (distanceToPlayer < fleeTriggerRange)
            {
                HandleEvent(FlyEvent.ScaredByPlayer);
                return; // don't also process flock events while fleeing
            }
 
            if (State == FlyState.Fleeing && distanceToPlayer > stopFleeingRange)
            {
                HandleEvent(FlyEvent.PlayerGotFar);
            }
        }
 
        if (State == FlyState.Fleeing)
            return; // stay fleeing until PlayerGotFar fires above
 
        if (neighbours.Count == 0)
        {
            HandleEvent(FlyEvent.LostFlock);
        }
        else
        {
            HandleEvent(FlyEvent.JoinedFlock);
        }
    }
 
    private void HandleEvent(FlyEvent e)
    {
        // any state can be scared into fleeing
        if (e == FlyEvent.ScaredByPlayer)
        {
            State = FlyState.Fleeing;
            return;
        }
 
        if (e == FlyEvent.PlayerGotFar && State == FlyState.Fleeing)
        {
            // go back to flocking if there's a flock to rejoin, otherwise alone
            State = neighbours.Count > 0 ? FlyState.Flocking : FlyState.Alone;
            return;
        }
 
        if (e == FlyEvent.LostFlock && State == FlyState.Flocking)
        {
            State = FlyState.Alone;
        }
        else if (e == FlyEvent.JoinedFlock && State == FlyState.Alone)
        {
            State = FlyState.Flocking;
        }
    }
 
    private void RunCurrentState()
    {
        Vector2 currentPos = rb.position;
        Vector2 desiredVelocity;
 
        if (State == FlyState.Flocking)
        {
            Vector2 separation = separationWeight * Steering.GetSeparation(currentPos, neighbours, maxSpeed);
            Vector2 cohesion = cohesionWeight * Steering.GetCohesion(currentPos, neighbours, maxSpeed);
            Vector2 alignment = alignmentWeight * Steering.GetAlignment(neighbours, maxSpeed);
            Vector2 boundary = boundaryWeight * Steering.GetBoundaryForce(currentPos - boundaryCenter, boundaryDims);
 
            Vector2 combined = separation + cohesion + alignment + boundary;
 
            desiredVelocity = combined.magnitude > 0.001f
                ? combined.normalized * maxSpeed
                : Vector2.zero;
        }
        else if (State == FlyState.Alone)
        {
            Transform nearest = FindNearestFlyAnyDistance();
 
            desiredVelocity = nearest != null
                ? Steering.SeekCore(currentPos, nearest.position, maxSpeed)
                : Vector2.zero;
        }
        else // Fleeing: run directly away from the player
        {
            desiredVelocity = player != null
                ? Steering.FleeCore(currentPos, player.position, maxSpeed)
                : Vector2.zero;
        }
 
        desiredVelocity = ApplyAvoidance(currentPos, desiredVelocity);
 
        Vector2 force = Steering.VelocityToForce(desiredVelocity, rb, accelTime, maxAccel);
        rb.AddForce(force);
    }
 
    private Transform FindNearestFlyAnyDistance()
    {
        Transform nearest = null;
        float nearestDistance = float.MaxValue;
 
        foreach (FlyFSM other in AllFlies)
        {
            if (other == this)
                continue;
 
            float distance = Vector2.Distance(transform.position, other.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = other.transform;
            }
        }
 
        return nearest;
    }
    private Vector2 ApplyAvoidance(Vector2 currentPos, Vector2 desiredVelocity)
    {
        if (!avoidance.Enabled || desiredVelocity == Vector2.zero)
            return desiredVelocity;
 
        Vector2 lookAheadPoint = currentPos + desiredVelocity.normalized * avoidance.LookAheadDistance;
        Vector2 adjustedPoint = Steering.GetAvoidanceTarget(currentPos, lookAheadPoint, avoidance);
 
        Vector2 newDirection = (adjustedPoint - currentPos).normalized;
        return newDirection * desiredVelocity.magnitude;
    }
}