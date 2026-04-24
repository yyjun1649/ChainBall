using System;
using UnityEngine;

public static class DateTimeUtil
{
    /// <summary>
    /// Unix Timestamp(초 단위)를 "HH:mm:ss" 형태로 변환
    /// </summary>
    public static string UnixTimestampToTimeString(long unixTimestamp)
    {
        DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).DateTime;
        return dateTime.ToString("HH:mm:ss");
    }

    /// <summary>
    /// Unix Timestamp(밀리초 단위)를 "HH:mm:ss" 형태로 변환
    /// </summary>
    public static string UnixTimestampMillisToTimeString(long unixTimestampMillis)
    {
        DateTime dateTime = DateTimeOffset.FromUnixTimeMilliseconds(unixTimestampMillis).DateTime;
        return dateTime.ToString("HH:mm:ss");
    }

    /// <summary>
    /// Unix Timestamp(밀리초 단위)를 "mm:ss" 형태로 변환
    /// </summary>
    public static string ConvertToTimeFormatMMSS(long totalSeconds)
    {
        int minutes = (int)(totalSeconds / 60);
        int seconds = (int)(totalSeconds % 60);

        return $"{minutes:D2}:{seconds:D2}";
    }

    /// <summary>
    /// 경과 시간(초)을 "HH:mm:ss" 형태로 변환
    /// </summary>
    public static string SecondsToTimeString(long totalSeconds)
    {
        TimeSpan timeSpan = TimeSpan.FromSeconds(totalSeconds);
        return timeSpan.ToString(@"hh\:mm\:ss");
    }

    /// <summary>
    /// 경과 시간(밀리초)을 "HH:mm:ss" 형태로 변환
    /// </summary>
    public static string MillisecondsToTimeString(long totalMilliseconds)
    {
        TimeSpan timeSpan = TimeSpan.FromMilliseconds(totalMilliseconds);
        return timeSpan.ToString(@"hh\:mm\:ss");
    }

    /// <summary>
    /// Ticks를 "HH:mm:ss" 형태로 변환
    /// </summary>
    public static string TicksToTimeString(long ticks)
    {
        TimeSpan timeSpan = new TimeSpan(ticks);
        return timeSpan.ToString(@"hh\:mm\:ss");
    }

    /// <summary>
    /// 경과 시간을 "HH:mm:ss.fff" (밀리초 포함) 형태로 변환
    /// </summary>
    public static string MillisecondsToTimeStringWithMillis(long totalMilliseconds)
    {
        TimeSpan timeSpan = TimeSpan.FromMilliseconds(totalMilliseconds);
        return timeSpan.ToString(@"hh\:mm\:ss\.fff");
    }

    /// <summary>
    /// Unity의 Time.time (float)을 long으로 변환 후 시분초 문자열로 변환
    /// </summary>
    public static string UnityTimeToString()
    {
        long totalMilliseconds = (long)(Time.time * 1000);
        return MillisecondsToTimeString(totalMilliseconds);
    }

    /// <summary>
    /// 현재 시간을 "HH:mm:ss" 형태로 반환
    /// </summary>
    public static string CurrentTimeString()
    {
        return DateTime.Now.ToString("HH:mm:ss");
    }

    /// <summary>
    /// DateTime을 Unix Timestamp(초 단위)로 변환
    /// 서버 통신에서 가장 많이 사용되는 방식
    /// </summary>
    public static long ToUnixTimestamp(DateTime dateTime)
    {
        return ((DateTimeOffset)dateTime).ToUnixTimeSeconds();
    }

    /// <summary>
    /// DateTime을 Unix Timestamp(밀리초 단위)로 변환
    /// JavaScript와의 호환성이나 더 정밀한 시간이 필요할 때
    /// </summary>
    public static long ToUnixTimestampMilliseconds(DateTime dateTime)
    {
        return ((DateTimeOffset)dateTime).ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// DateTime을 Ticks로 변환
    /// .NET 내부적으로 가장 정밀한 시간 표현
    /// </summary>
    public static long ToTicks(DateTime dateTime)
    {
        return dateTime.Ticks;
    }

    /// <summary>
    /// DateTime을 FileTime으로 변환 (Windows 파일 시스템 호환)
    /// </summary>
    public static long ToFileTime(DateTime dateTime)
    {
        return dateTime.ToFileTime();
    }

    /// <summary>
    /// UTC DateTime을 Unix Timestamp로 변환 (권장 방식)
    /// 타임존 문제를 피하기 위해 항상 UTC 사용 권장
    /// </summary>
    public static long ToUnixTimestampUTC(DateTime dateTime)
    {
        if (dateTime.Kind != DateTimeKind.Utc)
        {
            dateTime = dateTime.ToUniversalTime();
        }

        return ((DateTimeOffset)dateTime).ToUnixTimeSeconds();
    }

    /// <summary>
    /// 현재 시간을 Unix Timestamp로 변환
    /// </summary>
    public static long CurrentUnixTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    /// <summary>
    /// 현재 시간을 Unix Timestamp(밀리초)로 변환
    /// </summary>
    public static long CurrentUnixTimestampMilliseconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// DateTime을 총 초 단위로 변환 (1970-01-01부터의 경과 시간)
    /// Unix Timestamp와 동일하지만 double 정밀도 유지
    /// </summary>
    public static long ToTotalSeconds(DateTime dateTime)
    {
        return (long)(dateTime - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
    }

    /// <summary>
    /// DateTime을 총 밀리초 단위로 변환
    /// </summary>
    public static long ToTotalMilliseconds(DateTime dateTime)
    {
        return (long)(dateTime - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
    }

    /// <summary>
    /// Unity의 현재 게임 시간을 long으로 변환
    /// </summary>
    public static long UnityTimeToLong()
    {
        return (long)(Time.time * 1000); // 밀리초 단위
    }

    /// <summary>
    /// 두 DateTime 간의 차이를 밀리초로 반환
    /// </summary>
    public static long GetTimeDifferenceInMilliseconds(DateTime start, DateTime end)
    {
        return (long)(end - start).TotalMilliseconds;
    }
}