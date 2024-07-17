using System.Net;

namespace RestProxy;

public class RestException : Exception
{
    public HttpStatusCode Code { get; }

    public RestException(string message, HttpStatusCode code) : base(message)
    {
        Code = code;
    }

    public RestException(string message, HttpStatusCode code, Exception inner) : base(message, inner) 
    {
        Code = code;
    }
}
