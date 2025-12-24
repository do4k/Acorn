namespace Acorn.Extensions;

public static class ByteExtensions
{
    public static ReadOnlyMemory<byte> AsReadOnly(this IEnumerable<byte> bytes)
    {
        var byteArray = bytes.ToArray();
        return new ReadOnlyMemory<byte>(byteArray);
    }
}