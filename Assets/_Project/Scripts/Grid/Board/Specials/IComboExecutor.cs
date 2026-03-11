/// <summary>
/// Extended combo interface for combos that need side effects beyond pure cell calculation.
/// Override fan-out, PatchBot dashes, VFX triggers etc. all require mutable access to
/// ResolutionContext and board services.
///
/// If a combo implements both IComboBehavior and IComboExecutor,
/// the dispatcher calls Execute() instead of CalculateAffectedCells().
/// CalculateAffectedCells can still serve as a fallback or preview.
/// </summary>
public interface IComboExecutor
{
    void Execute(ComboExecutionContext ctx);
}

/// <summary>
/// Bundles all dependencies a combo's Execute() might need.
/// Created once per resolution pass by SpecialBehaviorDispatcher.
/// </summary>
public class ComboExecutionContext
{
    public ResolutionContext Resolution;
    public BoardController Board;
    public TileView TileA;
    public TileView TileB;
    public TileSpecial SpecialA;
    public TileSpecial SpecialB;

    // Services
    public SpecialVisualService VisualService;
    public PatchbotComboService PatchbotService;
    public ActivationQueueProcessor QueueProcessor;
}