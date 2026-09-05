using Wildfire.Core;

namespace Wildfire.Timberborn.Runtime;

internal interface ITimberbornFireChangeQueue
{
    int RegisteredChangeCountSinceLastDispatch { get; }
    void RegisterChange(FireSimChange change, string source, bool shouldLog = true);
    void LogRegisteredChanges(string source, int count);
}

internal interface ITimberbornQaWorld : ITimberbornFireChangeQueue
{
    FireGrid RequireInitializedGrid();
    IReadOnlyList<TimberbornImportedFieldTarget> ImportedTargets { get; }
    uint? LastTick { get; }
}
