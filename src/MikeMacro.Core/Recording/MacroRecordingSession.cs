using MikeMacro.Core.Models;

namespace MikeMacro.Core.Recording;

public sealed class MacroRecordingSession(IInputCapture capture, MacroRecorder recorder) : IDisposable
{
    private bool disposed;

    public event EventHandler<CapturedInputEventArgs>? ActionCaptured;

    public bool IsRecording => recorder.IsRecording;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsRecording)
        {
            throw new InvalidOperationException("A recording is already active.");
        }

        recorder.Start();
        capture.InputCaptured += OnInputCaptured;
        try
        {
            capture.Start();
        }
        catch
        {
            capture.InputCaptured -= OnInputCaptured;
            throw;
        }
    }

    public Macro Stop(string name, int repeatCount = 1)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!IsRecording)
        {
            throw new InvalidOperationException("No recording is active.");
        }

        capture.Stop();
        capture.InputCaptured -= OnInputCaptured;
        return recorder.Stop(name, repeatCount);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        if (IsRecording)
        {
            capture.Stop();
            capture.InputCaptured -= OnInputCaptured;
        }

        capture.Dispose();
        disposed = true;
    }

    private void OnInputCaptured(object? sender, CapturedInputEventArgs args)
    {
        recorder.Record(args.Action, args.TimestampMilliseconds);
        ActionCaptured?.Invoke(this, args);
    }
}