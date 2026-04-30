using UnityEngine;

namespace Library
{
    public abstract class PoolMonoBehaviour<T> : MonoBehaviour where T : PoolMonoBehaviour<T>
    {
        public int poolObjectId;
        internal ObjectPoolBase<T> SourcePool;

        // Addressable key format is declared via [PoolAddress("Foo_{0}")] on the concrete
        // (or nearest base) class. Read once via reflection by ObjectPoolBase, so no instance
        // is needed and no Awake/Start lifecycle is touched.

        public void Release()
        {
            if (SourcePool == null)
            {
                Destroy(gameObject);
                return;
            }
            SourcePool.Release(poolObjectId, (T)this);
        }

        public virtual void OnGet()
        {
            gameObject.SetActive(true);
        }

        public virtual void OnRelease()
        {
            gameObject.SetActive(false);
        }
    }
}
