namespace UndercoverLite.Core;

/// <summary>
/// Launcher-side session guard. It does not hook or control the PS2 game.
/// The UI releases the guard only when the started emulator process exits.
/// </summary>
public sealed class GameSessionGate
{
    public bool IsRunning { get; private set; }

    public bool TryEnter()
    {
        if (IsRunning) return false;
        IsRunning = true;
        return true;
    }

    public void Release() => IsRunning = false;
}