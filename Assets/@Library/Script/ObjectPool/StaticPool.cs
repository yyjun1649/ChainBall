using System;
using System.Collections.Generic;

public class StaticPool<T> where T : IDisposable, new()
{
    private readonly Queue<T> pool = new Queue<T>();
    private readonly int maxCapacity;

    public StaticPool(int capacity = 10)
    {
        maxCapacity = capacity;
    }

    public T Get()
    {
        if (pool.Count > 0)
        {
            return pool.Dequeue();
        }
        else
        {
            return new T();
        }
    }
    
    public void Get(out T item)
    {
        if (pool.Count > 0)
        {
            item = pool.Dequeue();
        }
        else
        {
            item = new T();
        }
    }

    public void Release(T item)
    {
        if (pool.Count < maxCapacity)
        {
            pool.Enqueue(item);
        }
        else
        {
            item.Dispose();
        }
    }
}