using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

// ML-Agents wrapper around the existing PlatformerPlayerController.
[RequireComponent(typeof(PlatformerPlayerController))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlatformerAgent : Agent
{
    // References
    [SerializeField] private PlatformerPlayerController controller;
    [SerializeField] private Transform startPosition; // where to reset the agent at the start of each episode
    [SerializeField] private Transform goalPosition; // the level end flag -- use to reward getting closer

    // Raycast observation
    [SerializeField] private float rayLength = 6f;
    [SerializeField] private LayerMask obstacleLayer; // hazards, boxes, ground
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private LayerMask goalLayer;

    // Angles in degrees, 0 = facing right (forward), positive = up.
    private static readonly float[] ForwardAngles = { -15f, -7f, 0f, 7f, 15f };
    // Steeper downward angles (pit detection)
    private static readonly float[] DownwardAngles = { -30f, -45f, -60f };
    // Sparse upward angles (overhead threats)
    private static readonly float[] UpwardAngles = { 45f, 65f };

    private Rigidbody2D rb;
    private float episodeTime;
    private float previousX;
    private float closestDistanceToGoal; // shortest distance to goal achieved so far this episode

    [SerializeField] private float maxEpisodeSectonds = 20f; // safety cap per episode

    public override void Initialize()
    {
        if (controller == null) controller = GetComponent<PlatformerPlayerController>();
        rb = GetComponent<Rigidbody2D>();

        // Tell controller an external source (this Agent) is driving movement
        controller.ExternallyControlled = true;
    }

    public override void OnEpisodeBegin()
    {
        Debug.Log("OnEpisodeBegin called! Resetting to: " + (startPosition != null ? startPosition.position.ToString() : "NULL startPosition"));
        episodeTime = 0f;

        // Reset position/velocity of each game object
        if (startPosition != null)
        {
            rb.position = startPosition.position;
            transform.position = startPosition.position;
        }
        rb.linearVelocity = Vector2.zero;
        previousX = transform.position.x;
        closestDistanceToGoal = goalPosition != null 
            ? Vector2.Distance(transform.position, goalPosition.position) : float.MaxValue;
    }

    private void FixedUpdate()
    {
        // Episode timeout to prevents an agent do nothing forever
        episodeTime += Time.fixedDeltaTime;
        if (episodeTime > maxEpisodeSectonds)
        {
            Debug.Log("Episode ended: TIMEOUT");
            AddReward(-0.5f); // ran out of time -- treat as a soft failure
            EndEpisode();
        }
    }

    // Observations
    public override void CollectObservations(VectorSensor sensor)
    {
        // Facing direction affects which way the forward/downward rays point
        float facing = Mathf.Sign(transform.localScale.x);

        foreach (float angle in ForwardAngles)
        {
            AddRayObservation(sensor, angle, facing);
        }
        foreach (float angle in DownwardAngles)
        {
            AddRayObservation(sensor, angle, facing);
        }
        foreach (float angle in UpwardAngles)
        {
            AddRayObservation(sensor, angle, facing);
        }

        // Physical state
        sensor.AddObservation(rb.linearVelocity); // 2 floats: current velocity (x, y)
        sensor.AddObservation(controller.IsGrounded); // 1 float (bool -> 0/1): grounded or airborne
    }

    // Cast one ray at the given angle and add two observation values:
    // normalised distance to the nearest hit (1 = nothing hit within range)
    // hit type (0 = nothing, 0.5 = obstacle/hazard, 1 = enemy)
    private void AddRayObservation(VectorSensor sensor, float angleDegrees, float facing)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(rad) * facing, Mathf.Sin(rad));

        RaycastHit2D obstacleHit = Physics2D.Raycast(transform.position, direction, rayLength, obstacleLayer);
        RaycastHit2D enemyHit = Physics2D.Raycast(transform.position, direction, rayLength, enemyLayer);
        RaycastHit2D goalHit = Physics2D.Raycast(transform.position, direction, rayLength, goalLayer);

        RaycastHit2D closestHit = default;
        float hitTypeValue = 0f; // nothing hit

        bool goalCloser = goalHit.collider != null &&
            (obstacleHit.collider == null || goalHit.distance <= obstacleHit.distance) &&
            (enemyHit.collider == null || goalHit.distance <= enemyHit.distance);

        bool obstacleCloser = !goalCloser && obstacleHit.collider != null && 
            (enemyHit.collider == null || obstacleHit.distance <= enemyHit.distance);

        if (goalCloser)
        {
            closestHit = goalHit;
            hitTypeValue = -1f;
        }
        else if (obstacleCloser)
        {
            closestHit = obstacleHit;
            hitTypeValue = 0.5f;
        }
        else if (enemyHit.collider != null)
        {
            closestHit = enemyHit;
            hitTypeValue = 1f;
        }

        float normalisedDistance = closestHit.collider != null 
            ? closestHit.distance / rayLength : 1f; // nothing in range -- explicit default, not left undefined

        sensor.AddObservation(normalisedDistance);
        sensor.AddObservation(hitTypeValue);
    }

    // Action -- Countinuous branch 0: horizontal move (-1 to 1), Discrete branch 0: (0 = no, 1 = yes)
    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveInput = actions.ContinuousActions[0];
        controller.MoveHorizontal(moveInput);

        int jumpInput = actions.DiscreteActions[0];
        if (jumpInput == 1)
        {
            controller.TryJump();
            AddReward(-0.001f);
        }

        Debug.Log($"Move: {moveInput}, Jump: {jumpInput}, Pos: {transform.position}");

        // Small per-step penalty (time penalty)
        AddReward(-0.0005f);

        // Small reward for forward progress
        if (goalPosition != null)
        {
            float distanceToGoal = Vector2.Distance(transform.position, goalPosition.position);
            if (distanceToGoal < closestDistanceToGoal)
            {
                float newProgress = closestDistanceToGoal - distanceToGoal;
                AddReward(newProgress * 1f);
                closestDistanceToGoal = distanceToGoal;
            }
        }
        previousX = transform.position.x;
    }

    // Lets human test the agent's action space manually (Behavior Type: Heuristic Only)
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        var discreteActions = actionsOut.DiscreteActions;

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        float horizontal = 0f;
        bool jumpPressed = false;

        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal += 1f;
            jumpPressed = keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame;
        }

        continuousActions[0] = horizontal;
        discreteActions[0] = jumpPressed ? 1 : 0;
    }

    // Reward hooks
    public void OnHazardHit()
    {
        Debug.Log("Episode ended: HAZARD HIT at " + transform.position);
        AddReward(-1f);
        EndEpisode();
    }

    public void OnEnemyHit()
    {
        AddReward(-1f);
        EndEpisode();
    }

    public void OnCoinCollected()
    {
        AddReward(0.1f);
    }

    public void OnGoalReached()
    {
        Debug.Log("OnGoalReached called!");
        AddReward(20f);
        EndEpisode();
    }

}
