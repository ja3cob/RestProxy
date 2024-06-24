namespace RestProxy;

public class RestException : Exception
{
    public RestException() { }
    public RestException(string message) : base(message) { }
    public RestException(string message, Exception inner) : base(message, inner) { }
}
