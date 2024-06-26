﻿using System.Net;
using System.Text;

namespace RestProxy;

internal class RestApiCaller
{
    private readonly HttpClient _client;

    public RestApiCaller(string baseUri)
    {
        if(string.IsNullOrEmpty(baseUri))
        {
            throw new ArgumentException("Base uri cannot be empty");
        }

        _client = CreateHttpClient(baseUri);
    }

    private static HttpClient CreateHttpClient(string baseUri)
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri(baseUri);

        client.DefaultRequestHeaders.Add("Accept", "application/json");
        return client;
    }

    public async Task<HttpResponseMessage> CallRest(string requestUri, HttpMethod method, string? body)
    {
        StringContent? content = null;
        if (body != null)
        {
            content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        Func<string, StringContent?, Task<HttpResponseMessage>>? requestWithContent = null;
        Func<string, Task<HttpResponseMessage>>? requestWithoutContent = null;

        HttpResponseMessage? response;
        if (method == HttpMethod.Post)
        {
            requestWithContent = _client.PostAsync;
        }
        else if (method == HttpMethod.Patch)
        {
            requestWithContent = _client.PatchAsync;
        }
        else if (method == HttpMethod.Put)
        {
            requestWithContent = _client.PutAsync;
        }
        else if (method == HttpMethod.Delete)
        {
            requestWithoutContent = _client.DeleteAsync;
        }
        else if (method == HttpMethod.Get)
        {
            requestWithoutContent = _client.GetAsync;
        }

        try
        {
            if (requestWithContent != null)
            {
                response = await requestWithContent(requestUri, content).ConfigureAwait(false);
            }
            else if (requestWithoutContent != null)
            {
                response = await requestWithoutContent(requestUri).ConfigureAwait(false);
            }
            else
            {
                throw new RestException($"Method {method.Method} is not supported");
            }

            if (response == null)
            {
                throw new RestException("Server returned null");
            }
        }
        catch (Exception ex)
        {
            throw new RestException("Error while processing the request", ex);
        }

        SetCookies(response);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new RestException("The requested resource could not be found", response.StatusCode);
        }

        if (response.IsSuccessStatusCode == false)
        {
            throw new RestException("Error while processing the request", response.StatusCode);
        }

        return response;
    }

    private void SetCookies(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? cookiesEnumerable) == false)
        {
            return;
        }

        List<string> setCookies = cookiesEnumerable.ToList();

        KeyValuePair<string, IEnumerable<string>> existingCookies = _client.DefaultRequestHeaders.FirstOrDefault(p => p.Key == "Cookie");
        IEnumerable<string> setCookieNames = setCookies.Select(p => p.Split('=')[0]);

        List<string> cookiesToAdd = existingCookies.Key == null
            ? []
            : existingCookies.Value
                .Where(p => setCookieNames.Contains(p.Split('=')[0]) == false)
                .ToList();

        cookiesToAdd.AddRange(setCookies);

        _client.DefaultRequestHeaders.Remove("Cookie");

        foreach (string cookie in cookiesToAdd)
        {
            _client.DefaultRequestHeaders.Add("Cookie", cookie);
        }
    }
}
