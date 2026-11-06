using UnityEngine;

public enum ShowIfCondition { Or, And }

public class ShowIfAttribute : PropertyAttribute
{
    public string conditionField;
    public object[] conditionValues;
    public ShowIfCondition condition;

    // Single value — existing usage unchanged
    public ShowIfAttribute(string conditionField, object conditionValue)
    {
        this.conditionField = conditionField;
        this.conditionValues = new object[] { conditionValue };
        this.condition = ShowIfCondition.Or;
    }

    // Multiple values with condition type
    public ShowIfAttribute(string conditionField, ShowIfCondition condition, params object[] conditionValues)
    {
        this.conditionField = conditionField;
        this.conditionValues = conditionValues;
        this.condition = condition;
    }
}