using System;

[AttributeUsage(AttributeTargets.Method)]
public sealed class InspectorButtonAttribute : Attribute
{
    public string Label { get; }
    public bool PlayModeOnly { get; }

    // Optional: the name of a bool field on the same object; the button only shows while it's true
    // ([InspectorButton("Go", ShowIf = nameof(myBool))])
    public string ShowIf { get; set; }

    public InspectorButtonAttribute(string label = null, bool playModeOnly = false)
    {
        Label = label;
        PlayModeOnly = playModeOnly;
    }
}