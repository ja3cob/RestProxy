using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using ASPClientLib.Attributes;
using Newtonsoft.Json;

namespace RestProxy
{
    internal class RestProxy : DispatchProxy
    {
        public RestApiCaller? ApiCaller { get; set; }
        public bool ThrowOnNonSuccessfulResponse { get; set; } = true;

        public RequestFinishedEventHandler? RequestFinished;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null)
            {
                throw new ArgumentNullException(nameof(targetMethod));
            }

            if (ApiCaller == null)
            {
                throw new RestException(nameof(ApiCaller), HttpStatusCode.InternalServerError);
            }

            string requestUri = UriResolver.Resolve(targetMethod, args);
            HttpMethod method = ResolveMethod(targetMethod);
            string? body = ResolveBody(targetMethod, args);

            HttpResponseMessage httpResponse;
            try
            {
                httpResponse = ApiCaller.CallRest(requestUri, method, body).GetAwaiter().GetResult();
            }
            catch (RestException rex)
            {
                RequestFinished?.Invoke(false, rex.Code, rex.Message);
                if (ThrowOnNonSuccessfulResponse)
                {
                    throw;
                }

                return null;
            }

            RequestFinished?.Invoke(true, httpResponse.StatusCode);

            string? response = httpResponse?.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            bool returnsTask = targetMethod.ReturnType.Name == typeof(Task<>).Name;
            Type returnType = returnsTask
                ? targetMethod.ReturnType.GetGenericArguments()[0]
                : targetMethod.ReturnType;

            object? result = null;
            if (string.IsNullOrEmpty(response) == false && returnType.IsGenericType)
            {
                object? deserializedResponse = JsonConvert.DeserializeObject(response, returnType.GetGenericArguments()[0]);
                result = Activator.CreateInstance(returnType, deserializedResponse);
            }

            return returnsTask
                ? typeof(Task)
                    .GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(returnType)
                    .Invoke(null, new[] { result })
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
                    return JsonConvert.SerializeObject(args[i]);
                }
            }

            return null;
        }

        private static HttpMethod ResolveMethod(MethodInfo targetMethod)
        {
            return Array.Find(targetMethod.GetCustomAttributes(false), p => p is HttpMethodAttribute) switch
            {
                HttpGetAttribute _ => HttpMethod.Get,
                HttpPostAttribute _ => HttpMethod.Post,
                HttpPutAttribute _ => HttpMethod.Put,
                HttpPatchAttribute _ => HttpMethod.Patch,
                HttpDeleteAttribute _ => HttpMethod.Delete,
                _ => throw new RestException("Method not supported", HttpStatusCode.BadRequest)
            };
        }
    }
}
