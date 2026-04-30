using System;
using System.Threading;

public sealed class SingleInstanceGate : IDisposable
{
    private const string DefaultName = @"Local\MediaPlayerGlobalHotkeys.Singleton";

    private readonly Mutex mutex;
    private bool disposed;

    private SingleInstanceGate(Mutex mutex)
    {
        this.mutex = mutex;
    }

    public static string GetDefaultName()
    {
        return DefaultName;
    }

    public static SingleInstanceGate TryAcquireDefault()
    {
        return TryAcquire(DefaultName);
    }

    public static SingleInstanceGate TryAcquire(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A mutex name is required.", "name");
        }

        bool createdNew;
        Mutex localMutex = null;

        try
        {
            localMutex = new Mutex(true, name, out createdNew);

            if (!createdNew)
            {
                localMutex.Dispose();
                return null;
            }

            return new SingleInstanceGate(localMutex);
        }
        catch
        {
            if (localMutex != null)
            {
                localMutex.Dispose();
            }

            throw;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        try
        {
            mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }

        mutex.Dispose();
    }
}
