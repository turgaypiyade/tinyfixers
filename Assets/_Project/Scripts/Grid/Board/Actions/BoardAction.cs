using System.Collections;

public abstract class BoardAction
{
    // Indicates if the sequencer should wait for this action to finish before playing the next one
    public virtual bool Blocking => true;
    
    // Executes the visual animation phase of this action
    // State/Logic changes must have already been completed before this method is called.
    public abstract IEnumerator ExecuteVisuals(ActionSequencer sequencer);
}
