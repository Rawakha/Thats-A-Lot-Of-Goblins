using System;

[AttributeUsage(AttributeTargets.Method)]
public sealed class InspectorButtonAttribute : Attribute
{
    public string Label { get; }
    public bool PlayModeOnly { get; }

    public InspectorButtonAttribute(string label = null, bool playModeOnly = false)
    {
        Label = label;
        PlayModeOnly = playModeOnly;
    }
}