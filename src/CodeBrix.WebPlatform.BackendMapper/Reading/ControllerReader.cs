using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeBrix.WebPlatform.BackendMapper.Model;

namespace CodeBrix.WebPlatform.BackendMapper.Reading;

/// <summary>Reads the REST controllers of a backend assembly.</summary>
internal sealed class ControllerReader
{
    private static readonly (string Attribute, string Method)[] HttpMethods =
    {
        (KnownNames.HttpGet, "GET"), (KnownNames.HttpPost, "POST"), (KnownNames.HttpPut, "PUT"),
        (KnownNames.HttpDelete, "DELETE"), (KnownNames.HttpPatch, "PATCH"),
    };

    private readonly MapContext _context;

    public ControllerReader(MapContext context)
    {
        _context = context;
    }

    public static bool IsController(Type type)
    {
        if (!type.IsClass || type.IsAbstract || !type.IsPublic || type.IsGenericTypeDefinition)
        {
            return false;
        }

        if (Attributes.Has(Attributes.OfTypeAndBases(type), KnownNames.NonController))
        {
            return false;
        }

        for (var current = type.BaseType; current != null; current = current.BaseType)
        {
            if (current.FullName == KnownNames.ControllerBase)
            {
                return true;
            }
        }

        return false;
    }

    public RestController Read(Type type)
    {
        var name = type.Name.EndsWith("Controller", StringComparison.Ordinal) && type.Name.Length > "Controller".Length
            ? type.Name.Substring(0, type.Name.Length - "Controller".Length)
            : type.Name;
        var controller = new RestController(name, type.FullName);
        var classAttributes = Attributes.OfTypeAndBases(type).ToList();
        if (!Attributes.Has(classAttributes, KnownNames.ApiController))
        {
            _context.Problem(type.Name, "the controller has no [ApiController] attribute, which the platform's binding rules rely on.");
        }

        var routes = Attributes.FindAll(type.GetCustomAttributesData(), KnownNames.Route);
        if (routes.Count == 0)
        {
            routes = Attributes.FindAll(classAttributes, KnownNames.Route).Take(1).ToList();
        }

        if (routes.Count > 1)
        {
            _context.Problem(type.Name, "the controller has more than one [Route]; give it exactly one.");
        }

        var controllerTemplate = routes.Count > 0 ? Attributes.FirstString(routes[0]) ?? string.Empty : string.Empty;
        var functionNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var method in ActionMethods(type))
        {
            var endpoint = ReadAction(type, name, controllerTemplate, classAttributes, method);
            if (endpoint == null)
            {
                continue;
            }

            if (!functionNames.Add(WireNames.SnakeCase(FunctionBaseName(method.Name))))
            {
                _context.Problem($"{type.Name}.{method.Name}",
                    "another action of this controller has the same name; the generated functions would collide. Rename one of them.");
                continue;
            }

            controller.EndpointList.Add(endpoint);
        }

