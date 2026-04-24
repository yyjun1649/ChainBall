
    using System;
    using System.Threading.Tasks;

    public static class TimeHelper
    {
        public static async void After(float time, Action onFinish)
        {
            await Task.Delay((int)(time * 1000));
            
            onFinish?.Invoke();
        }
    }
