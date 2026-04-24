
    using UnityEngine;

    public static class UnitLayer
    {
        private static LayerMask _userLayer;
        
        public static LayerMask UserLayer
        {
            get
            {
                if (_userLayer == 0)
                {
                    _userLayer = LayerMask.NameToLayer("User");
                }

                return _userLayer;
            }
        }
    }
