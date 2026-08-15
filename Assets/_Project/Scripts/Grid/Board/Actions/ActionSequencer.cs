using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActionSequencer : MonoBehaviour
{
    public BoardController Board { get; private set; }

    // Gives actions access to the animator to play specific visual effects
    public BoardAnimator Animator => Board.boardAnimatorRef;

    private Queue<BoardAction> actionQueue = new Queue<BoardAction>();
    public bool IsPlaying { get; private set; }

    // Non-blocking actions are started with StartCoroutine and NOT awaited (see PlaySequence),
    // so IsPlaying can flip false in the finally while their visuals are still running. That
    // hole is exactly why the decoupled-resolve overlap gate needed a blunt whole-resolve special
    // lock: a detached fall could start while a non-blocking special sweep tail was still animating.
    // This counter exposes those in-flight detached action coroutines so the overlap gate can wait
    // for them precisely. IsPlaying semantics are intentionally UNCHANGED (blast radius); this is a
    // separate additive signal.
    private int detachedActionsInFlight;
    public int DetachedActionsInFlight => detachedActionsInFlight;

    // True only when the sequencer queue is fully drained AND no detached (non-blocking) action
    // coroutine is still running. Distinct from !IsPlaying, which ignores detached tails.
    public bool AllActionsSettled => !IsPlaying && detachedActionsInFlight == 0;

    public void Initialize(BoardController controller)
    {
        Board = controller;
    }

    public void Enqueue(BoardAction action)
    {
        actionQueue.Enqueue(action);
        if (!IsPlaying)
        {
            IsPlaying = true;
            StartCoroutine(PlaySequence());
        }
    }

    public void Enqueue(IEnumerable<BoardAction> actions)
    {
        foreach (var a in actions)
        {
            actionQueue.Enqueue(a);
        }
        if (!IsPlaying && actionQueue.Count > 0)
        {
            IsPlaying = true;
            StartCoroutine(PlaySequence());
        }
    }

    // Inserts actions at the FRONT of the queue so they play before any already-queued actions.
    // Used by PatchBot combos so the LineH/V fires immediately on arrival, before cascade.
    public void EnqueueFront(IEnumerable<BoardAction> actions)
    {
        var incoming = new List<BoardAction>(actions);
        if (incoming.Count == 0) return;

        var remaining = new List<BoardAction>(actionQueue);
        actionQueue.Clear();
        foreach (var a in incoming)  actionQueue.Enqueue(a);
        foreach (var a in remaining) actionQueue.Enqueue(a);

        if (!IsPlaying && actionQueue.Count > 0)
        {
            IsPlaying = true;
            StartCoroutine(PlaySequence());
        }
    }

    private IEnumerator PlaySequence()
    {
        // IsPlaying is already set to true by Enqueue() — no race window
        try
        {
            while (actionQueue.Count > 0)
            {
                BoardAction action = actionQueue.Dequeue();

                if (action.Blocking)
                {
                    yield return StartCoroutine(action.ExecuteVisuals(this));
                }
                else
                {
                    StartCoroutine(RunDetached(action));
                }
            }
        }
        finally
        {
            IsPlaying = false;
            Board.OnActionSequenceFinished();
        }
    }

    // Wraps a non-blocking action so its still-running visual is counted in DetachedActionsInFlight.
    // OnActionSequenceFinished timing is intentionally left on the queue-drain (IsPlaying) semantics;
    // this only lets callers that care (overlap gate) observe detached tails.
    private IEnumerator RunDetached(BoardAction action)
    {
        detachedActionsInFlight++;
        try
        {
            yield return action.ExecuteVisuals(this);
        }
        finally
        {
            detachedActionsInFlight = Mathf.Max(0, detachedActionsInFlight - 1);
        }
    }
}
