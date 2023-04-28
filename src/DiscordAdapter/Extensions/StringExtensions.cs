namespace DiscordAdapter.Extensions
{
    public static class StringExtensions
    {
        public static ulong ToUInt64(this string value) { return ulong.Parse(value); }
    }
}
