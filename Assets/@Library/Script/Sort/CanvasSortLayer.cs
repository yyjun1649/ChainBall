
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    public class CanvasSortLayer : MonoBehaviour
    {
        public int defaultValue;
        public bool isUpdate = true;
        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            
            SetLayer();
        }

        private void Update()
        {
            if (!isUpdate)
            {
                return;
            }

            SetLayer();
        }

        private void SetLayer()
        {
            var pos = transform.position;

            pos.y = 0;
            
            var value = Mathf.CeilToInt(pos.z * -1 * 100 + pos.y * -1 * 100);

            _canvas.sortingOrder = value + defaultValue;
        }
    }

