using System.Reflection;
using System.Text;
using ASPClientLib.Attributes;

namespace RestProxy;

internal static class UriResolver
{
    private const string ControllerSuffix = "Controller";

    private static Type _controller = null!;
    private static MethodInfo _method = null!;
    private static object?[]? _args;

    public static string Resolve(MethodInfo method, object?[]? args, string defaultRoute)
    {
        _controller = method.DeclaringType ?? throw new ArgumentException("Method declaring type is null");
        _method = method;
        _args = args;

        return ResolveRoute() + ResolveQuery();
    }

    private static string ResolveQuery()
    {
        var builder = new StringBuilder("");

        var methodParams = _method.GetParameters();
        for (int i = 0; i < methodParams.Length; i++)
        {
            if (!Array.Exists(methodParams[i].GetCustomAttributes(false), p => p is FromQueryAttribute))
            {
                continue;
            }

            builder.Append(builder.Length > 0 ? '&' : '?');

            builder.Append(methodParams[i].Name + "=");
            if (_args?[i] != null)
            {
                builder.Append(_args[i]);
            }
        }

        return builder.ToString();
    }

    private static string ResolveRoute()
    {
        object? routeObj = Array.Find(_method.GetCustomAttributes(false), p => p is RouteAttribute)
                           ?? Array.Find(_controller.GetCustomAttributes(false), p => p is RouteAttribute);
        if (routeObj == default)
        {
            throw new ArgumentException("Controller or method need to have route attribute");
        }

        return ((RouteAttribute)routeObj).Template.ReplaceTokens().ReplaceEntities();
    }

    private static string GetRouteParameterValue(ParameterInfo[] parameters, string paramName)
    {
        var targetParameter = Array.Find(parameters, p =>
            (p.GetCustomAttributes(false).Length == 0
             || Array.Exists(p.GetCustomAttributes(false), r => r is FromRouteAttribute))
            && p.Name == paramName);

        int index = Array.IndexOf(parameters, targetParameter);
        if (index < 0)
        {
            return string.Empty;
        }

        return _args?[index]?.ToString() ?? string.Empty;
    }

    private static string ReplaceEntities(this string uri)
    {
        var builder = new StringBuilder("");
        for (int i = 0; i < uri.Length; i++)
        {
            if (uri[i] != '{')
            {
                builder.Append(uri[i]);
                continue;
            }

            i++;
            var varName = new StringBuilder("");
            for (; uri[i] != '}'; i++)
            {
                varName.Append(uri[i]);
            }

            builder.Append(GetRouteParameterValue(_method.GetParameters(), varName.ToString()));
        }

        return builder.ToString();
    }

    private static string ReplaceTokens(this string uri)
    {
        return uri
            .Replace("[action]", _method.Name)
            .Replace("[controller]", GetControllerName());
    }

    private static string GetControllerName()
    {
        string name = _controller.Name;
        if (name.StartsWith('I'))
        {
            name = name[1..];
        }

        if (name.EndsWith(ControllerSuffix))
        {
            name = name.Remove(name.Length - ControllerSuffix.Length);
        }

        return name;
    }
}
