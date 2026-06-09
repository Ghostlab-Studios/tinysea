/// <summary>
/// Group 5: cooperative pause/stop signal shared between SimulationController and the
/// per-day loop in SimulationRunner.Run(). Pausing spins the day loop WITHOUT consuming
/// RNG or advancing state, so a paused-then-resumed run is byte-identical to an
/// uninterrupted run with the same seed. Fields are volatile because scenarios run on
/// Task.Run background threads (Editor/standalone) while pause/stop are toggled from the
/// main thread.
/// </summary>
public class RunControl
{
    public volatile bool Paused;
    public volatile bool Stopped;
}
