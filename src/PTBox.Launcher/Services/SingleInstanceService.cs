using System.Security.Principal;

namespace PTBox.Launcher.Services;

public sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activation;
    private RegisteredWaitHandle? _wait;
    public bool IsFirst { get; }
    public SingleInstanceService()
    {
        var user = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        _mutex = new Mutex(true, @"Local\PTBox.Launcher." + user, out var created);
        IsFirst = created;
        _activation = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\PTBox.Activate." + user);
    }
    public void Signal() => _activation.Set();
    public void Listen(Action restore) => _wait = ThreadPool.RegisterWaitForSingleObject(_activation, (_, _) => restore(), null, Timeout.Infinite, false);
    public void Dispose()
    {
        _wait?.Unregister(null);
        _activation.Dispose();
        if (IsFirst) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
