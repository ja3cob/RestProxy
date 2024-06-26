﻿using System.Reflection;
using System.Text.Json;
using ASPClientLib.Attributes;

namespace RestProxy;

internal class RestProxy : DispatchProxy
{
    public RestApiCaller? ApiCaller { get; set; }
    public string ControllerRoute { get; set; } = "";
    public bool ThrowOnNonSuccessfulResponse { get; set; } = true;

    public RequestFinishedEventHandler? RequestFinished;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null)
        {
            RequestFinished?.Invoke(false, "Target method cannot be null");
            throw new ArgumentNullException(nameof(targetMethod));
        }

        if (ApiCaller == null)
        {
            RequestFinished?.Invoke(false, "Rest api caller cannot be null");
            throw new RestException(nameof(ApiCaller));
        }

        string requestUri = UriResolver.Resolve(targetMethod, args, ControllerRoute);
        HttpMethod method = ResolveMethod(targetMethod);
        string? body = ResolveBody(targetMethod, args);

        HttpResponseMessage? httpResponse = null;
        try
        {
            httpResponse = ApiCaller.CallRest(requestUri, method, body).GetAwaiter().GetResult();
        }
        catch (RestException rex)
        {
            RequestFinished?.Invoke(false, rex.Message, rex.Code);
            if (ThrowOnNonSuccessfulResponse)
            {
                throw;
            }
        }

        RequestFinished?.Invoke(true, null, httpResponse?.StatusCode);

        string? response = httpResponse?.Content.ReadAsStringAsync().GetAwaiter().GetResult();

        bool returnsTask = targetMethod.ReturnType.Name == typeof(Task<>).Name;
        Type returnType = returnsTask
            ? targetMethod.ReturnType.GetGenericArguments()[0]
            : targetMethod.ReturnType;

        object? result = null;
        if (string.IsNullOrEmpty(response) == false && returnType.IsGenericType)
        {
            object? deserializedResponse = JsonSerializer.Deserialize(response, returnType.GetGenericArguments()[0], new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            result = Activator.CreateInstance(returnType, deserializedResponse);
        }

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
                return JsonSerializer.Serialize(args[i], new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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
