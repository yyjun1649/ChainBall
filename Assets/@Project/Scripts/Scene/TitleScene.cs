using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Library;
using SpecData;
using UnityEngine;

public class TitleScene : BaseScene
{
    private CancellationTokenSource _cts;

    protected override void OnSceneLoaded()
    {
        _cts = new CancellationTokenSource();
        InitializeAsync(_cts.Token).Forget();
    }

    protected override void OnSceneDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async UniTaskVoid InitializeAsync(CancellationToken cancellationToken)
    {
        // 1. Core handlers: Addressables, Time, screen/quality settings.
        await Handlers.Instance.Initialize(cancellationToken);

        // 2. Spec tables (Resources-backed) → User data (ES3 load + initialize).
        await SpecDataManager.Instance.LoadAllAsync(cancellationToken);
        await UserDataManager.Instance.LoadAllAsync(cancellationToken);

        // 3. Pre-gameplay systems.
        // TODO: apply saved sound volume via Handlers.Sound.
        // TODO: apply locale / language from UserData.
        // TODO: fetch remote config and surface a notice popup if present.

        // 4. Advance to Lobby.
        // TODO: replace with "Tap to Start" flow once the title popup exists.
        await Handlers.Scene.ChangeSceneAsync("Lobby", true, 1f, cancellationToken);
    }
}
