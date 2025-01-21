# RestProxy
Proxy generator for .NET HTTP clients, which connect to an ASP.NET Core server.

## Remarks
This package is still in development and definitely misses some features. If you would like to propose something you can create an issue on [GitHub](https://github.com/ja3cob/RestProxy)

## Properties
As it is for now, RestProxyManager has 2 properties:
- RequestTimeoutMilliseconds - request timeout in milliseconds
- AllowUntrustedServerCertificate - whether to allow or not untrusted server's certificate

## Usage
1. Generate an interface representing your controller in ASP.NET Core server, but use types from **[ASPClientLib](https://github.com/ja3cob/ASPClientLib)** package (this allows support on all platforms because ASP.NET Core framework sometimes cannot be referenced). Controller interface must include at least **RouteAttribute** on the interface or actions and **HttpMethodAttribute** on all actions.

    Example:

    **AuthenticationController** ASP.NET Controller:
    >     [ApiController]
    >     [RouteApiController("[action]")]
    >     public class AuthenticationController(AuthService service) : ControllerBase
    >     {
    >         [HttpPost]
    >         [AllowAnonymous]
    >         public async Task<ActionResult<bool>> Login([FromBody] LoginRequest request)
    >         {
    >             var principal = service.TryGetPrincipal(request);
    >             if (principal == null)
    >             {
    >                 return Unauthorized(false);
    >             }
    >     
    >             await HttpContext.SignInAsync(principal);
    >             return Ok(true);
    >         }
    >     }
    > Note that in order for this controller to work, types (HttpPost, IActionResult, etc.) are from the **ASP.NET Core** framework.
    
    **IAuthenticationController** interface:
    >     [RouteApiController("[action]")]
    >     public interface IAuthenticationController
    >     {
    >         [HttpPost]
    >         Task<ActionResult<bool>> Login([FromBody] LoginRequest request);
    >     }
    > Note that the interface uses types from the **[ASPClientLib](https://github.com/ja3cob/ASPClientLib)** package to mantain support on all client platforms
    
2. Reference **RestProxy** and your controller interface (here **IAutenticationController**) in your client project.
3. Create a new instance of the **RestProxyManager** class, providing base url of the server as the constructor parameter. Use the manager to get proxy and send request to the server.

         var manager = new RestProxyManager("https://localhost:5000/");
         var proxy = manager.GetProxy<IAuthenticationController>();
         bool result = (await proxy.Login(new LoginRequest(username, password))).Value;

    *This construction automatically sets cookies when an appropriate header is received in response.*

    1. Optionally you can configure authentication by providing asynchronous authentication method delegate in the constructor.

        ```
        var manager = new RestProxyManager("https://localhost:5000", authenticateActionAsync: AuthenticateAsync);
        var proxy = manager.GetProxy<ISimpleController>();
        var result = proxy.GetResult().Value; //Authentication happens on first 401 Unauthorized response
        ```
        *This construction allows to configure header-based authentication.*

        You can also configure other authentication options via **RestProxyManager** constructor parameters.
