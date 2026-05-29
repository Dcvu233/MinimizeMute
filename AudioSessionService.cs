using NAudio.CoreAudioApi;
using System.Diagnostics;

namespace MinimizeMute;

internal sealed class AudioSessionService : IDisposable
{
    private readonly MMDeviceEnumerator deviceEnumerator = new();
    private readonly Dictionary<string, List<AudioSnapshot>> snapshotsByProcessName = new(StringComparer.OrdinalIgnoreCase);

    public void MuteProcessFamily(string processName)
    {
        if (!snapshotsByProcessName.TryGetValue(processName, out var snapshots))
        {
            snapshots = new List<AudioSnapshot>();
            snapshotsByProcessName[processName] = snapshots;
        }

        var knownSessionIds = snapshots
            .Select(snapshot => snapshot.SessionInstanceId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var session in GetSessionsForProcessName(processName))
        {
            if (knownSessionIds.Contains(session.GetSessionInstanceIdentifier))
            {
                continue;
            }

            var volume = session.SimpleAudioVolume;
            snapshots.Add(new AudioSnapshot(session.GetSessionInstanceIdentifier, volume.Volume, volume.Mute));
            volume.Mute = true;
        }
    }

    public void RestoreProcessFamily(string processName)
    {
        if (!snapshotsByProcessName.Remove(processName, out var snapshots))
        {
            return;
        }

        foreach (var snapshot in snapshots)
        {
            var session = GetAllSessions()
                .FirstOrDefault(candidate => candidate.GetSessionInstanceIdentifier == snapshot.SessionInstanceId);
            if (session is null)
            {
                continue;
            }

            var volume = session.SimpleAudioVolume;
            volume.Volume = snapshot.Volume;
            volume.Mute = snapshot.WasMuted;
        }
    }

    public void RestoreAll()
    {
        foreach (var processName in snapshotsByProcessName.Keys.ToList())
        {
            RestoreProcessFamily(processName);
        }
    }

    public IReadOnlySet<string> MutedProcessNames => snapshotsByProcessName.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

    private IEnumerable<AudioSessionControl> GetSessionsForProcessName(string processName)
    {
        foreach (var session in GetAllSessions())
        {
            if (IsProcessName(session.GetProcessID, processName))
            {
                yield return session;
            }
        }
    }

    private static bool IsProcessName(uint processId, string processName)
    {
        try
        {
            if (processId > int.MaxValue)
            {
                return false;
            }

            using var process = Process.GetProcessById((int)processId);
            return string.Equals(process.ProcessName, processName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private IEnumerable<AudioSessionControl> GetAllSessions()
    {
        using var device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var sessions = device.AudioSessionManager.Sessions;

        for (var i = 0; i < sessions.Count; i++)
        {
            yield return sessions[i];
        }
    }

    public void Dispose()
    {
        RestoreAll();
        deviceEnumerator.Dispose();
    }

    private sealed record AudioSnapshot(string SessionInstanceId, float Volume, bool WasMuted);
}
