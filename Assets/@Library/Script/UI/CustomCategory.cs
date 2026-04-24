using System;
using System.Collections.Generic;
using UnityEngine;

namespace Library
{
    public class CustomCategory : MonoBehaviour
    {
        [SerializeField] private List<CustomCategoryItem> items;

        private Action<int> _onClick;

        private int currentIndex = 0;

        public int CurrentIndex => currentIndex;

        public void Initialize(int itemCount, Action<int> onClick)
        {
            _onClick = onClick;
            
            for (int i = 0; i < items.Count; i++)
            {
                if (i >= itemCount)
                {
                    items[i].gameObject.SetActive(false);
                    continue;
                }
                
                items[i].Initialize(i,OnClick);
            }

            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < items.Count; i++)
            {
                items[i].Refresh(currentIndex);
            }
        }

        public void OnClick(int i)
        {
            currentIndex = i;
            
            _onClick?.Invoke(i);
            
            Refresh();
        }
    }
}