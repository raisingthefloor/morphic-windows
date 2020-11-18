namespace Morphic.Settings.Resolvers
{
    /// <summary>
    /// Wraps a string, which can contain resolver expressions which get expanded when accessed.
    /// </summary>
    public class ResolvingString
    {
        private readonly string originalValue;
        private readonly bool hasExpression;

        private ResolvingString(string value)
        {
            this.originalValue = value;
            this.hasExpression = Resolver.ContainsExpression(this.originalValue);
        }

        public static implicit operator ResolvingString(string value)
        {
            return new ResolvingString(value);
        }

        public static implicit operator string(ResolvingString value)
        {
            return value.ToString();
        }

        public override string ToString()
        {
            return this.hasExpression
                ? Resolver.Resolve(this.originalValue) ?? string.Empty
                : this.originalValue;
        }
    }
}
