using Sigtrap.Relays;
using UnityEngine;

namespace Library
{
    public class EventHandler : MonoBehaviour
    {
        private Relay<eEventType> _onEvent = new Relay<eEventType>();
        
        public void Subscribe(IEvent obj)
        {
            _onEvent.AddListener(obj.HandleEvent);
        }

        public void Unsubscribe(IEvent obj)
        {
            _onEvent.RemoveListener(obj.HandleEvent);
        }

        public void GenerateEvent(eEventType eventType)
        {
            _onEvent.Dispatch(eventType);
        }
    }

    public enum eEventType
    {
        Shop,
        Currency,
        Stat,
        Augments,
        Items,
    }
}
