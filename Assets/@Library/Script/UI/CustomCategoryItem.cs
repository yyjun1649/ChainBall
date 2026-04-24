using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Library
{
    public class CustomCategoryItem : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private GameObject _goOn;
        [SerializeField] private GameObject _goOff;
        [SerializeField] private TextMeshProUGUI _txt;

        private int _index;
        private Action<int> _action;
        
        public void Initialize(int i, Action<int> action)
        {
            _index = i;
            _action = action;
            
            gameObject.SetActive(true);
        }

        public void SetText(string st)
        {
            _txt.text = st;
        }
        
        public void OnPointerClick(PointerEventData eventData)
        {
            _action?.Invoke(_index);
        }

        public void Refresh(int currentIndex)
        {
            if (!gameObject.activeSelf) return;
            
            var isOn = _index == currentIndex;
            _goOn.SetActive(isOn);
            _goOff.SetActive(!isOn);
        }
    }
}