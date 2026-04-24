using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Library
{
    public class Joystick
    {
        [SerializeField] private Image _imgBackground;
        [SerializeField] private Image _imgController;
        [SerializeField] private Transform _originPos;
        
        public Vector2 TouchPosition { get; private set; }
    
        public void OnPointerDown(PointerEventData eventData)
        {
            _imgBackground.transform.position = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            TouchPosition = Vector2.zero;

            var touchPosition = TouchPosition;
            
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _imgBackground.rectTransform, eventData.position, eventData.pressEventCamera, out touchPosition))
            {
                touchPosition.x = (touchPosition.x / _imgBackground.rectTransform.sizeDelta.x);
                touchPosition.y = (touchPosition.y / _imgBackground.rectTransform.sizeDelta.y);

                TouchPosition = new Vector2(touchPosition.x * 2 - 1, touchPosition.y * 2 - 1);
            
                TouchPosition = (touchPosition.magnitude >1)? touchPosition.normalized : touchPosition;

                _imgController.rectTransform.anchoredPosition = new Vector2(
                    touchPosition.x * _imgBackground.rectTransform.sizeDelta.x * 0.5f,
                    touchPosition.y * _imgBackground.rectTransform.sizeDelta.y * 0.5f);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _imgController.rectTransform.anchoredPosition = Vector2.zero;
            TouchPosition = Vector2.zero;
            
            _imgBackground.transform.position = _originPos.transform.position;
        }

        public float Horizontal()
        {
            return TouchPosition.x;
        }

        public float Vertical()
        {
            return TouchPosition.y;
        }
    }
}