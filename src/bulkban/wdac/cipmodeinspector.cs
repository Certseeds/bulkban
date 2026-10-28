namespace bulkban.Wdac;

/// <summary>
/// 从 WDAC .cip 二进制里读取 audit/enforce 模式。
/// WDAC 策略头的 option flags 位于偏移 0x26,最低位为 Audit Mode。
/// 该偏移与位在 4 个组的审计/强制产物上实测一致。
/// </summary>
internal static class CipModeInspector
{
    internal const int OptionsOffset = 0x26;
    internal const byte AuditBit = 0x01;

    internal static PolicyMode? Detect(string cipPath)
    {
        using var stream = File.OpenRead(cipPath);
        if (stream.Length <= OptionsOffset)
        {
            return null;
        }

        stream.Position = OptionsOffset;
        var value = stream.ReadByte();
        if (value < 0)
        {
            return null;
        }

        return (value & AuditBit) != 0 ? PolicyMode.Audit : PolicyMode.Enforce;
    }
}
