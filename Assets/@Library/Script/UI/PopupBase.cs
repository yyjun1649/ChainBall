
    using System;
    using System.ComponentModel;
    using Library;
    using MoreMountains.Feedbacks;
    using Sigtrap.Relays;
    using UnityEngine;

    public abstract class PopupBase : MonoBehaviour ,IPopup, IEvent
    {
        public bool IsOnStack;

        protected bool _isActive;
        public bool IsActive => _isActive;

        private Relay<PopupBase> OnShow = new Relay<PopupBase>();

        private Relay<PopupBase> OnHide = new Relay<PopupBase>();

        [SerializeField] protected Canvas _canvas;

        private MMF_Player _feelPlayer;
        private bool _feelPlayerResolved;

        public void SetAction(Action<PopupBase> onShow, Action<PopupBase> onHide)
        {
            OnShow.RemoveAll();
            OnHide.RemoveAll();
            OnShow.AddListener(onShow);
            OnHide.AddListener(onHide);
        }

        public void SetSortIndex(int index)
        {
            _canvas.sortingOrder = index;
        }

        public virtual void Show()
        {
            _isActive = true;

            gameObject.SetActive(true);

            var player = ResolveFeelPlayer();
            if (player != null)
            {
                player.PlayFeedbacks();
            }

            OnShow?.Dispatch(this);

            Handlers.Event.Subscribe(this);
        }

        public virtual void Hide()
        {
            _isActive = false;

            if (_feelPlayer != null)
            {
                _feelPlayer.StopFeedbacks();
            }

            gameObject.SetActive(false);

            OnHide?.Dispatch(this);

            Handlers.Event.Unsubscribe(this);
        }

        private MMF_Player ResolveFeelPlayer()
        {
            if (!_feelPlayerResolved)
            {
                _feelPlayer = GetComponentInChildren<MMF_Player>(includeInactive: true);
                _feelPlayerResolved = true;
            }
            return _feelPlayer;
        }

        private void Reset()
        {
#if UNITY_EDITOR
            _canvas = GetComponent<Canvas>();
#endif
        }

        public abstract void HandleEvent(eEventType eventType);
    }
