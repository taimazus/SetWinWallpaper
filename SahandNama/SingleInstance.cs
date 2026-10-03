namespace SahandNama;

// Keep the named handles alive for the entire UI lifetime. No thread owns the
// mutex, so shutdown and abnormal process termination both release the gate.
internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex gate;
    private readonly EventWaitHandle activation;
    public bool IsPrimary { get; }

    public SingleInstance(string name)
    {
        activation = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".Activate");
        try
        {
            gate = new Mutex(false, name, out var created);
            IsPrimary = created;
        }
        catch
        {
            activation.Dispose();
            throw;
        }
    }

    public void RequestActivation() => activation.Set();
    public bool TakeActivationRequest() => activation.WaitOne(0);

    public void Dispose()
    {
        gate.Dispose();
        activation.Dispose();
    }
}
