using System.Buffers.Binary;

namespace Aion2PetTracker.Core.Protocol;

/// <summary>A server-announced drop, not a confirmed inventory receipt.</summary>
public readonly record struct LootDrop(uint ObjectId, float X, float Y, float Z, uint DropId, uint ItemId, uint Count);

public static class LootMessages
{
    public const ushort Drop = 0x1E56;

    // Layout observed in both GPL cube fixtures (2026-10-13), body without the opcode:
    // 01 | object u32 | object xyz f32 | drop xyz f32 | u32 | drop-id u32 | item u32 | count u32 | tail.
    public static bool TryParseDrop(ReadOnlySpan<byte> body, out LootDrop drop)
    {
        drop = default;
        if (body.Length < 45 || body[0] != 1)
            return false;
        var id = BinaryPrimitives.ReadUInt32LittleEndian(body[1..]);
        var x = BinaryPrimitives.ReadSingleLittleEndian(body[5..]);
        var y = BinaryPrimitives.ReadSingleLittleEndian(body[9..]);
        var z = BinaryPrimitives.ReadSingleLittleEndian(body[13..]);
        var dropId = BinaryPrimitives.ReadUInt32LittleEndian(body[33..]);
        var item = BinaryPrimitives.ReadUInt32LittleEndian(body[37..]);
        var count = BinaryPrimitives.ReadUInt32LittleEndian(body[41..]);
        if (id == 0 || dropId == 0 || item == 0 || count is 0 or > 1_000_000_000 || !PositionMessages.IsWorldPosition(x, y, z))
            return false;
        drop = new(id, x, y, z, dropId, item, count);
        return true;
    }
}
