namespace VoiceInput.App.Startup;

/// <summary>
/// Enforces single-instance application behaviour using a named system Mutex.
/// The first process to construct this guard acquires the mutex; subsequent
/// processes will find <see cref="IsFirstInstance"/> is <c>false</c> and should exit.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string DefaultMutexName = @"Global\AetherVoiceService";

    private readonly Mutex _mutex;
    private readonly bool _isOwner;
    private bool _disposed;

    /// <summary>
    /// Gets a value indicating whether this process is the first (and only) instance.
    /// When <c>false</c> the application should exit immediately.
    /// </summary>
    public bool IsFirstInstance => _isOwner;

    /// <summary>
    /// Initialises the guard and attempts to acquire the named mutex.
    /// </summary>
    /// <param name="mutexName">
    /// The name of the system mutex. Defaults to <c>Global\VoiceInputService</c>.
    /// </param>
    public SingleInstanceGuard(string mutexName = DefaultMutexName)
    {
        _mutex = new Mutex(initiallyOwned: true, name: mutexName, createdNew: out _isOwner);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_isOwner)
            _mutex.ReleaseMutex();

        _mutex.Dispose();
    }
}
