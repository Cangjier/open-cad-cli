using System.Net;
using System.Web;
using TidyHPC.Extensions;
using TidyHPC.LiteJson;

namespace OpenCad.Cli;

/// <summary>
/// 模拟axios
/// </summary>
public class axios
{
    static axios()
    {
        HttpClient.Timeout = TimeSpan.FromDays(8);
    }

    private static HttpClient HttpClient { get; set; } = new HttpClient();

    public static void setProxy(string proxy)
    {
        HttpClient.Dispose();
        HttpClient = new HttpClient(new HttpClientHandler()
        {
            Proxy = new WebProxy(proxy)
        });
        HttpClient.Timeout = TimeSpan.FromDays(8);
    }

    public static void unsetProxy()
    {
        HttpClient.Dispose();
        HttpClient = new HttpClient();
        HttpClient.Timeout = TimeSpan.FromDays(8);
    }

    public static async Task<axiosResponse> get(string url)
    {
        return await get(url, null);
    }

    public static async Task<axiosResponse> get(string url, axiosConfig? config)
    {
        axiosResponse result = new();
        url = config?.getUrl(url) ?? url;
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        config?.setRequest(request);
        var response = await HttpClient.SendAsync(request);
        await result.setResponse(response, config);
        return result;
    }

    public static async Task<axiosResponse> delete(string url, axiosConfig? config)
    {
        axiosResponse result = new();
        url = config?.getUrl(url) ?? url;
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        config?.setRequest(request);
        var response = await HttpClient.SendAsync(request);
        await result.setResponse(response, config);
        return result;
    }

    public static async Task<axiosResponse> post(string url, Json data, axiosConfig? config)
    {
        axiosResponse result = new();
        url = config?.getUrl(url) ?? url;
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (data.Is<byte[]>())
        {
            request.Content = new ByteArrayContent(data.As<byte[]>());
        }
        else
        {
            request.Content = new StringContent(data.ToString(), Util.UTF8, "application/json");
        }
        config?.setRequest(request);
        var response = await HttpClient.SendAsync(request);
        await result.setResponse(response, config);
        return result;
    }

    public static async Task<axiosResponse> post(string url, Json data)
    {
        return await post(url, data, null);
    }

    public static async Task<axiosResponse> put(string url, Json data, axiosConfig? config)
    {
        axiosResponse result = new();
        url = config?.getUrl(url) ?? url;
        var request = new HttpRequestMessage(HttpMethod.Put, url);
        if (data.Is<byte[]>())
        {
            request.Content = new ByteArrayContent(data.As<byte[]>());
        }
        else
        {
            request.Content = new StringContent(data.ToString(), Util.UTF8, "application/json");
        }
        config?.setRequest(request);
        var response = await HttpClient.SendAsync(request);
        await result.setResponse(response, config);
        return result;
    }

    public static async Task<axiosResponse> put(string url, Json data)
    {
        return await put(url, data, null);
    }

    public static async Task<axiosResponse> patch(string url, Json data, axiosConfig? config)
    {
        axiosResponse result = new();
        url = config?.getUrl(url) ?? url;
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), url);
        if (data.Is<byte[]>())
        {
            request.Content = new ByteArrayContent(data.As<byte[]>());
        }
        else
        {
            request.Content = new StringContent(data.ToString(), Util.UTF8, "application/json");
        }
        config?.setRequest(request);
        var response = await HttpClient.SendAsync(request);
        await result.setResponse(response, config);
        return result;
    }

    public static async Task<axiosResponse> patch(string url, Json data)
    {
        return await patch(url, data, null);
    }

    public static async Task download(string url,string path)
    {
        var response = await HttpClient.GetAsync(url);
        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fileStream);
    }
}

public class axiosResponse
{
    /// <summary>
    /// 返回的data
    /// </summary>
    public object? data { get; set; }

    public Dictionary<string, string> headers { get; } = [];

    public int status { get; set; }

    public string statusText { get; set; } = "";

    public async Task setResponse(HttpResponseMessage response,axiosConfig? config)
    {
        status = (int)response.StatusCode;
        statusText = response.ReasonPhrase ?? string.Empty;
        foreach (var item in response.Headers)
        {
            headers.Add(item.Key, item.Value.Join(","));
        }
        // 根据 headers 中的 Content-Type 判断返回的数据类型
        if (response.Content != null)
        {
            foreach (var item in response.Content.Headers)
            {
                headers.Add(item.Key, item.Value.Join(","));
            }
            if (config == null)
            {
                data = Json.Parse(await response.Content.ReadAsStringAsync());
            }
            else if (config.responseType == "json")
            {
                data = Json.Parse(await response.Content.ReadAsStringAsync());
            }
            else if (config.responseType == "text")
            {
                data = await response.Content.ReadAsStringAsync();
            }
            else if (config.responseType == "arraybuffer")
            {
                data = await response.Content.ReadAsByteArrayAsync();
            }
            else
            {
                data = await response.Content.ReadAsByteArrayAsync();
            }
        }
    }
}

public class axiosConfig
{
    public static implicit operator axiosConfig(Json target)
    {
        var result = new axiosConfig();
        if(target.ContainsKey("headers"))
        {
            foreach (var item in target.Get("headers").GetObjectEnumerable())
            {
                result.headers.Add(item.Key, item.Value.AsString);
            }
        }
        if (target.ContainsKey("responseType"))
        {
            result.responseType = target.Get("responseType").AsString;
        }
        return result;
    }

    public Dictionary<string, string> headers = [];

    public Dictionary<string, string> @params = [];

    public string responseType = "json";

    public void setRequest(HttpRequestMessage request)
    {
        foreach (var (key, value) in headers)
        {
            if (key.Contains("Content") == false)
            {
                request.Headers.Add(key, value);
            }
            else
            {
                request.Content?.Headers.TryAddWithoutValidation(key, value);
            }
        }
    }

    public string getUrl(string url)
    {
        if (@params.Count == 0)
        {
            return url;
        }
        var uri = new Uri(url);
        var query = HttpUtility.ParseQueryString(uri.Query);
        foreach (var (key, value) in @params)
        {
            query[key] = value;
        }
        return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}?{query}";
    }
}
