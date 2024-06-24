using System.Net;

namespace RestProxy;

public class RestException : Exception
{
    public HttpStatusCode? Code { get; }

    public RestException() { }
    public RestException(string message) : base(message) { }

    public RestException(string message, HttpStatusCode code) : base(message)
    {
        Code = code;
    }

    public RestException(string message, Exception inner) : base(message, inner) { }
}
