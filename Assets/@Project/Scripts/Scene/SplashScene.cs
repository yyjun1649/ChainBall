using Cysharp.Threading.Tasks;
using Library;

public class SplashScene : BaseScene
{
    protected override void OnSceneLoaded()
    {
        Handlers.Scene.ChangeSceneAsync("Title").Forget();
    }

    protected override void OnSceneDestroy()
    {

    }
}
