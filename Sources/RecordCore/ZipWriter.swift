import Foundation

/// Minimal zip writer: stored (uncompressed) entries with UTF-8 file names (flag bit 11),
/// so non-ASCII names survive in web unzippers. Not for archives over 4 GB.
public enum ZipWriter {
    public static func archive(_ entries: [(path: String, data: Data)], modified: Date = Date()) -> Data {
        let (dosTime, dosDate) = dosDateTime(modified)
        var out = Data()
        var central = Data()
        for entry in entries {
            let name = Data(entry.path.utf8)
            let crc = crc32(entry.data)
            let size = UInt32(entry.data.count)
            let offset = UInt32(out.count)

            out.append(le32(0x04034b50))
            out.append(le16(20)); out.append(le16(0x0800)); out.append(le16(0))   // version, flags, stored
            out.append(le16(dosTime)); out.append(le16(dosDate))
            out.append(le32(crc)); out.append(le32(size)); out.append(le32(size))
            out.append(le16(UInt16(name.count))); out.append(le16(0))
            out.append(name)
            out.append(entry.data)

            central.append(le32(0x02014b50))
            central.append(le16(0x0314)); central.append(le16(20))                 // made by Unix 2.0, needed 2.0
            central.append(le16(0x0800)); central.append(le16(0))
            central.append(le16(dosTime)); central.append(le16(dosDate))
            central.append(le32(crc)); central.append(le32(size)); central.append(le32(size))
            central.append(le16(UInt16(name.count))); central.append(le16(0)); central.append(le16(0))
            central.append(le16(0)); central.append(le16(0))                       // disk, internal attrs
            central.append(le32(0o100644 << 16))                                   // regular file, rw-r--r--
            central.append(le32(offset))
            central.append(name)
        }
        let centralOffset = UInt32(out.count)
        out.append(central)
        out.append(le32(0x06054b50))
        out.append(le16(0)); out.append(le16(0))
        out.append(le16(UInt16(entries.count))); out.append(le16(UInt16(entries.count)))
        out.append(le32(UInt32(central.count))); out.append(le32(centralOffset))
        out.append(le16(0))
        return out
    }

    private static let crcTable: [UInt32] = (0..<256).map { n -> UInt32 in
        var c = UInt32(n)
        for _ in 0..<8 { c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1 }
        return c
    }

    static func crc32(_ data: Data) -> UInt32 {
        var c: UInt32 = 0xFFFFFFFF
        data.withUnsafeBytes { buf in
            for byte in buf { c = crcTable[Int((c ^ UInt32(byte)) & 0xFF)] ^ (c >> 8) }
        }
        return c ^ 0xFFFFFFFF
    }

    private static func dosDateTime(_ date: Date) -> (UInt16, UInt16) {
        let c = Calendar(identifier: .gregorian).dateComponents([.year, .month, .day, .hour, .minute, .second], from: date)
        let hour: Int = c.hour ?? 0, minute: Int = c.minute ?? 0, second: Int = c.second ?? 0
        let year: Int = max((c.year ?? 1980) - 1980, 0), month: Int = c.month ?? 1, day: Int = c.day ?? 1
        let time: Int = (hour << 11) | (minute << 5) | (second / 2)
        let date: Int = (year << 9) | (month << 5) | day
        return (UInt16(time), UInt16(date))
    }

    private static func le16(_ v: UInt16) -> Data { withUnsafeBytes(of: v.littleEndian) { Data($0) } }
    private static func le32(_ v: UInt32) -> Data { withUnsafeBytes(of: v.littleEndian) { Data($0) } }
}
