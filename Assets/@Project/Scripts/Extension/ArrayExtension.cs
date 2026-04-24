using System;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

public static class ArrayExtension
{
    public static T GetRandomElement<T>(this T[] array)
    {
        if (array.Length > 0)
        {
            return array[Random.Range(0, array.Length)];
        }

        return default(T);
    }
    public static T GetRandomElement<T>(this IReadOnlyList<T> array)
    {
        if (array.Count > 0)
        {
            return array[Random.Range(0, array.Count)];
        }

        return default(T);
    }

    public static T GetRandomElement<T>(this IReadOnlyList<T> array, float[] weight)
    {
        if (array.Count > 0 &&
            array.Count == weight.Length)
        {
            float sum = 0f;
            for (int i = 0; i < weight.Length; i++)
            {
                sum += weight[i];
            }

            var rand = Random.Range(0, sum);

            for (var i = 0; i < array.Count; i++)
            {
                sum -= weight[i];

                if (sum <= rand)
                {
                    return array[i];
                }
            }

            return array[Random.Range(0, array.Count)];
        }

        return default(T);
    }

    public static T GetRandomElement<T>(this T[] array, float[] weight)
    {
        if (array.Length > 0 &&
            array.Length == weight.Length)
        {
            float sum = 0f;
            for (int i = 0; i < weight.Length; i++)
            {
                sum += weight[i];
            }

            var rand = Random.Range(0, sum);

            for (var i = 0; i < array.Length; i++)
            {
                sum -= weight[i];

                if (sum <= rand)
                {
                    return array[i];
                }
            }

            return array[Random.Range(0, array.Length)];
        }

        return default(T);
    }
    
    public static T GetRandomElement<T>(this T[] array, List<float> weight)
    {
        if (array.Length > 0 &&
            array.Length == weight.Count)
        {
            float sum = 0f;
            for (int i = 0; i < weight.Count; i++)
            {
                sum += weight[i];
            }

            var rand = Random.Range(0, sum);

            for (var i = 0; i < array.Length; i++)
            {
                sum -= weight[i];

                if (sum <= rand)
                {
                    return array[i];
                }
            }

            return array[Random.Range(0, array.Length)];
        }

        return default(T);
    }
    
    public static T GetRandomElement<T>(this List<T> array, List<float> weight)
    {
        if (array.Count > 0 &&
            array.Count == weight.Count)
        {
            // 가중치 합산
            float sum = 0;
            for (int i = 0; i < weight.Count; i++)
            {
                sum += weight[i];
            }

            var rand = Random.Range(0, sum);

            for (var i = 0; i < array.Count; i++)
            {
                sum -= weight[i];

                if (sum <= rand)
                {
                    return array[i];
                }
            }

            return array[Random.Range(0, array.Count)];
        }

        return default(T);
    }
    
    public static int GetRandomElementIndex(this List<float> weight)
    {
        if (weight.Count > 0)
        {
            // 가중치 합산
            float sum = 0;
            for (int i = 0; i < weight.Count; i++)
            {
                sum += weight[i];
            }

            var rand = Random.Range(0, sum);

            for (var i = 0; i < weight.Count; i++)
            {
                sum -= weight[i];

                if (sum <= rand)
                {
                    return i;
                }
            }

            return Random.Range(0, weight.Count);
        }

        return 0;
    }

    public static T GetRandomElement<T>(this List<T>  array)
    {
        if (array.Count > 0)
        {
            return array[Random.Range(0, array.Count)];
        }

        return default(T);
    }
    

    public static List<T> GetRandomNotDuplicateElement<T>(this List<T> array, int count , List<T> savedlist)
    {
        if (array == null || array.Count <= 0)
        {
            return null;
        }

        if (array.Count < count)
        {
            return array;
        }
        
        do
        {
            var random = array.GetRandomElement();

            if (!savedlist.Contains(random))
            {
                savedlist.Add(random);
            }
        } while (savedlist.Count < count);

        return savedlist;
    }
}
