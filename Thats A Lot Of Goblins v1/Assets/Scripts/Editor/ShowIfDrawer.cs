using UnityEditor;
using UnityEngine;
using System.Reflection;

[CustomPropertyDrawer(typeof(ShowIfAttribute))]
public class ShowIfDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (ShouldShow(property))
            EditorGUI.PropertyField(position, property, label);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!ShouldShow(property))
            return 0f;

        return EditorGUI.GetPropertyHeight(property, label);
    }

    private bool ShouldShow(SerializedProperty property)
    {
        ShowIfAttribute showIf = (ShowIfAttribute)attribute;

        SerializedProperty conditionProperty = property.serializedObject.FindProperty(showIf.conditionField);
        if (conditionProperty == null) return true;

        bool Matches(object value)
        {
            switch (conditionProperty.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    return conditionProperty.boolValue.Equals(value);
                case SerializedPropertyType.Enum:
                    return conditionProperty.enumValueIndex.Equals((int)value);
                case SerializedPropertyType.Integer:
                    return conditionProperty.intValue.Equals(value);
                default:
                    return true;
            }
        }

        if (showIf.condition == ShowIfCondition.Or)
        {
            foreach (var value in showIf.conditionValues)
                if (Matches(value)) return true;
            return false;
        }
        else // And
        {
            foreach (var value in showIf.conditionValues)
                if (!Matches(value)) return false;
            return true;
        }
    }
}