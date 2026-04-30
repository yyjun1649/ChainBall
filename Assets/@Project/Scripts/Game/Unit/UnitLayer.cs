using UnityEngine;

public static class UnitLayer
{
    private static int _userLayer = -1;

    public static int UserLayer
    {
        get
        {
            if (_userLayer < 0)
            {
                _userLayer = LayerMask.NameToLayer("User");
            }

            return _userLayer;
        }
    }
}
