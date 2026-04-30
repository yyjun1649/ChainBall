using System;
using UnityEngine;

namespace Library
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class PoolCanvasAttribute : Attribute
    {
        public RenderMode Mode { get; }

        public PoolCanvasAttribute(RenderMode mode = RenderMode.WorldSpace)
        {
            Mode = mode;
        }
    }
}
