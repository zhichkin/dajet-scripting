namespace DaJet.Scripting
{
    internal static class Constants
    {
        internal readonly static byte[] TAG_UNDEFINED = [0x01];
        internal readonly static byte[] TAG_BOOLEAN = [0x02];
        internal readonly static byte[] TAG_NUMERIC = [0x03];
        internal readonly static byte[] TAG_DATETIME = [0x04];
        internal readonly static byte[] TAG_STRING = [0x05];
        internal readonly static byte[] TAG_BINARY = [0x06];
        internal readonly static byte[] TAG_ENTITY = [0x08];
        internal readonly static byte[] TRUE = [0x01];
        internal readonly static byte[] FALSE = [0x00];
        internal readonly static byte[] EMPTY_TYPE_CODE = [0x00000000];
        internal readonly static byte[] EMPTY_UUID = [0x00000000000000000000000000000000];
        internal readonly static byte[] VALUE_STORAGE = [0x01, 0x01, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xEF, 0xBB, 0xBF, 0x7B, 0x22, 0x55, 0x22, 0x7D];
    }
}