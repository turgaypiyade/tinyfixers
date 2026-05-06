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

        while (actionQueue.Count > 0)
        {
            BoardAction action = actionQueue.Dequeue();

            if (action.Blocking)
            {
                yield return StartCoroutine(action.ExecuteVisuals(this));
            }
            else
            {
                StartCoroutine(action.ExecuteVisuals(this));
            }
        }

        IsPlaying = false;

        // Let the controller know the visual sequence is finished,
        // so it can check for falls, collapses, or level end states.
        Board.OnActionSequenceFinished();
    }
}
