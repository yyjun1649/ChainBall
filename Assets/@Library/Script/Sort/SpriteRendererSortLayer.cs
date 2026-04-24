
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    public class SpriteRendererSortLayer : MonoBehaviour
    {
        public bool isUpdate = true;

        private SpriteRenderer[] _spriteRenderers;
        private List<int> defaultSortingOrder = new List<int>();

        private Transform _transform;

        private void Awake()
        {
            _spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
            _transform = transform;
            for (int i = 0; i < _spriteRenderers.Length; i++)
            {
                defaultSortingOrder.Add(_spriteRenderers[i].sortingOrder);
            }

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
            var pos = _transform.position;

            var value = Mathf.CeilToInt(pos.z * -1 * 100 + pos.y * -1 * 100);

            for (int i = 0; i < _spriteRenderers.Length; i++)
            {
                _spriteRenderers[i].sortingOrder = value + defaultSortingOrder[i];
            }
        }

        private void OnEnable()
        {
            SetLayer();
        }
    }

