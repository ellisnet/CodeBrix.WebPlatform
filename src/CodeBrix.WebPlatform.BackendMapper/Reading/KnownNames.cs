namespace CodeBrix.WebPlatform.BackendMapper.Reading;

/// <summary>The full names of the framework and platform types the mapper recognises.</summary>
internal static class KnownNames
{
    public const string ControllerBase = "Microsoft.AspNetCore.Mvc.ControllerBase";
    public const string ApiController = "Microsoft.AspNetCore.Mvc.ApiControllerAttribute";
    public const string NonController = "Microsoft.AspNetCore.Mvc.NonControllerAttribute";
    public const string NonAction = "Microsoft.AspNetCore.Mvc.NonActionAttribute";
    public const string Route = "Microsoft.AspNetCore.Mvc.RouteAttribute";
    public const string HttpMethodAttribute = "Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute";
    public const string HttpGet = "Microsoft.AspNetCore.Mvc.HttpGetAttribute";
    public const string HttpPost = "Microsoft.AspNetCore.Mvc.HttpPostAttribute";
    public const string HttpPut = "Microsoft.AspNetCore.Mvc.HttpPutAttribute";
    public const string HttpDelete = "Microsoft.AspNetCore.Mvc.HttpDeleteAttribute";
    public const string HttpPatch = "Microsoft.AspNetCore.Mvc.HttpPatchAttribute";
    public const string FromBody = "Microsoft.AspNetCore.Mvc.FromBodyAttribute";
    public const string FromQuery = "Microsoft.AspNetCore.Mvc.FromQueryAttribute";
    public const string FromRoute = "Microsoft.AspNetCore.Mvc.FromRouteAttribute";
    public const string FromHeader = "Microsoft.AspNetCore.Mvc.FromHeaderAttribute";
    public const string FromForm = "Microsoft.AspNetCore.Mvc.FromFormAttribute";
    public const string FromServices = "Microsoft.AspNetCore.Mvc.FromServicesAttribute";
    public const string FromKeyedServices = "Microsoft.Extensions.DependencyInjection.FromKeyedServicesAttribute";
    public const string ProducesResponseType = "Microsoft.AspNetCore.Mvc.ProducesResponseTypeAttribute";
    public const string ProducesResponseTypeOfT = "Microsoft.AspNetCore.Mvc.ProducesResponseTypeAttribute`1";
    public const string ActionResultOfT = "Microsoft.AspNetCore.Mvc.ActionResult`1";
    public const string IActionResult = "Microsoft.AspNetCore.Mvc.IActionResult";
    public const string IResult = "Microsoft.AspNetCore.Http.IResult";
    public const string Authorize = "Microsoft.AspNetCore.Authorization.AuthorizeAttribute";
    public const string AllowAnonymous = "Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute";
    public const string Hub = "Microsoft.AspNetCore.SignalR.Hub";
    public const string HubOfT = "Microsoft.AspNetCore.SignalR.Hub`1";

    public const string Task = "System.Threading.Tasks.Task";
    public const string TaskOfT = "System.Threading.Tasks.Task`1";
    public const string ValueTask = "System.Threading.Tasks.ValueTask";
    public const string ValueTaskOfT = "System.Threading.Tasks.ValueTask`1";
    public const string Void = "System.Void";
    public const string Object = "System.Object";
    public const string String = "System.String";
    public const string NullableOfT = "System.Nullable`1";
    public const string CancellationToken = "System.Threading.CancellationToken";
    public const string AsyncEnumerableOfT = "System.Collections.Generic.IAsyncEnumerable`1";
    public const string ChannelReaderOfT = "System.Threading.Channels.ChannelReader`1";

    public const string JsonIgnore = "System.Text.Json.Serialization.JsonIgnoreAttribute";
    public const string JsonPropertyName = "System.Text.Json.Serialization.JsonPropertyNameAttribute";
    public const string JsonConverter = "System.Text.Json.Serialization.JsonConverterAttribute";
    public const string JsonStringEnumConverter = "System.Text.Json.Serialization.JsonStringEnumConverter";

    public const string WireRequired = "CodeBrix.WebPlatform.Core.Mapping.WireRequiredAttribute";
    public const string WireOptional = "CodeBrix.WebPlatform.Core.Mapping.WireOptionalAttribute";
    public const string WireType = "CodeBrix.WebPlatform.Core.Mapping.WireTypeAttribute";
    public const string WireFile = "CodeBrix.WebPlatform.Core.Mapping.WireFileAttribute";
    public const string WireIgnore = "CodeBrix.WebPlatform.Core.Mapping.WireIgnoreAttribute";
    public const string WireHub = "CodeBrix.WebPlatform.Core.Mapping.WireHubAttribute";
    public const string WireRequestMarker = "CodeBrix.WebPlatform.Core.Mapping.WireRequestMarkerAttribute";
    public const string WireSessionRefresh = "CodeBrix.WebPlatform.Core.Mapping.WireSessionRefreshAttribute";
    public const string ApiProblem = "CodeBrix.WebPlatform.Core.Problems.ApiProblem";
}
