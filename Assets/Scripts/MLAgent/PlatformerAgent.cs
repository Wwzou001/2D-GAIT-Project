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
    [SerializeField] private LevelRandomizer levelRandomizer; // randomise start/goal direction and obstacle placement each episode
    [SerializeField] private LevelEndFlag levelEndFlag;

    // Raycast observation
    [SerializeField] private float rayLength = 6f;
    [SerializeField] private LayerMask obstacleLayer; // hazards, boxes, ground
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private LayerMask goalLayer;

    [SerializeField] private float groundCheckDistance = 0.3f;
    [SerializeField] private float forwardCheckDistance = 0.6f;
    [SerializeField] private float unjustifiedJumpPenalty = -0.1f;

    [SerializeField] private float fixedJumpPenalty = -0.5f;

    // Diable log for manualy testing
    [SerializeField] private bool logEachStep = false;

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
    private int stepsSinceProgress; // steps since the last new best distance was reached

    [SerializeField] private float maxEpisodeSeconds = 20f; // safety cap per episode

    private bool goalReachedThisEpisode = false;

    private float episodeStartTime;
    [SerializeField] private float episodeStartFreezeDuration = 0.1f;


    public override void Initialize()
    {
        if (controller == null) controller = GetComponent<PlatformerPlayerController>();
        rb = GetComponent<Rigidbody2D>();

        // Tell controller an external source (this Agent) is driving movement
        controller.ExternallyControlled = true;
    }

    public override void OnEpisodeBegin()
    {
        // Cancle jump first
        if (controller != null)
        {
            controller.CancleJump();
        }

        // Randomise goal direction/distacne and obstacle placement before anything below read goalPos
        if (levelRandomizer != null)
        {
            levelRandomizer.RandomiseLevel();
        }

        if (PlatformerGameManager.Instance != null)
        {
            PlatformerGameManager.Instance.ResetForNextEpisode();
        }

        if (logEachStep)
        { 
            Debug.Log("OnEpisodeBegin called! Resetting to: " + (startPosition != null ? startPosition.position.ToString() : "NULL startPosition")); 
        }
        episodeTime = 0f;

        if (levelEndFlag != null)
        {
            levelEndFlag.ResetTrigger();
        }

        goalReachedThisEpisode = false;

        // Reset position/velocity of each game object
        if (startPosition != null)
        {
            rb.position = startPosition.position;
            transform.position = startPosition.position;
        }
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        controller.MoveHorizontal(0f);

        previousX = transform.position.x;
        closestDistanceToGoal = goalPosition != null 
            ? Mathf.Abs(goalPosition.position.x - transform.position.x) 
            + Mathf.Abs(goalPosition.position.y - transform.position.y) * 0.1f
            : float.MaxValue;
        stepsSinceProgress = 0;

        episodeStartTime = Time.time;
    }

    private void FixedUpdate()
    {
        // Episode timeout to prevents an agent do nothing forever
        episodeTime += Time.fixedDeltaTime;
        if (episodeTime > maxEpisodeSeconds)
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

        if (goalPosition != null)
        {
            Vector2 toGoal = goalPosition.position - transform.position;
            sensor.AddObservation(toGoal.x / 20f);
            sensor.AddObservation(toGoal.y / 20f);
            sensor.AddObservation(toGoal.magnitude / 20f);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
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

    // Action -- Discrete branch 0: horizontal move (0 = none, 1 = left, 2 = right), Discrete branch 1: (0 = no, 1 = yes)
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (Time.time - episodeStartTime < episodeStartFreezeDuration) return;

        int moveDir = actions.DiscreteActions[0];
        float moveInput = moveDir == 0 ? 0f : (moveDir == 1 ? -1f : 1f);
        controller.MoveHorizontal(moveInput);

        int jumpInput = actions.DiscreteActions[1];

        if (jumpInput == 1)
        {
            bool didJump = controller.TryJump();

            if (didJump) 
            {
                if (controller.UsesFixedDistanceJump)
                {
                    AddReward(fixedJumpPenalty);
                }
                else
                {
                    bool justified = IsJumpJustified();
                    if (!justified)
                    {
                        AddReward(unjustifiedJumpPenalty);
                    }
                    if (logEachStep)
                    { 
                        Debug.Log(justified ? "Jump: justified, no penalty" : $"Jump: NOT justified, penalty {unjustifiedJumpPenalty}"); 
                    }
                }
            }
        }

        if (logEachStep)
        { 
            Debug.Log($"Move: {moveInput}, Jump: {jumpInput}, Pos: {transform.position}"); 
        }

        // Small per-step penalty (time penalty)
        AddReward(-0.0005f);

        // Small reward for forward progress
        if (goalPosition != null)
        {
            float dx = Mathf.Abs(goalPosition.position.x - transform.position.x);
            float dy = Mathf.Abs(goalPosition.position.y - transform.position.y);
            float distanceToGoal = dx + dy * 0.1f;
            if (distanceToGoal < closestDistanceToGoal)
            {
                float newProgress = closestDistanceToGoal - distanceToGoal;
                AddReward(newProgress * 1f);
                closestDistanceToGoal = distanceToGoal;
                stepsSinceProgress = 0;
            }
            else
            {
                stepsSinceProgress++;
                //if (stepsSinceProgress % 200 == 0)
                //{ 
                //    AddReward(-0.5f); 
                //}
            }
        }
        previousX = transform.position.x;
    }

    // Check whether there is a physical reason to jump right now: ground ahead/below doesn't continue
    // (a gap,ledge, platform edge), or an obstacle directly in front that block walking
    private bool IsJumpJustified()
    {
        float facing = Mathf.Sign(transform.localScale.x);

        Vector3 feetPosition = controller.GroundCheckPoint != null ? controller.GroundCheckPoint.position : transform.position;
        
        // Is the ground directly below about to end?
        Vector2 groundCheckOrigin = feetPosition + new Vector3(facing * 0.4f, 0f, 0f);
        bool groundContinuesAhead = Physics2D.Raycast(groundCheckOrigin, Vector2.down, groundCheckDistance, obstacleLayer);

        // Is there something solid directly ahead at foot height, block a walk through?
        bool obstacleAhead = Physics2D.Raycast(feetPosition, new Vector2(facing, 0f), forwardCheckDistance, obstacleLayer);

        return !groundContinuesAhead || obstacleAhead;
    }

    private void OnDrawGizmosSelected()
    {
        float facing = Application.isPlaying ? Mathf.Sign(transform.localScale.x) : 1f;

        Vector3 feetPosition = (controller != null && controller.GroundCheckPoint.position != null) ? controller.GroundCheckPoint.position : transform.position;

        Vector2 groundCheckOrigin = feetPosition + new Vector3(facing * 0.4f, 0f, 0f);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(groundCheckOrigin, groundCheckOrigin + Vector2.down * groundCheckDistance);
        Gizmos.DrawWireSphere(groundCheckOrigin, 0.03f);

        Gizmos.color = Color.blue;
        Vector2 forwardEnd = (Vector2)feetPosition + new Vector2(facing, 0f) * forwardCheckDistance;
        Gizmos.DrawLine(feetPosition, forwardEnd);
    }

    // Lets human test the agent's action space manually (Behavior Type: Heuristic Only)
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        int moveDir = 0;
        bool jumpPressed = false;

        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveDir = 1;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveDir = 2;
            jumpPressed = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
        }

        discreteActions[0] = moveDir;
        discreteActions[1] = jumpPressed ? 1 : 0;
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
        if (goalReachedThisEpisode) return;
        goalReachedThisEpisode = true;

        Debug.Log("OnGoalReached called!");
        AddReward(20f);
        EndEpisode();
    }

}
