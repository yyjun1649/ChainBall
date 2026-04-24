
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    public class ParticleRendererSortLayer : MonoBehaviour
    {
        public bool isUpdate = true;
        private ParticleSystemRenderer[] _particleSystemRenderers;
        private List<int> defaultSortingOrder = new List<int>();

        private void Awake()
        {
            _particleSystemRenderers = GetComponentsInChildren<ParticleSystemRenderer>();

            for (int i = 0; i < _particleSystemRenderers.Length; i++)
            {
                defaultSortingOrder.Add(_particleSystemRenderers[i].sortingOrder);
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
            var value = Mathf.CeilToInt(transform.position.z * -1 * 100 + transform.position.y * -1 * 100);

            for (int i = 0; i < _particleSystemRenderers.Length; i++)
            {
                _particleSystemRenderers[i].sortingOrder = value + defaultSortingOrder[i];
            }
        }
    }

