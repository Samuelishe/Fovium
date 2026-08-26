using System.Buffers.Binary;
using System.Text;

namespace Fovium.Tests.ColorManagementProbe;

internal static class IccTestData
{
    private const int MatrixProfileSize = 312;

    public static byte[] CreateMinimalProfile(
        int suppliedLength = 132,
        uint? declaredSize = null,
        bool includeSignature = true)
    {
        if (suppliedLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(suppliedLength));
        }

        var bytes = new byte[suppliedLength];
        if (bytes.Length < 4)
        {
            return bytes;
        }

        BinaryPrimitives.WriteUInt32BigEndian(bytes, declaredSize ?? checked((uint)suppliedLength));
        if (bytes.Length < 128)
        {
            return bytes;
        }

        bytes[8] = 4;
        bytes[9] = 0x30;
        WriteSignature(bytes, 12, "mntr");
        WriteSignature(bytes, 16, "RGB ");
        WriteSignature(bytes, 20, "XYZ ");
        if (includeSignature)
        {
            WriteSignature(bytes, 36, "acsp");
        }

        if (bytes.Length >= 132)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(128, 4), 0);
        }

        return bytes;
    }

    public static byte[] CreateMatrixRgbProfile()
    {
        var bytes = CreateMinimalProfile(MatrixProfileSize, MatrixProfileSize);
        WriteS15Fixed16(bytes, 68, 0.9642);
        WriteS15Fixed16(bytes, 72, 1.0);
        WriteS15Fixed16(bytes, 76, 0.8249);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(128, 4), 7);

        WriteTagEntry(bytes, 132, "rXYZ", 216, 20);
        WriteTagEntry(bytes, 144, "gXYZ", 236, 20);
        WriteTagEntry(bytes, 156, "bXYZ", 256, 20);
        WriteTagEntry(bytes, 168, "wtpt", 276, 20);
        WriteTagEntry(bytes, 180, "rTRC", 296, 14);
        WriteTagEntry(bytes, 192, "gTRC", 296, 14);
        WriteTagEntry(bytes, 204, "bTRC", 296, 14);

        WriteXyzTag(bytes, 216, 0.4360747, 0.2225045, 0.0139322);
        WriteXyzTag(bytes, 236, 0.3850649, 0.7168786, 0.0971045);
        WriteXyzTag(bytes, 256, 0.1430804, 0.0606169, 0.7141733);
        WriteXyzTag(bytes, 276, 0.9642, 1.0, 0.8249);
        WriteSignature(bytes, 296, "curv");
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(304, 4), 1);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(308, 2), 0x0233);
        return bytes;
    }

    private static void WriteTagEntry(
        byte[] bytes,
        int tableOffset,
        string signature,
        int dataOffset,
        int dataSize)
    {
        WriteSignature(bytes, tableOffset, signature);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(tableOffset + 4, 4), checked((uint)dataOffset));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(tableOffset + 8, 4), checked((uint)dataSize));
    }

    private static void WriteXyzTag(
        byte[] bytes,
        int offset,
        double x,
        double y,
        double z)
    {
        WriteSignature(bytes, offset, "XYZ ");
        WriteS15Fixed16(bytes, offset + 8, x);
        WriteS15Fixed16(bytes, offset + 12, y);
        WriteS15Fixed16(bytes, offset + 16, z);
    }

    private static void WriteS15Fixed16(byte[] bytes, int offset, double value) =>
        BinaryPrimitives.WriteInt32BigEndian(
            bytes.AsSpan(offset, 4),
            checked((int)Math.Round(value * 65536, MidpointRounding.AwayFromZero)));

    private static void WriteSignature(byte[] bytes, int offset, string value) =>
        Encoding.ASCII.GetBytes(value).CopyTo(bytes, offset);
}
