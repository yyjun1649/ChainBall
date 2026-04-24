using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property |
                AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public class ConditionalHideAttribute : PropertyAttribute
{
    public string ConditionalSourceField { get; private set; }
    public string[] CompareValues { get; private set; }
    public bool HideInInspector { get; private set; }
    public bool Inverse { get; private set; }

    /// <summary>
    /// 조건부로 Inspector에서 필드를 숨기거나 표시하는 속성 (단일 값)
    /// </summary>
    /// <param name="conditionalSourceField">비교할 필드 이름</param>
    /// <param name="compareValue">비교할 값</param>
    /// <param name="hideInInspector">true면 조건 충족시 숨김, false면 조건 충족시 표시</param>
    /// <param name="inverse">조건을 반전시킬지 여부</param>
    public ConditionalHideAttribute(string conditionalSourceField, string compareValue, bool hideInInspector = true, bool inverse = false)
    {
        ConditionalSourceField = conditionalSourceField;
        CompareValues = new string[] { compareValue };
        HideInInspector = hideInInspector;
        Inverse = inverse;
    }

    /// <summary>
    /// 조건부로 Inspector에서 필드를 숨기거나 표시하는 속성 (다중 값)
    /// </summary>
    /// <param name="conditionalSourceField">비교할 필드 이름</param>
    /// <param name="compareValues">비교할 값들</param>
    /// <param name="hideInInspector">true면 조건 충족시 숨김, false면 조건 충족시 표시</param>
    /// <param name="inverse">조건을 반전시킬지 여부</param>
    public ConditionalHideAttribute(string conditionalSourceField, string[] compareValues, bool hideInInspector = true, bool inverse = false)
    {
        ConditionalSourceField = conditionalSourceField;
        CompareValues = compareValues;
        HideInInspector = hideInInspector;
        Inverse = inverse;
    }

    /// <summary>
    /// 조건부로 Inspector에서 필드를 숨기거나 표시하는 속성 (정수 비교용)
    /// </summary>
    /// <param name="conditionalSourceField">비교할 필드 이름</param>
    /// <param name="compareValue">비교할 정수 값</param>
    /// <param name="hideInInspector">true면 조건 충족시 숨김, false면 조건 충족시 표시</param>
    /// <param name="inverse">조건을 반전시킬지 여부</param>
    public ConditionalHideAttribute(string conditionalSourceField, int compareValue, bool hideInInspector = true, bool inverse = false)
    {
        ConditionalSourceField = conditionalSourceField;
        CompareValues = new string[] { compareValue.ToString() };
        HideInInspector = hideInInspector;
        Inverse = inverse;
    }
}