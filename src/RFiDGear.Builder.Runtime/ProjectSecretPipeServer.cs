using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace RFiDGear.Builder.Runtime;

public sealed record ProjectSecretPipeDescriptor(string PipeName, int ServerProcessId);

public sealed class ProjectSecretPipeServer : IDisposable
{
    private readonly IReadOnlyDictionary<string, string> secrets;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task serverTask;
    private volatile int expectedClientProcessId;
    private bool disposed;

    private ProjectSecretPipeServer(IReadOnlyDictionary<string, string> secrets)
    {
        this.secrets = new Dictionary<string, string>(secrets, StringComparer.Ordinal);
        Descriptor = new ProjectSecretPipeDescriptor("SieEncode.Secrets.v1." + Guid.NewGuid().ToString("N"), Environment.ProcessId);
        serverTask = Task.Run(ServeAsync);
    }

    public ProjectSecretPipeDescriptor Descriptor { get; }

    public static ProjectSecretPipeServer Start(IReadOnlyDictionary<string, string> secrets)
    {
        ArgumentNullException.ThrowIfNull(secrets);
        if (secrets.Count == 0) throw new ArgumentException("Die Secret-Sitzung enthält keine Schlüsselreferenzen.", nameof(secrets));
        return new ProjectSecretPipeServer(secrets);
    }

    public void AuthorizeClient(int processId)
    {
        if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId));
        expectedClientProcessId = processId;
    }

    private async Task ServeAsync()
    {
        while (!cancellation.IsCancellationRequested)
        {
            using var pipe = new NamedPipeServerStream(Descriptor.PipeName, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await pipe.WaitForConnectionAsync(cancellation.Token).ConfigureAwait(false);
                var expected = expectedClientProcessId;
                if (expected <= 0 || !TryGetClientProcessId(pipe, out var actual) || actual != expected)
                {
                    pipe.Disconnect();
                    continue;
                }

                using var reader = new StreamReader(pipe, leaveOpen: true);
                using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
                var line = await reader.ReadLineAsync(cancellation.Token).ConfigureAwait(false);
                var request = JsonSerializer.Deserialize<SecretRequest>(line ?? "") ?? new SecretRequest("");
                var found = secrets.TryGetValue(request.Reference, out var secret);
                await writer.WriteLineAsync(JsonSerializer.Serialize(new SecretResponse(found, found ? secret : null,
                    found ? null : "Unbekannte oder nicht freigegebene Schlüsselreferenz."))).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (IOException) when (!cancellation.IsCancellationRequested) { }
        }
    }

    private static bool TryGetClientProcessId(NamedPipeServerStream pipe, out int processId)
    {
        processId = 0;
        if (!OperatingSystem.IsWindows() || !GetNamedPipeClientProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out var id)) return false;
        processId = checked((int)id);
        return processId > 0;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        cancellation.Cancel();
        try { serverTask.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
        cancellation.Dispose();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(IntPtr pipe, out uint clientProcessId);

    private sealed record SecretRequest(string Reference);
    private sealed record SecretResponse(bool Success, string? Secret, string? Error);
}
