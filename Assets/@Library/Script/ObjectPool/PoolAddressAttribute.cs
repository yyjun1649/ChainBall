using System;

namespace Library
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class PoolAddressAttribute : Attribute
    {
        public string Format { get; }

        // Format must contain "{0}" — replaced by poolObjectId at load time.
        public PoolAddressAttribute(string format)
        {
            Format = format;
        }
    }
}
