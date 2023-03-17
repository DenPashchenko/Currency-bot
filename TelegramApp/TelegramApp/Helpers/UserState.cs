namespace TelegramApp.Helpers
{
    public class UserState : IEquatable<UserState?>
    {
        public DateTime Date { get; set; }
        public string? CurrencyCode { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as UserState);
        }

        public bool Equals(UserState? other)
        {
            return other is not null &&
                   Date == other.Date &&
                   CurrencyCode == other.CurrencyCode;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Date, CurrencyCode);
        }

        public static bool operator ==(UserState? left, UserState? right)
        {
            return EqualityComparer<UserState>.Default.Equals(left, right);
        }

        public static bool operator !=(UserState? left, UserState? right)
        {
            return !(left == right);
        }
    }
}

