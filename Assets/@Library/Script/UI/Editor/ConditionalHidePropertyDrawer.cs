using UnityEngine;
using UnityEditor;
using System;
using System.Linq;

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ConditionalHideAttribute))]
public class ConditionalHidePropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        ConditionalHideAttribute condHAtt = (ConditionalHideAttribute)attribute;
        bool enabled = GetConditionalHideAttributeResult(condHAtt, property);

        bool wasEnabled = GUI.enabled;
        GUI.enabled = enabled;
        if (enabled)
        {
            EditorGUI.PropertyField(position, property, label, true);
        }

        GUI.enabled = wasEnabled;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        ConditionalHideAttribute condHAtt = (ConditionalHideAttribute)attribute;
        bool enabled = GetConditionalHideAttributeResult(condHAtt, property);

        if (enabled)
        {
            return EditorGUI.GetPropertyHeight(property, label);
        }
        else
        {
            return -EditorGUIUtility.standardVerticalSpacing;
        }
    }

    private bool GetConditionalHideAttributeResult(ConditionalHideAttribute condHAtt, SerializedProperty property)
    {
        bool enabled = true;
        string propertyPath = property.propertyPath;
        string conditionPath = propertyPath.Replace(property.name, condHAtt.ConditionalSourceField);
        SerializedProperty sourcePropertyValue = property.serializedObject.FindProperty(conditionPath);

        if (sourcePropertyValue != null)
        {
            enabled = CheckPropertyType(sourcePropertyValue, condHAtt.CompareValues);
            if (condHAtt.Inverse) enabled = !enabled;
        }
        else
        {
            Debug.LogWarning($"Attempting to use ConditionalHideAttribute but no matching SourcePropertyValue found in object: {condHAtt.ConditionalSourceField}");
        }

        return enabled;
    }

    private bool CheckPropertyType(SerializedProperty sourcePropertyValue, string[] compareValues)
    {
        string currentValue = "";
        
        switch (sourcePropertyValue.propertyType)
        {
            case SerializedPropertyType.Boolean:
                currentValue = sourcePropertyValue.boolValue.ToString();
                break;
            case SerializedPropertyType.Enum:
                currentValue = sourcePropertyValue.enumNames[sourcePropertyValue.enumValueIndex];
                break;
            case SerializedPropertyType.Integer:
                currentValue = sourcePropertyValue.intValue.ToString();
                break;
            case SerializedPropertyType.Float:
                currentValue = sourcePropertyValue.floatValue.ToString();
                break;
            case SerializedPropertyType.String:
                currentValue = sourcePropertyValue.stringValue;
                break;
            case SerializedPropertyType.ObjectReference:
                currentValue = (sourcePropertyValue.objectReferenceValue != null).ToString();
                break;
            default:
                Debug.LogError("Data type of the property used for conditional hiding [" + sourcePropertyValue.propertyType + "] is currently not supported");
                return true;
        }

        return compareValues.Contains(currentValue);
    }
}
#endif