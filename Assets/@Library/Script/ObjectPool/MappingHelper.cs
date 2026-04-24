namespace Library
{
    using System.Collections.Generic;
    using UnityEngine;

    public class MappingHelper<T, U>
    {
        private Dictionary<T, U> _dicMapping = new Dictionary<T, U>();

        public void Register(T t, U u)
        {
            if (!_dicMapping.TryGetValue(t, out var a))
            {
                _dicMapping.Add(t, u);
            }
        }

        public void Unregister(T t)
        {
            _dicMapping.Remove(t);
        }

        public U Get(T t)
        {
            return _dicMapping.GetValueOrDefault(t);
        }
        
        public bool TryGet(T t ,out U u)
        {
            if (_dicMapping.TryGetValue(t, out u))
            {
                return true;
            }

            return false;
        }
    }
}