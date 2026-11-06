using System.Text;

namespace PiggyMetrics.AuthService.Tests.Contract;

internal static class Send
{
    public static Task<HttpResponseMessage> GetAsync(HttpClient client, string path, string? bearer)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        AddBearer(request, bearer);
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string path, string json, string? bearer)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        AddBearer(request, bearer);
        return client.SendAsync(request);
    }

    private static void AddBearer(HttpRequestMessage request, string? bearer)
    {
        if (!string.IsNullOrEmpty(bearer))
        {
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearer);
        }
    }
}
