using System.Buffers.Binary;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace EldenBingo.Export;

internal static class LocalJsonWire
{
    public const int MaximumSnapshotBytes = 512 * 1024;
    public static string PipeName(int port) => "EldenBingo.LocalJson.v1." + port + "." +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserDomainName + "/" + Environment.UserName)))[..16];

    public static async Task WriteAsync(Stream stream, string text, CancellationToken token)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        if (bytes.Length > MaximumSnapshotBytes + 100) throw new InvalidDataException("The live JSON snapshot is too large.");
        var length = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        await stream.WriteAsync(length, token).ConfigureAwait(false);
        await stream.WriteAsync(bytes, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }

    public static async Task<string> ReadAsync(Stream stream, CancellationToken token)
    {
        async Task readAll(byte[] buffer)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(offset), token).ConfigureAwait(false);
                if (read == 0) throw new EndOfStreamException();
                offset += read;
            }
        }
        var header = new byte[4];
        await readAll(header).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length < 0 || length > MaximumSnapshotBytes + 100) throw new InvalidDataException("Invalid local JSON message size.");
        var bytes = new byte[length];
        await readAll(bytes).ConfigureAwait(false);
        return Encoding.UTF8.GetString(bytes);
    }
}
