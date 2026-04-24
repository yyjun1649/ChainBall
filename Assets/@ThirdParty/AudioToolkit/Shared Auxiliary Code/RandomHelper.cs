using UnityEngine;

namespace CS.Essentials.Math
{
    /// <summary>
    /// Helper for various stuff and also for making it easier to find references to UnityEngine.Random in our code.
    /// </summary>
    public static class RandomHelper
    {
        /// <returns>A new random seed based on DateTime.Now.Ticks.</returns>
        public static int CreateRandomSeed()
        {
            return (int) System.DateTime.Now.Ticks;
        }

        /// <summary>
        /// Seeds the random number generator with <paramref name="seed"/> then computes
        /// a random number inside [<paramref name="minimum"/>, <paramref name="maximum"/>]
        /// (both inclusive) and restores the original seed. Returns the generated number.
        /// </summary>

        public static float RandomRangeWithSeed( int seed, float minimum, float maximum )
        {
            float value;

#if UNITY_5_4_OR_NEWER
            var stateBackup = UnityEngine.Random.state;

            UnityEngine.Random.InitState( seed );
            {
                value = UnityEngine.Random.Range( minimum, maximum );
            }
            UnityEngine.Random.state = stateBackup;
#else
            var seedBackup = UnityEngine.Random.seed;

            UnityEngine.Random.seed = seed;
            {
                value = UnityEngine.Random.Range( minimum, maximum );
            }
            UnityEngine.Random.seed = seedBackup;
#endif

            return value;
        }

        // NOTE: Propeties and methods are intentionally explicit to allow easier debugging
        //       by adding break points or logs into the code.

        public static Quaternion rotation 
        {
            get
            {
                return UnityEngine.Random.rotation;
            }
        }

        public static Vector3 onUnitSphere
        {
            get
            {
                return UnityEngine.Random.onUnitSphere;
            }
        }

        public static Vector2 insideUnitCircle
        {
            get
            {
                return UnityEngine.Random.insideUnitCircle;
            }
        }

        public static Vector3 insideUnitSphere
        {
            get
            {
                return UnityEngine.Random.insideUnitSphere;
            }
        }

        public static Quaternion rotationUniform
        {
            get
            {
                return UnityEngine.Random.rotationUniform;
            }
        }

        public static UnityEngine.Random.State state
        {
            get
            {
                return UnityEngine.Random.state;
            }

            set
            {
                UnityEngine.Random.state = value;
            }
        }

        public static float value
        {
            get
            {
                return UnityEngine.Random.value;
            }
        }

        public static Color ColorHSV( float hueMin, float hueMax, float saturationMin, float saturationMax, float valueMin, float valueMax,
            float alphaMin, float alphaMax )
        {
            var value = UnityEngine.Random.ColorHSV( hueMin, hueMax, saturationMin, saturationMax, valueMin, valueMax, alphaMin, alphaMax );
            
            return value;
        }

        public static Color ColorHSV()
        {
            var value = UnityEngine.Random.ColorHSV();

            return value;
        }

        public static Color ColorHSV( float hueMin, float hueMax )
        {
            var value = UnityEngine.Random.ColorHSV( hueMin, hueMax );

            return value;
        }

        public static Color ColorHSV( float hueMin, float hueMax, float saturationMin, float saturationMax )
        {
            var value = UnityEngine.Random.ColorHSV( hueMin, hueMax, saturationMin, saturationMax );

            return value;
        }

        public static Color ColorHSV( float hueMin, float hueMax, float saturationMin, float saturationMax, float valueMin, float valueMax )
        {
            var value = UnityEngine.Random.ColorHSV( hueMin, hueMax, saturationMin, saturationMax, valueMin, valueMax );

            return value;
        }

        public static void InitState( int seed )
        {
            UnityEngine.Random.InitState( seed );
        }

        public static int Range( int inclusiveMinimum, int exclusiveMaximum )
        {
            var value = UnityEngine.Random.Range( inclusiveMinimum, exclusiveMaximum );

            return value;
        }

        public static float Range( float inclusiveMinimum, float inclusiveMaximum )
        {
            var value = UnityEngine.Random.Range( inclusiveMinimum, inclusiveMaximum );

            return value;
        }
    }
}
