using System.Buffers.Binary;

namespace Aion2PetTracker.Core.Pets;

/// <summary>Original readers from elkaida's pinned MIT protocol documentation (see licenses/pets).
/// The dispatcher strips the opcode first. Crystals, loot, damage and kills are never souls.</summary>
public static class PetSoulProtocol
{
    public const ushort Registered = 0x0990, LevelChanged = 0x1490;
    public static bool TryStage(ReadOnlySpan<byte> body, out int pet, out int level)
    {
        pet = 0;
        level = 0;
        if (body.Length is < 10 or > 64)
            return false;
        uint id = BinaryPrimitives.ReadUInt32LittleEndian(body[2..]), stage = BinaryPrimitives.ReadUInt32LittleEndian(body[6..]);
        if (id is < 1000 or > 99999 || stage is < 1 or > 3)
            return false;
        pet = (int)id;
        level = (int)stage;
        return true;
    }
    public const ushort Collection = 0x0090, Gained = 0x0D90;
    public static bool TryCollection(ReadOnlySpan<byte> b, out Dictionary<int, long> totals)
        => TryCollection(b, out totals, out _);
    public static bool TryCollection(ReadOnlySpan<byte> b, out Dictionary<int, long> totals, out int tailOffset)
    {
        totals = [];
        tailOffset = 0;
        int p = 4;
        if (b.Length is < 6 or > 65535 || !Count(b, ref p, out var count) || count > (b.Length - p) / 12)
            return false;
        var levels = new Dictionary<int, int>();
        var points = new Dictionary<int, int>();
        for (int i = 0; i < count; i++)
        {
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(b[p..]), repeat = BinaryPrimitives.ReadUInt32LittleEndian(b[(p + 4)..]), level = BinaryPrimitives.ReadUInt32LittleEndian(b[(p + 8)..]);
            p += 12;
            if (id == 0 || id > int.MaxValue || repeat != id || level > 3 || !levels.TryAdd((int)id, (int)level))
                return false;
        }
        if (!Count(b, ref p, out count) || count > (b.Length - p) / 8)
            return false;
        for (int i = 0; i < count; i++)
        {
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(b[p..]), point = BinaryPrimitives.ReadUInt32LittleEndian(b[(p + 4)..]);
            p += 8;
            if (id == 0 || id > int.MaxValue || point > 74 || !points.TryAdd((int)id, (int)point))
                return false;
        }
        foreach (int id in levels.Keys.Union(points.Keys))
        {
            int level = levels.GetValueOrDefault(id), point = points.GetValueOrDefault(id);
            int goal = level switch
            {
                0 => 5,
                1 => 25,
                2 => 75,
                _ => 1
            };
            if (point >= goal)
                return false;
            totals[id] = (level switch
            {
                0 => 0,
                1 => 5,
                2 => 30,
                _ => 105
            }) + point;
        }
        tailOffset = p;
        return true; // subsequent fields are not interpreted as individual pet progress
    }
    private static bool Count(ReadOnlySpan<byte> b, ref int p, out int count)
    {
        count = 0;
        uint value = 0;
        int start = p;
        for (int shift = 0; shift <= 28 && p < b.Length; shift += 7)
        {
            byte c = b[p++];
            if (shift == 28 && (c & 0xF0) != 0)
                return false;
            value |= (uint)(c & 127) << shift;
            if (c < 128)
            {
                if (value > 512 || (p - start > 1 && c == 0))
                    return false;
                count = (int)value;
                return true;
            }
        }
        return false;
    }
    public static bool TryGains(ReadOnlySpan<byte> b, out List<SoulGain> gains)
    {
        gains = [];
        if (b.Length < 9 || b[0] is < 1 or > 127 || b.Length != 1 + b[0] * 8)
            return false;
        for (int p = 1; p < b.Length; p += 8)
        {
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(b[p..]), amount = BinaryPrimitives.ReadUInt32LittleEndian(b[(p + 4)..]);
            if (id == 0 || id > int.MaxValue || amount is 0 or >= 1000)
                return false;
            gains.Add(new((int)id, (int)amount));
        }
        return true;
    }
}
