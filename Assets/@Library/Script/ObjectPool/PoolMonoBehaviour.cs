using UnityEngine;

namespace Library
{
    public abstract class PoolMonoBehaviour<T> : MonoBehaviour where T : PoolMonoBehaviour<T>
    {
        public int poolObjectId { get; internal set; }
        internal ObjectPoolBase<T> SourcePool;

        // Addressable key format. Must contain "{0}" — replaced by poolObjectId at load time.
        // Read once on an inactive prototype instance, so Awake/Start state must not be required.
        protected internal abstract string AddressFormat { get; }

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
