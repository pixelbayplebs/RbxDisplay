using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RbxDisplay;

public sealed class UniverseLookupTests
{
    [Fact(DisplayName = "BloxStrike place resolves to its universe")]
    public async Task BloxStrikePlaceResolvesToItsUniverse()
    {
        string result = await UniverseLookup.ResolveAsync(GameProfile.BloxStrikePlaceId.ToString(System.Globalization.CultureInfo.InvariantCulture), Handler(request =>
        {
            Assert.EndsWith("/places/" + GameProfile.BloxStrikePlaceId + "/universe", request.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            return Json("{\"universeId\":" + GameProfile.BloxStrikeUniverseId + "}");
        }), CancellationToken.None);
        Assert.Equal(GameProfile.BloxStrikeUniverseId.ToString(System.Globalization.CultureInfo.InvariantCulture), result);
    }

    [Fact(DisplayName = "places in one universe collapse to one id")]
    public async Task PlacesInOneUniverseCollapse()
    {
        string result = await UniverseLookup.ResolveAsync("10,20", Handler(request => Json("{\"universeId\":5}")), CancellationToken.None);
        Assert.Equal("5", result);
    }

    [Fact(DisplayName = "distinct universes stay in place order")]
    public async Task DistinctUniversesStayInPlaceOrder()
    {
        string result = await UniverseLookup.ResolveAsync("10,20", Handler(request =>
            Json(request.RequestUri!.AbsolutePath.Contains("/places/10/", StringComparison.Ordinal)
                ? "{\"universeId\":5}"
                : "{\"universeId\":8}")), CancellationToken.None);
        Assert.Equal("5,8", result);
    }

    [Fact(DisplayName = "missing universe id is rejected")]
    public async Task MissingUniverseIdIsRejected()
    {
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            UniverseLookup.ResolveAsync("10", Handler(_ => Json("{\"universeId\":0}")), CancellationToken.None));
        Assert.Contains("10", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "unknown place is rejected")]
    public async Task UnknownPlaceIsRejected()
    {
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            UniverseLookup.ResolveAsync("10", Handler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)), CancellationToken.None));
        Assert.Contains("10", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "network failure is rejected")]
    public async Task NetworkFailureIsRejected()
    {
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            UniverseLookup.ResolveAsync("10", new ThrowingHandler(), CancellationToken.None));
        Assert.Contains("10", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "blank place list is rejected before any request")]
    public async Task BlankPlaceListIsRejected()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            UniverseLookup.ResolveAsync("", new ThrowingHandler(), CancellationToken.None));
    }

    private static ScriptedHandler Handler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        return new ScriptedHandler(respond);
    }

    private static HttpResponseMessage Json(string body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> respond;

        public ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            this.respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(respond(request));
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("down");
        }
    }
}
