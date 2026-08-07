namespace TodoApi.Services
{
    /// <summary>
    /// Thrown when an optimistic concurrency conflict occurs (version mismatch).
    /// </summary>
    public class ConcurrencyException : Exception
    {
        public ConcurrencyException() { }
        public ConcurrencyException(string message) : base(message) { }
        public ConcurrencyException(string message, Exception inner) : base(message, inner) { }
    }
}