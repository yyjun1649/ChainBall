using UnityEngine;

public static class UtilCode
    {        
        /// <summary>
        /// 방향벡터를 각도(도)로 변환 (0~360도)
        /// </summary>
        public static float VectorToAngle(Vector2 direction)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        
            // 0~360도 범위로 변환
            if (angle < 0)
                angle += 360f;
            
            return angle;
        }
    
        /// <summary>
        /// 방향벡터를 각도(도)로 변환 (-180~180도)
        /// </summary>
        public static float VectorToAngleSigned(Vector2 direction)
        {
            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }
    
        /// <summary>
        /// 각도를 방향벡터로 변환
        /// </summary>
        public static Vector2 AngleToVector(float angleDegrees)
        {
            float angleRad = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
        }
    }
