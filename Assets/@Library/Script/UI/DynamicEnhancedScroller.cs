using System;
using System.Collections.Generic;
using EnhancedUI.EnhancedScroller;
using Sigtrap.Relays;
using UnityEngine;
using UnityEngine.UI;

namespace Library
{
    [RequireComponent(typeof(EnhancedScroller))]
    public class DynamicEnhancedScroller : MonoBehaviour, IEnhancedScrollerDelegate
    {
        [SerializeField] protected EnhancedScroller _enhancedScroller;
        [SerializeField] protected int _cellCountInRows = 1;
        [SerializeField] protected bool _isReverse;

        protected EnhancedScrollerCellView _slotPrefab;
        protected List<object> _dataList = new List<object>();
        protected float _slotSize;
        
        protected Relay<EnhancedScrollerCellView, object> _onCellBind = new Relay<EnhancedScrollerCellView, object>();
        protected Relay<EnhancedScrollerCellView, List<object>> _onRowBind = new Relay<EnhancedScrollerCellView, List<object>>();

        public void Initialize<T>(EnhancedScrollerCellView slotPrefab, List<T> dataList, 
                                  Action<EnhancedScrollerCellView, T> onCellBind, 
                                  Action<EnhancedScrollerCellView, List<T>> onRowBind = null, 
                                  bool stayScrollPosition = false)
        {
            if (dataList == null)
            {
                Debug.LogError($"[Enhanced Scroller] Data({typeof(T).Name}) is null");
                return;
            }

            _slotPrefab = slotPrefab;

            _dataList.Clear();

            foreach (object data in dataList)
            {
                _dataList.Add(data);
            }

            if (_onCellBind != null)
            {
                _onCellBind.AddListener((cellView, data) => onCellBind(cellView, (T)data));
            }

            if (onRowBind != null)
            {
                _onRowBind.AddListener((cellView, data) => onRowBind(cellView, data.ConvertAll(d => (T)d)));
            }

            if (_slotSize <= 0)
            {
                var slotRect = _slotPrefab.GetComponent<RectTransform>();
                _slotSize = _enhancedScroller.scrollDirection == EnhancedScroller.ScrollDirectionEnum.Vertical
                    ? slotRect.rect.height
                    : slotRect.rect.width;
            }

            _enhancedScroller.Delegate = this;
            if (stayScrollPosition)
            {
                _enhancedScroller.ReloadData(_enhancedScroller.NormalizedScrollPosition);
            }
            else
            {
                _enhancedScroller.ReloadData(!_isReverse ? 0 : 1);
            }
        }

        public int GetNumberOfCells(EnhancedScroller scroller)
        {
            return (_dataList == null) ? 0 : Mathf.CeilToInt(_dataList.Count / (float)_cellCountInRows);
        }

        public float GetCellViewSize(EnhancedScroller scroller, int dataIndex)
        {
            return _slotSize;
        }

        public EnhancedScrollerCellView GetCellView(EnhancedScroller scroller, int dataIndex, int cellIndex)
        {
            var cellView = scroller.GetCellView(_slotPrefab);

            if (_cellCountInRows > 1)
            {
                int startIndex = dataIndex * _cellCountInRows;
                int getRangeCount = Mathf.Min(_dataList.Count - startIndex, _cellCountInRows);
                _onRowBind.Dispatch(cellView, _dataList.GetRange(startIndex, getRangeCount));
            }
            else
            {
                _onCellBind?.Dispatch(cellView, _dataList[dataIndex]);
            }

            return cellView;
        }

        public void Refresh()
        {
            _enhancedScroller.RefreshActiveCellViews();
        }

        public void OnValidate()
        {
#if UNITY_EDITOR
            _enhancedScroller = GetComponent<EnhancedScroller>();
#endif
        }
    }
}
