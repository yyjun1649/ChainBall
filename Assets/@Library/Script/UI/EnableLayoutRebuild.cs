using System;
using UnityEngine;
using UnityEngine.UI;

public class EnableLayoutRebuild : MonoBehaviour
{
    private RectTransform _transform;
    
    private void OnEnable()
    {
        _transform ??= GetComponent<RectTransform>();
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(_transform);
    }
}
