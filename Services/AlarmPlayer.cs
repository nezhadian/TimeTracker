using System.IO;

namespace TimeTracker.Services;

public sealed class AlarmPlayer
{
    public Task PlayBeepAsync()
    {
        var wav = CreateBeepWav(880, 380);
        return Task.Run(() =>
        {
            using var stream = new MemoryStream(wav);
            using var player = new System.Media.SoundPlayer(stream);
            player.PlaySync();
        });
    }

    public static byte[] CreateBeepWav(int frequency, int milliseconds)
    {
        const int sampleRate = 22050;
        var sampleCount = Math.Max(1, sampleRate * milliseconds / 1000);
        var dataLength = sampleCount * 2;
        using var stream = new MemoryStream(44 + dataLength);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + dataLength);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(dataLength);

        var fadeLength = Math.Max(1, sampleRate / 50);
        for (var i = 0; i < sampleCount; i++)
        {
            var fade = 1d;
            if (i < fadeLength)
                fade = i / (double)fadeLength;
            else if (i > sampleCount - fadeLength)
                fade = (sampleCount - i) / (double)fadeLength;

            var sample = Math.Sin(2 * Math.PI * frequency * i / sampleRate);
            writer.Write((short)(sample * fade * 0.35 * short.MaxValue));
        }

        return stream.ToArray();
    }
}
