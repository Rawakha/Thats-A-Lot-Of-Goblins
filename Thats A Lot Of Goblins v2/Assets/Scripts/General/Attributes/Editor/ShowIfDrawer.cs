#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(ShowIfAttribute))]
public class ShowIfDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (ShouldShow(property))
            EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return ShouldShow(property)
            ? EditorGUI.GetPropertyHeight(property, label, true)
            : -EditorGUIUtility.standardVerticalSpacing;
    }

    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        SerializedProperty drawnProperty = property.Copy();
        VisualElement root = new VisualElement();
        IMGUIContainer field = new IMGUIContainer(() =>
        {
            drawnProperty.serializedObject.UpdateIfRequiredOrScript();
            float height = EditorGUI.GetPropertyHeight(drawnProperty, true);
            Rect rect = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true));
            EditorGUI.PropertyField(rect, drawnProperty, true);
            drawnProperty.serializedObject.ApplyModifiedProperties();
        });
        root.Add(field);

        void Refresh() => root.style.display = ShouldShow(drawnProperty) ? DisplayStyle.Flex : DisplayStyle.None;
        Refresh();

        SerializedProperty conditionProperty = FindConditionProperty(drawnProperty, ((ShowIfAttribute)attribute).conditionField);
        if (conditionProperty != null)
            root.TrackPropertyValue(conditionProperty, _ => Refresh());

        return root;
    }

    private bool ShouldShow(SerializedProperty property)
    {
        ShowIfAttribute showIf = (ShowIfAttribute)attribute;
        SerializedProperty conditionProperty = FindConditionProperty(property, showIf.conditionField);
        if (conditionProperty == null || conditionProperty.hasMultipleDifferentValues)
            return true;

        if (showIf.condition == ShowIfCondition.And)
        {
            foreach (object value in showIf.conditionValues)
                if (!Matches(conditionProperty, value)) return false;
            return true;
        }

        foreach (object value in showIf.conditionValues)
            if (Matches(conditionProperty, value)) return true;
        return false;
    }

    private static SerializedProperty FindConditionProperty(SerializedProperty property, string fieldName)
    {
        string path = property.propertyPath;
        int lastDot = path.LastIndexOf('.');
        if (lastDot >= 0)
        {
            SerializedProperty sibling = property.serializedObject.FindProperty(path.Substring(0, lastDot + 1) + fieldName);
            if (sibling != null) return sibling;
        }

        return property.serializedObject.FindProperty(fieldName);
    }

    private static bool Matches(SerializedProperty condition, object expected)
    {
        if (expected == null) return false;

        try
        {
            switch (condition.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    return condition.boolValue == Convert.ToBoolean(expected);

                case SerializedPropertyType.Enum:
                    if (expected is Enum enumValue)
                    {
                        int index = condition.enumValueIndex;
                        return index >= 0 && index < condition.enumNames.Length && condition.enumNames[index] == Enum.GetName(enumValue.GetType(), enumValue);
                    }
                    return condition.enumValueIndex == Convert.ToInt32(expected);

                case SerializedPropertyType.Integer:
                    return condition.longValue == Convert.ToInt64(expected);

                case SerializedPropertyType.Float:
                    return Mathf.Approximately(condition.floatValue, Convert.ToSingle(expected));

                case SerializedPropertyType.String:
                    return condition.stringValue == expected.ToString();

                default:
                    return false;
            }
        }
        catch (InvalidCastException) { return false; }
        catch (FormatException) { return false; }
        catch (OverflowException) { return false; }
    }
}
#endif