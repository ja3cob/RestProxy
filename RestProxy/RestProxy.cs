﻿using System.Reflection;
using System.Text.Json;
using ASPClientLib.Attributes;

namespace RestProxy;

internal class RestProxy : DispatchProxy
{
    public RestApiCaller? ApiCaller { get; set; }
    public string ControllerRoute { get; set; } = "";

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null)
        {
            throw new ArgumentNullException(nameof(targetMethod));
        }

        if (ApiCaller == null)
        {
            throw new RestException(nameof(ApiCaller));
        }

        string requestUri = UriResolver.Resolve(targetMethod, args, ControllerRoute);
        HttpMethod method = ResolveMethod(targetMethod);
        string? body = ResolveBody(targetMethod, args);

        string response = ApiCaller.CallRest(requestUri, method, body).GetAwaiter().GetResult()
            .Content.ReadAsStringAsync().GetAwaiter().GetResult();

        bool returnsTask = targetMethod.ReturnType.Name == typeof(Task<>).Name;
        Type returnType = returnsTask
            ? targetMethod.ReturnType.GetGenericArguments()[0]
            : targetMethod.ReturnType;

        if (string.IsNullOrEmpty(response)
            || returnType.IsGenericType == false)
        {
            return null;
        }

        object? deserializedResponse = JsonSerializer.Deserialize(response, returnType.GetGenericArguments()[0]);
        object? result = Activator.CreateInstance(returnType, deserializedResponse);

        return returnsTask
            ? typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(returnType)
                .Invoke(null, [result])
            : result;
    }

    private static string? ResolveBody(MethodInfo targetMethod, object?[]? args)
    {
        var methodParams = targetMethod.GetParameters();
        for (int i = 0; i < methodParams.Length; i++)
        {
            if (Array.Exists(methodParams[i].GetCustomAttributes(false), p => p is FromBodyAttribute)
                && args?[i] != null)
            {
                return JsonSerializer.Serialize(args[i]);
            }
        }

        return null;
    }

    private static HttpMethod ResolveMethod(MethodInfo targetMethod)
    {
        return Array.Find(targetMethod.GetCustomAttributes(false), p => p is HttpMethodAttribute) switch
        {
            HttpGetAttribute => HttpMethod.Get,
            HttpPostAttribute => HttpMethod.Post,
            HttpPutAttribute => HttpMethod.Put,
            HttpPatchAttribute => HttpMethod.Patch,
            HttpDeleteAttribute => HttpMethod.Delete,
            _ => throw new RestException("Method not supported")
        };
    }
}