        return controller;
    }

    /// <summary>The method name a generated function is named after: without an <c>Async</c> suffix.</summary>
    internal static string FunctionBaseName(string methodName)
        => methodName.EndsWith("Async", StringComparison.Ordinal) && methodName.Length > "Async".Length
            ? methodName.Substring(0, methodName.Length - "Async".Length)
            : methodName;

    private static IEnumerable<MethodInfo> ActionMethods(Type type)
    {
        //Base classes first, so shared actions come before a controller's own.
        var chain = new List<Type>();
        for (var current = type; current != null && current.FullName != KnownNames.ControllerBase && current.FullName != KnownNames.Object; current = current.BaseType)
        {
            chain.Add(current);
        }

        chain.Reverse();
        foreach (var declaring in chain)
        {
            foreach (var method in declaring.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName || method.IsGenericMethodDefinition)
                {
                    continue;
                }

                var attributes = method.GetCustomAttributesData();
                if (Attributes.Has(attributes, KnownNames.NonAction) || Attributes.Has(attributes, KnownNames.WireIgnore))
                {
                    continue;
                }

                yield return method;
            }
        }
    }

    private RestEndpoint ReadAction(Type type, string controllerName, string controllerTemplate,
        List<CustomAttributeData> classAttributes, MethodInfo method)
    {
        var location = $"{type.Name}.{method.Name}";
        var attributes = method.GetCustomAttributesData();
        var problemsBefore = _context.Problems.Count;

        var httpAttributes = attributes.Where(a => Attributes.Is(a, KnownNames.HttpMethodAttribute)).ToList();
        if (httpAttributes.Count == 0)
        {
            _context.Problem(location, "the action has no HTTP method attribute ([HttpGet], [HttpPost], ...). The platform uses attribute routing only; mark a method that is not an endpoint [NonAction] or [WireIgnore].");
            return null;
        }

        if (httpAttributes.Count > 1)
        {
            _context.Problem(location, "the action has more than one HTTP method attribute; give each method and route its own action.");
            return null;
        }

        var httpMethod = HttpMethods.FirstOrDefault(m => Attributes.Is(httpAttributes[0], m.Attribute)).Method;
        if (httpMethod == null)
        {
            _context.Problem(location, $"the HTTP method attribute {httpAttributes[0].AttributeType.Name} is not supported (GET, POST, PUT, DELETE and PATCH are).");
            return null;
        }

        var actionTemplate = Attributes.FirstString(httpAttributes[0]);
        var actionRoutes = Attributes.FindAll(attributes, KnownNames.Route);
        if (actionRoutes.Count > 0)
        {
            if (actionTemplate != null || actionRoutes.Count > 1)
            {
                _context.Problem(location, "the action's route is declared more than once; put it on the HTTP method attribute only.");
            }
            else
            {
                actionTemplate = Attributes.FirstString(actionRoutes[0]);
            }
        }

        var combined = RouteTemplate.ReplaceTokens(RouteTemplate.Combine(controllerTemplate, actionTemplate), controllerName, method.Name);
        var segments = RouteTemplate.Parse(combined, out var cleanTemplate, out var unsupported);
        if (segments == null)
        {
            _context.Problem(location, $"the route '{combined}' cannot be mapped: {unsupported}");
            return null;
        }

        var endpoint = new RestEndpoint(method.Name, httpMethod, cleanTemplate);
        endpoint.RouteList.AddRange(segments);
        ReadAuthorization(endpoint, location, attributes, classAttributes);
        ReadParameters(endpoint, type, method, location, segments);
        ReadResponse(endpoint, type, method, location, attributes, classAttributes);
        ReadSessionRefresh(endpoint, location, attributes);
        return _context.Problems.Count == problemsBefore ? endpoint : null;
    }

    /// <summary>
    /// Checks an action marked <c>[WireSessionRefresh]</c> against what the platform's clients do
    /// with it: a POST without a body, sent when the session has expired.
    /// </summary>
    private void ReadSessionRefresh(RestEndpoint endpoint, string location, IList<CustomAttributeData> attributes)
    {
        if (!Attributes.Has(attributes, KnownNames.WireSessionRefresh))
        {
            return;
        }

        _context.SessionRefreshLocations.Add(location);
        endpoint.IsSessionRefresh = true;
        if (endpoint.HttpMethod != "POST")
        {
            _context.Problem(location, "the [WireSessionRefresh] action must answer POST, because the platform's clients renew a session with a POST.");
        }

        if (endpoint.RequiresSignIn)
        {
            _context.Problem(location, "the [WireSessionRefresh] action is called when the session has expired, so it must be callable without one. Mark it [AllowAnonymous].");
        }

        if (endpoint.Parameters.Count > 0)
        {
            _context.Problem(location, "the [WireSessionRefresh] action must take nothing (no route values, query values or body): the platform's clients call it without any.");
        }

        if (endpoint.IsFile)
        {
            _context.Problem(location, "an action cannot be both [WireFile] and [WireSessionRefresh].");
        }
    }

    private void ReadAuthorization(RestEndpoint endpoint, string location, IList<CustomAttributeData> attributes, List<CustomAttributeData> classAttributes)
    {
        if (Attributes.Has(attributes, KnownNames.AllowAnonymous) || Attributes.Has(classAttributes, KnownNames.AllowAnonymous))
        {
            endpoint.RequiresSignIn = false;
        }
        else if (Attributes.Has(attributes, KnownNames.Authorize) || Attributes.Has(classAttributes, KnownNames.Authorize))
        {
            endpoint.RequiresSignIn = true;
            _context.SignInSeen = true;
        }
        else
        {
            _context.Problem(location, "neither the action nor its controller says who may call it. Add [Authorize] or [AllowAnonymous].");
        }
    }

    private void ReadParameters(RestEndpoint endpoint, Type type, MethodInfo method, string location, List<RouteSegment> segments)
    {
        var routeTokens = segments.Where(s => s.IsParameter).Select(s => s.Text).ToList();
        var route = new Dictionary<string, EndpointParameter>(StringComparer.OrdinalIgnoreCase);
        var query = new List<EndpointParameter>();
        EndpointParameter body = null;
        foreach (var parameter in method.GetParameters())
        {
            var parameterLocation = $"{location}({parameter.Name})";
            var attributes = parameter.GetCustomAttributesData();
            if (parameter.ParameterType.FullName == KnownNames.CancellationToken
                || Attributes.Has(attributes, KnownNames.FromServices) || Attributes.Has(attributes, KnownNames.FromKeyedServices))
            {
                continue;
            }

            if (Attributes.Has(attributes, KnownNames.FromHeader) || Attributes.Has(attributes, KnownNames.FromForm))
            {
                _context.Problem(parameterLocation, "a parameter read from a header or a form cannot be mapped. Take it from the route, the query string or a JSON body.");
                continue;
            }

            if (parameter.ParameterType.IsByRef)
            {
                _context.Problem(parameterLocation, "a ref or out parameter cannot be mapped.");
                continue;
            }

            var fromRoute = Attributes.Find(attributes, KnownNames.FromRoute);
            var fromQuery = Attributes.Find(attributes, KnownNames.FromQuery);
            var fromBody = Attributes.Find(attributes, KnownNames.FromBody);
            var explicitName = (fromRoute == null ? null : Attributes.Named(fromRoute, "Name") as string)
                ?? (fromQuery == null ? null : Attributes.Named(fromQuery, "Name") as string);
            var wireName = explicitName ?? parameter.Name;
            var matchesToken = routeTokens.Any(t => string.Equals(t, wireName, StringComparison.OrdinalIgnoreCase));
            var simple = _context.TypeReader.IsSimple(parameter.ParameterType);

            ParameterSource source;
            if (fromRoute != null)
            {
                source = ParameterSource.Route;
            }
            else if (fromBody != null)
            {
                source = ParameterSource.Body;
            }
            else if (fromQuery != null)
            {
                source = ParameterSource.Query;
            }
            else if (matchesToken)
            {
                source = ParameterSource.Route;
            }
            else
            {
                source = simple ? ParameterSource.Query : ParameterSource.Body;
            }

            switch (source)
            {
                case ParameterSource.Route:
                    if (!matchesToken)
                    {
                        _context.Problem(parameterLocation, $"the route has no '{{{wireName}}}' for this parameter.");
                        break;
                    }

                    if (!simple)
                    {
                        _context.Problem(parameterLocation, "a route value must be a single value (text, a number, an id, a date, an enum).");
                        break;
                    }

                    var token = routeTokens.First(t => string.Equals(t, wireName, StringComparison.OrdinalIgnoreCase));
                    var routeType = _context.TypeReader.Read(parameter.ParameterType, parameterLocation, attributes, () => false);
                    if (routeType.IsOptional)
                    {
                        _context.Problem(parameterLocation, "a route value cannot be optional.");
                        break;
                    }

                    if (!route.TryAdd(token, new EndpointParameter(parameter.Name, token, ParameterSource.Route, routeType)))
                    {
                        _context.Problem(parameterLocation, $"two parameters read the route value '{{{token}}}'.");
                    }

                    break;

                case ParameterSource.Body:
                    if (body != null)
                    {
                        _context.Problem(parameterLocation, "the action reads more than one parameter from the body; an action has one body.");
                        break;
                    }

                    var bodyType = _context.TypeReader.Read(parameter.ParameterType, parameterLocation, attributes, () => false);
                    body = new EndpointParameter(parameter.Name, null, ParameterSource.Body, bodyType);
                    break;

                default:
                    if (simple)
                    {
                        var queryType = _context.TypeReader.Read(parameter.ParameterType, parameterLocation, attributes,
                            Optionality.ForParameter(parameter, method, type));
                        if (parameter.HasDefaultValue)
                        {
                            queryType.IsOptional = true;
                        }

                        query.Add(new EndpointParameter(parameter.Name, wireName, ParameterSource.Query, queryType));
                    }
                    else
                    {
                        ReadQueryObject(parameter, parameterLocation, query);
                    }

                    break;
            }
        }

        foreach (var token in routeTokens)
        {
            if (route.TryGetValue(token, out var parameter))
            {
                endpoint.ParameterList.Add(parameter);
            }
            else
            {
                _context.Problem(location, $"no parameter of the action reads the route value '{{{token}}}'.");
            }
        }

        if (body != null)
        {
            endpoint.ParameterList.Add(body);
        }

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in query)
        {
            if (!keys.Add(parameter.WireName))
            {
                _context.Problem(location, $"the query-string key '{parameter.WireName}' is read twice.");
                continue;
            }

            endpoint.ParameterList.Add(parameter);
        }
    }

    /// <summary>A class read from the query string: each of its settable single-value properties is one key.</summary>
    private void ReadQueryObject(ParameterInfo parameter, string parameterLocation, List<EndpointParameter> query)
    {
        var type = parameter.ParameterType;
        if (type.IsArray || type.IsGenericType || type.IsInterface || type.IsAbstract || (type.Namespace ?? string.Empty).StartsWith("System", StringComparison.Ordinal))
        {
            _context.Problem(parameterLocation, $"the type {TypeReader.Display(type)} cannot be read from the query string. Use single values, or a class whose properties are single values.");
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0 || property.SetMethod == null || !property.SetMethod.IsPublic)
            {
                continue;
            }

            var attributes = property.GetCustomAttributesData();
            if (Attributes.Has(attributes, KnownNames.WireIgnore))
            {
                continue;
            }

            var propertyLocation = $"{parameterLocation}.{property.Name}";
            if (!_context.TypeReader.IsSimple(property.PropertyType))
            {
                _context.Problem(propertyLocation, "a property read from the query string must be a single value (text, a number, an id, a date, an enum).");
                continue;
            }

            var propertyType = _context.TypeReader.Read(property.PropertyType, propertyLocation, attributes, Optionality.ForProperty(property));
            query.Add(new EndpointParameter(property.Name, WireNames.JsonCamelCase(property.Name), ParameterSource.Query, propertyType));
        }
    }

    private void ReadResponse(RestEndpoint endpoint, Type type, MethodInfo method, string location,
        IList<CustomAttributeData> attributes, List<CustomAttributeData> classAttributes)
    {
        if (Attributes.Has(attributes, KnownNames.WireFile))
        {
            endpoint.IsFile = true;
            endpoint.SuccessStatus = 200;
            if (endpoint.HttpMethod != "GET")
            {
                _context.Problem(location, "a [WireFile] action must answer GET, because the browser fetches it by address.");
            }

            if (endpoint.Parameters.Any(p => p.Source == ParameterSource.Body))
            {
                _context.Problem(location, "a [WireFile] action cannot read a body.");
            }

            return;
        }

        //What the return type itself declares.
        var returned = Unwrap(method.ReturnType);
        Type returnDeclared = null;
        if (returned != null)
        {
            if (Attributes.FullNameOf(returned) == KnownNames.ActionResultOfT)
            {
                returnDeclared = returned.GetGenericArguments()[0];
            }
            else if (!IsResultType(returned))
            {
                returnDeclared = returned;
            }
        }

        //What [ProducesResponseType] declares, on the action and on its controller.
        var declared = new List<(int Status, Type Type)>();
        foreach (var attribute in attributes.Concat(classAttributes))
        {
            if (Attributes.Is(attribute, KnownNames.ProducesResponseTypeOfT))
            {
                var status = attribute.ConstructorArguments.Select(a => a.Value).OfType<int>().FirstOrDefault();
                declared.Add((status, attribute.AttributeType.GetGenericArguments()[0]));
            }
            else if (Attributes.Is(attribute, KnownNames.ProducesResponseType))
            {
                var status = attribute.ConstructorArguments.Select(a => a.Value).OfType<int>().FirstOrDefault();
                var declaredType = attribute.ConstructorArguments.Select(a => a.Value).OfType<Type>().FirstOrDefault()
                    ?? Attributes.Named(attribute, "Type") as Type;
                if (declaredType != null && declaredType.FullName == KnownNames.Void)
                {
                    declaredType = null;
                }

                declared.Add((status, declaredType));
            }
        }

        foreach (var (status, declaredType) in declared.Where(d => d.Status < 200 || d.Status > 299))
        {
            if (declaredType != null && declaredType.FullName != KnownNames.ApiProblem)
            {
                _context.Problem(location,
                    $"the {status} answer is declared with the body {TypeReader.Display(declaredType)}. Every error answer of a platform backend is an ApiProblem (and a 401 has no body).");
            }
        }

        var successes = declared.Where(d => d.Status >= 200 && d.Status <= 299).ToList();
        if (successes.Count == 0 && returnDeclared == null)
        {
            _context.Problem(location,
                "the success answer is not declared. Add [ProducesResponseType(typeof(T), 200)] (or [ProducesResponseType(204)] for an answer without a body), or return ActionResult<T>.");
            return;
        }

        var bodies = successes.Where(s => s.Type != null).Select(s => s.Type).ToList();
        if (returnDeclared != null)
        {
            bodies.Add(returnDeclared);
        }

        var distinctBodies = bodies.Select(TypeReader.ClrKey).Distinct(StringComparer.Ordinal).ToList();
        var hasEmptySuccess = successes.Any(s => s.Type == null) && !(returnDeclared != null && successes.All(s => s.Type == null));
        if (distinctBodies.Count > 1)
        {
            _context.Problem(location, $"the success answer is declared with different bodies ({string.Join(", ", bodies.Select(TypeReader.Display).Distinct())}); a generated function returns one type.");
            return;
        }

        if (distinctBodies.Count == 1 && hasEmptySuccess)
        {
            _context.Problem(location, "one success answer has a body and another has none; a generated function returns one shape. Split the action or always return the body.");
            return;
        }

        endpoint.SuccessStatus = successes.Count > 0 ? successes.Min(s => s.Status) : 200;
        if (distinctBodies.Count == 1)
        {
            endpoint.Response = _context.TypeReader.Read(bodies[0], location + " (response)", null, null);
        }
    }

    /// <summary>The type inside Task / ValueTask; null for void and for a task without a result.</summary>
    internal static Type Unwrap(Type returnType)
    {
        var name = Attributes.FullNameOf(returnType);
        if (name == KnownNames.Void || name == KnownNames.Task || name == KnownNames.ValueTask)
        {
            return null;
        }

        if (name == KnownNames.TaskOfT || name == KnownNames.ValueTaskOfT)
        {
            return returnType.GetGenericArguments()[0];
        }

        return returnType;
    }

    private static bool IsResultType(Type type)
    {
        if (type.FullName == KnownNames.IActionResult || type.FullName == KnownNames.IResult || type.FullName == KnownNames.Object)
        {
            return true;
        }

        return type.GetInterfaces().Any(i => i.FullName == KnownNames.IActionResult || i.FullName == KnownNames.IResult);
    }
}
