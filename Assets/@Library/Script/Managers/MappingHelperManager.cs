using UnityEngine;

namespace Library
{
    public class MappingHelperManager : Singleton<MappingHelperManager>
    {
        public MappingHelper<Collider2D, UnitController> Unit = new MappingHelper<Collider2D, UnitController>();
    }
}