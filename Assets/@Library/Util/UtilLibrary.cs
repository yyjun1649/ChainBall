using System;
using System.Collections.Generic;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;
using Random = UnityEngine.Random;

namespace Library
{
    public static class UtilLibrary
    {
        public static bool GetChance(double chance)
        {
            if (chance <= 0)
            {
                return false;
            }

            if (chance >= 100)
            {
                return true;
            }

            float randomFloatValue = Random.Range(0f, 100f);

            return chance >= randomFloatValue;
        }

        public static bool GetChance(float chance)
        {
            if (chance <= 0)
            {
                return false;
            }

            if (chance >= 1)
            {
                return true;
            }

            float randomFloatValue = Random.Range(0f, 1);

            return chance >= randomFloatValue;
        }

        public static bool GetChance(int chance)
        {
            if (chance <= 0)
            {
                return false;
            }

            if (chance >= 100)
            {
                return true;
            }

            int randomIntValue = Random.Range(0, 101);

            return chance >= randomIntValue;
        }

        public static float GetGaussianRandom(float mean, float stdDev)
        {
            float rand1 = Random.Range(0.0f, 1.0f);
            float rand2 = Random.Range(0.0f, 1.0f);

            float n = Mathf.Sqrt(-2.0f * Mathf.Log(rand1)) * Mathf.Cos(2.0f * Mathf.PI * rand2);

            return mean + (stdDev * n);
        }

        public static Color HexCodeToColor(string hexCode)
        {
            ColorUtility.TryParseHtmlString(hexCode, out Color color);
            return color;
        }

        public static void Shuffle<T>(this List<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = Random.Range(0, n);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }

        public static int GetRandomIndex(float[] weight)
        {
            float sum = 0f;
            for (int i = 0; i < weight.Length; i++)
            {
                sum += weight[i];
            }

            var random = Random.Range(0, sum);

            var num = 0f;
            for (var i = 0; i < weight.Length; i++)
            {
                num += weight[i];

                if (num >= random)
                {
                    return i;
                }
            }

            return 0;
        }
        
        public static int GetRandomIndexParam(params float[] weight)
        {
            float sum = 0f;
            for (int i = 0; i < weight.Length; i++)
            {
                sum += weight[i];
            }

            var random = Random.Range(0, sum);

            var num = 0f;
            for (var i = 0; i < weight.Length; i++)
            {
                num += weight[i];

                if (num >= random)
                {
                    return i;
                }
            }

            return 0;
        }

        public static int GetRandomIndex(List<Int32> weight)
        {
            int sum = 0;
            for (int i = 0; i < weight.Count; i++)
            {
                sum += weight[i];
            }

            var random = Random.Range(0, sum);

            var num = 0;
            for (var i = 0; i < weight.Count; i++)
            {
                num += weight[i];

                if (num >= random)
                {
                    return i;
                }
            }

            return 0;
        }

        public static int GetRandomIndex(List<float> weight)
        {
            float sum = 0f;
            for (int i = 0; i < weight.Count; i++)
            {
                sum += weight[i];
            }

            var random = Random.Range(0, sum);

            var num = 0f;
            for (var i = 0; i < weight.Count; i++)
            {
                num += weight[i];

                if (num >= random)
                {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>
        /// 컬러 변환
        /// </summary>
        public static Color GetColor(string hex)
        {
            Color color = new();
            ColorUtility.TryParseHtmlString($"#{hex}", out color);
            return color;
        }

        public static string ToRGBHex(Color c)
        {
            return ZString.Format("#{0:X2}{1:X2}{2:X2}", ToByte(c.r), ToByte(c.g), ToByte(c.b));
        }
        private static byte ToByte(float f)
        {
            f = Mathf.Clamp01(f);
            return (byte)(f * 255);
        }

        /// <summary>
        /// 메모리 클린업
        /// </summary>
        public static async void CleanUpMemory()
        {
            await Resources.UnloadUnusedAssets();
            GC.Collect();
        }
        public static async void GCIncremental()
        {
            var completeCount = 0;

            while (completeCount < 5)
            {
                if (GarbageCollector.CollectIncremental(3000000)) completeCount++;

                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }
        public static Vector3 RandomSpherePos(float inRadius, float outRadius)
        {
            var point = UnityEngine.Random.onUnitSphere;
            point.z = 0f;

            var r = UnityEngine.Random.Range(inRadius, outRadius);
            return point * r;
        }
        public static Vector3 TrimZ(this Vector3 vector3)
        {
            vector3.z = 0;
            return vector3;
        }
        public static long DateTimeToUnixTime(DateTime time)
        {
            var origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            TimeSpan span = time - origin;

            return (long) span.TotalSeconds;
        }
        public static DateTime UnixTimeToDateTime(long unixTime)
        {
            var origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);

            return origin.AddSeconds(unixTime);
        }

        public static bool TryGetValue<T>(this List<T> list, T target, out T result)
        {
            result = list.Find(x => x.Equals(target));

            return result != null;
        }
        
        /// <summary>
        /// UI 배열 등에 대해 조건에 맞게 초기화 또는 비활성화 처리를 합니다.
        /// </summary>
        /// <param name="totalCount">총 처리할 항목 수 (예: 배열 길이)</param>
        /// <param name="limitCount">활성화할 항목 수 (데이터 개수)</param>
        /// <param name="init">활성화 항목에 대해 수행할 초기화 함수 (index 전달)</param>
        /// <param name="disable">초과 항목에 대해 수행할 비활성화 함수 (index 전달)</param>
        public static void InitializeOrDisableRange(
            int totalCount,
            int limitCount,
            Action<int> init,
            Action<int> disable)
        {
            for (int i = 0; i < totalCount; i++)
            {
                if (i < limitCount)
                    init?.Invoke(i);
                else
                    disable?.Invoke(i);
            }
        }
    }
}