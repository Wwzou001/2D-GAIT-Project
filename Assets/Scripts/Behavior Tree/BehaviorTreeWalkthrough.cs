using System.Collections.Generic;
using UnityEngine;

public static class BehaviorTreeWalkthrough
{
    public static bool Enabled { get; set; } = false;

    public static bool IsPlaying { get; private set; } = false;

    public static float BeforeDelay { get; set; } = 0.75f;
    public static float AfterDelay { get; set; } = 0.5f;


    private enum StepType
    {
        Enter,
        Exit
    }


    private struct Step
    {
        public BehaviorTree.Node node;
        public BehaviorTree.Node.State state;
        public StepType type;

        public Step(
            BehaviorTree.Node node,
            BehaviorTree.Node.State state,
            StepType type)
        {
            this.node = node;
            this.state = state;
            this.type = type;
        }
    }


    private static readonly Queue<Step> steps = new();

    private static Step currentStep;

    private static float timer;


    public static void EnterNode(BehaviorTree.Node node)
    {
        if (!Enabled)
            return;

        steps.Enqueue(new Step(node, BehaviorTree.Node.State.Running, StepType.Enter));

    }


    public static void ExitNode(
        BehaviorTree.Node node,
        BehaviorTree.Node.State result)
    {
        if (!Enabled)
            return;

        steps.Enqueue(new Step(node, result, StepType.Exit));

    }


    public static void Begin()
    {
        if (!Enabled)
            return;

        if (steps.Count == 0)
        {
            IsPlaying = false;
            return;
        }

        IsPlaying = true;

        LoadNextStep();
    }

    public static void Update(float deltaTime)
    {
        if (!IsPlaying)
            return;

        timer += deltaTime;


        float delay =
            currentStep.type == StepType.Enter
                ? BeforeDelay
                : AfterDelay;


        if (timer < delay)
            return;


        if (steps.Count > 0)
        {
            LoadNextStep();
        }
        else
        {
            Finish();
        }
    }


    private static void LoadNextStep()
    {
        currentStep = steps.Dequeue();

        timer = 0f;


        currentStep.node.SetDisplayState(
            currentStep.state
        );


    }


    private static void Finish()
    {
        IsPlaying = false;

        timer = 0f;

    }


    public static void Clear()
    {
        steps.Clear();

        IsPlaying = false;

        timer = 0f;

    }
}