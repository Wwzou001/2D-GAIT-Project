using System.Collections.Generic;
using UnityEngine;
using SteeringBehaviours;

[RequireComponent(typeof(Rigidbody2D))]
public class SpiderFSM : MonoBehaviour
{
    public enum SpiderState { Flocking, Alone }
    public enum SpiderEvent { JoinedFlock, LostFlock }
 
    public SpiderState State = SpiderState.Alone;
 
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

    private static List<SpiderFSM> AllSpiders = new List<SpiderFSM>();
 
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }
 
    private void OnEnable()
    {
        AllSpiders.Add(this);
    }
 
    private void OnDisable()
    {
        AllSpiders.Remove(this);
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
 
        foreach (SpiderFSM other in AllSpiders)
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
        if (neighbours.Count == 0)
        {
            HandleEvent(SpiderEvent.LostFlock);
        }
        else
        {
            HandleEvent(SpiderEvent.JoinedFlock);
        }
    }
 
    private void HandleEvent(SpiderEvent e)
    {
        if (e == SpiderEvent.LostFlock && State == SpiderState.Flocking)
        {
            State = SpiderState.Alone;
        }
        else if (e == SpiderEvent.JoinedFlock && State == SpiderState.Alone)
        {
            State = SpiderState.Flocking;
        }
    }
 
    private void RunCurrentState()
    {
        Vector2 currentPos = rb.position;
        Vector2 desiredVelocity;
 
        if (State == SpiderState.Flocking)
        {
            Vector2 separation = separationWeight * Steering.GetSeparation(currentPos, neighbours, maxSpeed);
            Vector2 cohesion = cohesionWeight * Steering.GetCohesion(currentPos, neighbours, maxSpeed);
            Vector2 alignment = alignmentWeight * Steering.GetAlignment(neighbours, maxSpeed);
            Vector2 boundary = boundaryWeight * Steering.GetBoundaryForce(currentPos, boundaryDims);
 
            Vector2 combined = separation + cohesion + alignment + boundary;

            desiredVelocity = combined.magnitude > 0.001f
                ? combined.normalized * maxSpeed
                : Vector2.zero;
        }
        else // Alone
        {
            Transform nearest = FindNearestSpiderAnyDistance();
 
            desiredVelocity = nearest != null
                ? Steering.SeekCore(currentPos, nearest.position, maxSpeed)
                : Vector2.zero;
        }
 
        desiredVelocity = ApplyAvoidance(currentPos, desiredVelocity);
 
        Vector2 force = Steering.VelocityToForce(desiredVelocity, rb, accelTime, maxAccel);
        rb.AddForce(force);
    }
 
    private Transform FindNearestSpiderAnyDistance()
    {
        Transform nearest = null;
        float nearestDistance = float.MaxValue;
 
        foreach (SpiderFSM other in AllSpiders)
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