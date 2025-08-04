using System;
using System.Threading.Tasks;

internal class DisposableHandle : IAsyncDisposable
{
    private Func<ValueTask> _dispose;

    public DisposableHandle(Func<ValueTask> dispose)
    {
        _dispose = dispose;
    }

    public ValueTask DisposeAsync()
    {
        return _dispose();
    }
}
