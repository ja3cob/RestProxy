using System.Reflection;
using ASPClientLib.Attributes;
using ASPClientLib.Classes;
using ASPClientLib.Interfaces;

namespace RestProxy;

public class RestProxyManager(string baseUri)
{
    private readonly RestApiCaller _apiCaller = new(baseUri);

    public TController GetProxy<TController>()
        where TController : class
    {
        if (!VerifyController<TController>())
        {
            throw new ArgumentException($"{typeof(TController)} is not a valid controller interface");
        }

        if (DispatchProxy.Create<TController, RestProxy>() is not RestProxy proxy)
        {
            throw new RestException("Error while creating communication proxy with the api");
        }

        proxy.ApiCaller = _apiCaller;
        return proxy as TController;
    }

    private static bool VerifyController<TController>()
    {
        var controllerType = typeof(TController);
        if (controllerType.IsInterface == false
            || controllerType.Name.StartsWith("I") == false)
        {
            return false;
        }

        bool controllerHasRouteAttribute = controllerType.GetCustomAttributes().ToList().Exists(p => p.GetType().IsSubclassOf(typeof(RouteAttribute)));

        foreach (MethodInfo method in controllerType.GetMethods())
        {
            var attributes = method.GetCustomAttributes().ToList();
            if (attributes.Exists(p => p.GetType().IsSubclassOf(typeof(HttpMethodAttribute))) == false)
            {
                return false;
            }

            if (controllerHasRouteAttribute == false
                && !attributes.Exists(p => p.GetType().IsSubclassOf(typeof(RouteAttribute))) == false)
            {
                return false;
            }

            if (
                (
                    method.ReturnType.Name != typeof(ActionResult<>).Name
                    && typeof(IActionResult).IsAssignableFrom(method.ReturnType) == false
                )
                &&
                (
                    method.ReturnType.IsGenericType
                    && method.ReturnType.Name == typeof(Task<>).Name
                    && method.ReturnType.GetGenericArguments()[0].Name != typeof(ActionResult<>).Name
                    && typeof(IActionResult).IsAssignableFrom(method.ReturnType.GetGenericArguments()[0]) == false
                )
            )
            {
                return false;
            }
        }

        return true;
    }
}
