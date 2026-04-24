
    using System;
    using Library;
    using UnityEngine;

    public abstract class EventMonoBehaviour : MonoBehaviour, IEvent
    {
        protected virtual void OnEnable()
        {
            Handlers.Event.Subscribe(this);
        }

        protected virtual void OnDisable()
        {
            Handlers.Event.Unsubscribe(this);
        }

        protected virtual void OnDestroy()
        {
            Handlers.Event.Unsubscribe(this);
        }

        public abstract void HandleEvent(eEventType eventType);
    }
