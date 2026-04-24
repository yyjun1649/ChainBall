using System.Threading;
using Cysharp.Threading.Tasks;
using Library;

namespace SpecData
{
    public partial class SpecDataManager : Singleton<SpecDataManager>
    {
        public async UniTask LoadAllAsync(CancellationToken cancellationToken = default)
        {
            if (IsLoaded) return;

            // Yield once so the caller's frame can render a loading state before the sync Resources.Load burst.
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            LoadAll();
        }
    }
}
