// A component that is allowed on a mesh inside a bake group's Editable Sources: Level_Block, Level_Cliff, and any future
// tool that makes its own mesh. The baker only knows this interface, so it doesn't depend on any one tool.
// Setup: implement it on the tool. GC: editor only; no runtime members, ticks or subscriptions.
public interface Level_IBakeSource
{
#if UNITY_EDITOR
    // Make sure the mesh exists and is current, just before it's copied
    void PrepareForBake();

    // Changes whenever the mesh would change. Saved with the bake to spot stale sources, so it must survive a restart.
    string BakeKey { get; }
#endif
}
