using Library;
using SpecData;
using UnityEngine;

public class GameScene : BaseScene
{
    protected override async void OnSceneLoaded()
    {
        await Handlers.Instance.Initialize();

        await SpecDataManager.Instance.LoadAllAsync();

        var gm = GameManager.Instance;
        gm.Initialize();
        gm.StartBattle();
    }

    protected override void OnSceneDestroy()
    {

    }
}
