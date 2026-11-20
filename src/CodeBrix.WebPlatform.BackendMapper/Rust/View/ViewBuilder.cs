using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CodeBrix.WebPlatform.BackendMapper.Model;
using CodeBrix.WebPlatform.BackendMapper.Reading;

namespace CodeBrix.WebPlatform.BackendMapper.Rust.View;

/// <summary>Works out every Rust name, type and expression for a mapped backend.</summary>
internal sealed class ViewBuilder
{
    private const string MapImport = "std::collections::BTreeMap";

    /// <summary>The Rust types of the default type map, all of which have a natural default value.</summary>
    private static readonly HashSet<string> DefaultableScalars = new(StringComparer.Ordinal)
    {
        "String", "bool", "u8", "i8", "i16", "u16", "i32", "u32", "i64", "u64", "f32", "f64", "Date", "serde_json::Value",
    };

    private readonly RustClientOptions _options;

    public ViewBuilder(RustClientOptions options)
    {
        _options = options;
    }

    public ClientView Build(BackendMap map)
    {
        var view = new ClientView
        {
            Marker = RustClientGenerator.GeneratedMarker,
            AssemblyName = map.AssemblyName,
            SharedCrate = _options.SharedCratePath,
            SignalrModule = _options.SignalRModulePath,
            HasApi = map.Controllers.Any(c => c.Endpoints.Count > 0),
            HasHubs = map.Hubs.Count > 0,
        };

        var modelImports = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var wireStruct in map.Structs)
        {
            view.Structs.Add(BuildStruct(wireStruct, modelImports));
        }

        foreach (var wireEnum in map.Enums)
        {
            view.Enums.Add(BuildEnum(wireEnum));
        }

        view.ModelImports.AddRange(modelImports);

        var apiImports = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var controller in map.Controllers.Where(c => c.Endpoints.Count > 0))
        {
            view.Controllers.Add(BuildController(controller, apiImports));
        }

        view.ApiImports.AddRange(apiImports);

        var hubImports = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var hub in map.Hubs)
        {
            view.Hubs.Add(BuildHub(hub, hubImports));
        }

        view.HubImports.AddRange(hubImports);
        BuildSetup(map, view);
        return view;
    }

    /// <summary>
    /// The request marker, the session-refresh path and the parameters of the generated set-up
    /// function, which hands them to the shared crate's one set-up call.
    /// </summary>
    private void BuildSetup(BackendMap map, ClientView view)
    {
        if (map.RequestMarker != null)
        {
            view.HasRequestMarker = true;
            view.RequestMarker = WireNames.RustString(map.RequestMarker);
        }

        if (map.SessionRefresh == null)
        {
            return;
        }

        var imports = new SortedSet<string>(StringComparer.Ordinal);
        view.HasSessionRefresh = true;
        view.SessionRefreshPath = WireNames.RustString(map.SessionRefresh.RouteTemplate);
        view.SessionRefreshAction = $"{map.SessionRefreshController.ClrName}.{map.SessionRefresh.ActionName}";
        if (map.SessionRefresh.Response != null)
        {
            view.SessionRefreshAnswer = Owned(map.SessionRefresh.Response, imports);
            view.SetupUsesModels = UsesModels(map.SessionRefresh.Response);
            view.SetupParameters = $"on_session_refreshed: impl Fn({view.SessionRefreshAnswer}) + 'static, on_session_ended: impl Fn() + 'static";
            view.SessionRefreshedHook = $"{_options.SharedCratePath}::setup::json_hook(on_session_refreshed)";
        }
        else
        {
            view.SetupParameters = "on_session_refreshed: impl Fn() + 'static, on_session_ended: impl Fn() + 'static";
            view.SessionRefreshedHook = "Rc::new(move |_: &str| on_session_refreshed())";
        }

        view.SetupImports.AddRange(imports);
    }

    private static bool UsesModels(WireType type)
        => type.Kind switch
        {
            WireTypeKind.Struct or WireTypeKind.Enum => true,
            WireTypeKind.List or WireTypeKind.Map => UsesModels(type.Element),
            _ => false,
        };

    // ---- Types -------------------------------------------------------------------------------

    /// <summary>The Rust type of a value as it is stored (in a struct field, a return type).</summary>
    private string Owned(WireType type, ISet<string> imports, bool withOptional = true)
    {
        string text;
        switch (type.Kind)
        {
            case WireTypeKind.Scalar:
                text = type.Rust.Name;
                AddImport(type.Rust.Import, imports);
                break;
            case WireTypeKind.List:
                text = $"Vec<{Owned(type.Element, imports)}>";
                break;
            case WireTypeKind.Map:
                imports.Add(MapImport);
                text = $"BTreeMap<String, {Owned(type.Element, imports)}>";
                break;
            case WireTypeKind.Struct:
                text = type.Struct.Name;
                break;
            default:
                text = type.Enum.Name;
                break;
        }

        return withOptional && type.IsOptional ? $"Option<{text}>" : text;
    }

    private void AddImport(string import, ISet<string> imports)
    {
        if (string.IsNullOrWhiteSpace(import))
        {
            return;
        }

        //The shared crate may be known to the application under another path.
        const string shared = "codebrix_webplatform::";
        imports.Add(import.StartsWith(shared, StringComparison.Ordinal) && _options.SharedCratePath != "codebrix_webplatform"
            ? _options.SharedCratePath + "::" + import.Substring(shared.Length)
            : import);
    }

    private static bool IsCopy(WireType type)
        => type.Kind == WireTypeKind.Enum || (type.Kind == WireTypeKind.Scalar && type.Rust.IsCopy);

    private static bool IsText(WireType type) => type.Kind == WireTypeKind.Scalar && type.Rust.Name == "String";

    /// <summary>The Rust type of a value as a function takes it.</summary>
    private string Borrowed(WireType type, ISet<string> imports)
    {
        string text;
        if (IsText(type))
        {
            text = "&str";
        }
        else if (IsCopy(type))
        {
            text = Owned(type, imports, withOptional: false);
        }
        else if (type.Kind == WireTypeKind.List)
        {
            text = $"&[{Owned(type.Element, imports)}]";
        }
        else
        {
            text = "&" + Owned(type, imports, withOptional: false);
        }

        return type.IsOptional ? $"Option<{text}>" : text;
    }

    /// <summary>True when every value of the type has a natural default, so a struct of them can derive Default.</summary>
    private static bool HasDefault(WireType type, HashSet<string> visiting)
    {
        if (type.IsOptional || type.Kind == WireTypeKind.List || type.Kind == WireTypeKind.Map || type.Kind == WireTypeKind.Enum)
        {
            return true;
        }

        if (type.Kind == WireTypeKind.Scalar)
        {
            return type.Rust.With == null && DefaultableScalars.Contains(type.Rust.Name)
                && (type.Rust.Import == null || type.Rust.Import == TypeMapping.RustTypeMap.SharedDateImport);
        }

        if (!visiting.Add(type.Struct.ClrName))
        {
            return false;
        }

        var result = type.Struct.Fields.All(f => HasDefault(f.Type, visiting));
        visiting.Remove(type.Struct.ClrName);
        return result;
    }

    // ---- Models ------------------------------------------------------------------------------

    private StructView BuildStruct(WireStruct wireStruct, ISet<string> imports)
    {
        var hasDefault = wireStruct.Fields.All(f => HasDefault(f.Type, new HashSet<string>(StringComparer.Ordinal) { wireStruct.ClrName }));
        var view = new StructView
        {
            Name = wireStruct.Name,
            ClrName = wireStruct.ClrName,
            Derives = hasDefault ? "Clone, Debug, Default, PartialEq, Serialize, Deserialize" : "Clone, Debug, PartialEq, Serialize, Deserialize",
        };
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in wireStruct.Fields)
        {
            var snake = Unique(WireNames.SnakeCase(field.ClrName), taken);
            var fieldView = new FieldView { Name = WireNames.RustIdentifier(snake), Type = Owned(field.Type, imports) };
            if (WireNames.SerdeCamelCase(snake) != field.JsonName)
            {
                fieldView.Attributes.Add($"#[serde(rename = \"{WireNames.RustString(field.JsonName)}\")]");
            }

            if (field.Type.IsOptional)
            {
                fieldView.Attributes.Add("#[serde(default)]");
            }

            if (field.Type.Kind == WireTypeKind.Scalar && field.Type.Rust.With != null)
            {
                fieldView.Attributes.Add($"#[serde(with = \"{WireNames.RustString(field.Type.Rust.With)}\")]");
            }

            view.Fields.Add(fieldView);
        }

        return view;
    }

    private static EnumView BuildEnum(WireEnum wireEnum)
    {
        var view = new EnumView { Name = wireEnum.Name, ClrName = wireEnum.ClrName, AsText = wireEnum.AsText, Count = wireEnum.Members.Count };
        var values = new HashSet<long>();
        foreach (var member in wireEnum.Members)
        {
            var memberView = new EnumMemberView
            {
                Name = member.Name,
                WireName = WireNames.RustString(member.Name),
                Value = member.Value,
                IsFirst = view.Members.Count == 0,
            };
            view.Members.Add(memberView);
            if (values.Add(member.Value))
            {
                view.DistinctMembers.Add(memberView);
            }
        }

        return view;
    }

    // ---- REST --------------------------------------------------------------------------------

    private ControllerView BuildController(RestController controller, ISet<string> imports)
    {
        var view = new ControllerView
        {
            Name = controller.Name,
            ClrName = controller.ClrName,
            Module = WireNames.RustIdentifier(WireNames.SnakeCase(controller.Name)),
        };
        foreach (var endpoint in controller.Endpoints)
        {
            view.Functions.Add(BuildFunction(endpoint, view, imports));
        }

        return view;
    }

    private FunctionView BuildFunction(RestEndpoint endpoint, ControllerView controller, ISet<string> imports)
    {
        var baseName = ControllerReader.FunctionBaseName(endpoint.ActionName);
        var snake = WireNames.SnakeCase(baseName);
        var taken = new HashSet<string>(StringComparer.Ordinal) { "query_string" };
        var parameters = new List<string>();
        var formatText = new StringBuilder();
        var formatArguments = new List<string>();
        var routeParameters = endpoint.Parameters.Where(p => p.Source == ParameterSource.Route)
            .ToDictionary(p => p.WireName, StringComparer.OrdinalIgnoreCase);
        var routeNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in endpoint.Parameters.Where(p => p.Source == ParameterSource.Route))
        {
            var name = WireNames.RustIdentifier(Unique(WireNames.SnakeCase(parameter.Name), taken));
            routeNames[parameter.WireName] = name;
            parameters.Add($"{name}: {Borrowed(parameter.Type, imports)}");
        }

        foreach (var segment in endpoint.Route)
        {
            if (!segment.IsParameter)
            {
                formatText.Append(segment.Text.Replace("{", "{{", StringComparison.Ordinal).Replace("}", "}}", StringComparison.Ordinal));
                continue;
            }

            formatText.Append("{}");
            var name = routeNames[segment.Text];
            formatArguments.Add(IsText(routeParameters[segment.Text].Type) ? $"encode({name})" : $"encode(&{name}.to_string())");
        }

        var body = endpoint.Parameters.FirstOrDefault(p => p.Source == ParameterSource.Body);
        var bodyExpression = "None";
        if (body != null)
        {
            var name = WireNames.RustIdentifier(Unique(WireNames.SnakeCase(body.Name), taken));
            parameters.Add($"{name}: &{Owned(body.Type, imports, withOptional: false)}");
            bodyExpression = $"Some(client::body({name})?)";
        }

        var queryParameters = endpoint.Parameters.Where(p => p.Source == ParameterSource.Query).ToList();
        var queryStatement = string.Empty;
        if (queryParameters.Count > 0)
        {
            var pushes = new StringBuilder("let query_string = QueryString::new()");
            if (queryParameters.Count > _options.MaxSeparateQueryArguments)
            {
                var structView = new QueryStructView
                {
                    Name = baseName + "Query",
                    Doc = $"The query-string values of [`{snake}`].",
                };
                var fieldNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (var parameter in queryParameters)
                {
                    var field = WireNames.RustIdentifier(Unique(WireNames.SnakeCase(parameter.Name), fieldNames));
                    structView.Fields.Add(new FieldView { Name = field, Type = Owned(parameter.Type, imports) });
                    var value = IsCopy(parameter.Type) ? $"query.{field}" : parameter.Type.IsOptional ? $"query.{field}.as_ref()" : $"&query.{field}";
                    pushes.Append(Push(parameter, value));
                }

                controller.QueryStructs.Add(structView);
                Unique("query", taken);
                parameters.Add($"query: &{structView.Name}");
            }
            else
            {
                foreach (var parameter in queryParameters)
                {
                    var name = WireNames.RustIdentifier(Unique(WireNames.SnakeCase(parameter.Name), taken));
                    parameters.Add($"{name}: {Borrowed(parameter.Type, imports)}");
                    pushes.Append(Push(parameter, name));
                }
            }

            queryStatement = pushes.Append(".build();").ToString();
            formatText.Append("{}");
            formatArguments.Add("query_string");
        }

        var path = formatArguments.Count == 0
            ? $"\"{WireNames.RustString(formatText.ToString().Replace("{{", "{", StringComparison.Ordinal).Replace("}}", "}", StringComparison.Ordinal))}\""
            : $"format!(\"{WireNames.RustString(formatText.ToString())}\", {string.Join(", ", formatArguments)})";
        var needsSignIn = endpoint.RequiresSignIn ? "needs sign-in" : "open to everyone";
        var function = new FunctionView
        {
            Name = WireNames.RustIdentifier(endpoint.IsFile ? snake + "_url" : snake),
            Doc = endpoint.IsFile
                ? $"The address of `{endpoint.HttpMethod} {endpoint.RouteTemplate}` ({needsSignIn}), a file for the browser to fetch."
                : $"`{endpoint.HttpMethod} {endpoint.RouteTemplate}` ({needsSignIn}).",
            Parameters = string.Join(", ", parameters),
            IsFile = endpoint.IsFile,
            HasResponse = endpoint.Response != null,
            ReturnType = endpoint.IsFile ? "String" : endpoint.Response != null ? Owned(endpoint.Response, imports) : "()",
            Method = endpoint.HttpMethod,
            Auth = endpoint.RequiresSignIn ? "Auth::Session" : "Auth::Public",
            QueryStatement = queryStatement,
            Path = formatArguments.Count == 0 && !endpoint.IsFile ? path : formatArguments.Count == 0 ? path + ".to_string()" : path,
            Body = bodyExpression,
        };
        if (endpoint.IsFile)
        {
            //Beside the address, a function that reads the file itself, through the same client.
            function.BytesName = WireNames.RustIdentifier(snake + "_bytes");
            function.BytesDoc = $"`{endpoint.HttpMethod} {endpoint.RouteTemplate}` ({needsSignIn}), the file read as bytes.";
            function.BytesReturnType = "Vec<u8>";
            function.BytesPath = formatArguments.Count == 0 ? path : "&" + path;
        }

        return function;
    }

    private static string Push(EndpointParameter parameter, string value)
        => parameter.Type.IsOptional
            ? $".push_opt(\"{WireNames.RustString(parameter.WireName)}\", {value})"
            : $".push(\"{WireNames.RustString(parameter.WireName)}\", {value})";

    // ---- Hubs --------------------------------------------------------------------------------

    private HubView BuildHub(HubMap hub, ISet<string> imports)
    {
        var view = new HubView
        {
            Name = hub.Name,
            ClrName = hub.ClrName,
            Descriptor = WireNames.ScreamingSnakeCase(hub.Name),
            Path = WireNames.RustString(hub.Path),
            RequiresSignIn = hub.RequiresSignIn ? "true" : "false",
            SignInDoc = hub.RequiresSignIn ? "needs sign-in" : "open to everyone",
        };
        var functionNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "connect", "use_hub", "connection", "state", "use_state",
        };
        foreach (var method in hub.Methods)
        {
            var taken = new HashSet<string>(StringComparer.Ordinal) { "self" };
            var parameters = new List<string> { "&self" };
            var arguments = new List<string>();
            foreach (var argument in method.Arguments)
            {
                var name = WireNames.RustIdentifier(Unique(WireNames.SnakeCase(argument.Name), taken));
                parameters.Add($"{name}: {Borrowed(argument.Type, imports)}");
                arguments.Add($"to_arg(&{name})");
            }

            view.Methods.Add(new HubMethodView
            {
                Name = WireNames.RustIdentifier(Unique(WireNames.SnakeCase(ControllerReader.FunctionBaseName(method.Name)), functionNames)),
                WireName = WireNames.RustString(method.Name),
                IsStream = method.Kind == HubMethodKind.Stream,
                Parameters = string.Join(", ", parameters),
                Arguments = arguments.Count == 0 ? "Vec::new()" : $"vec![{string.Join(", ", arguments)}]",
                ResultType = method.Result != null ? Owned(method.Result, imports) : "()",
            });
        }

        foreach (var message in hub.Messages)
        {
            var snake = WireNames.SnakeCase(message.Name);
            var messageView = new HubMessageView
            {
                Name = snake,
                WireName = WireNames.RustString(message.Name),
                Decoder = "decode_" + WireNames.SnakeCase(hub.Name) + "_" + snake,
            };
            if (message.Arguments.Count == 0)
            {
                messageView.PayloadType = "()";
                messageView.Decode = "Some(())";
            }
            else if (message.Arguments.Count == 1)
            {
                messageView.PayloadType = Owned(message.Arguments[0].Type, imports);
                messageView.Decode = $"arg::<{messageView.PayloadType}>(args, 0)";
            }
            else
            {
                messageView.PayloadType = hub.Name + WireNames.TypeName(message.Name) + "Args";
                messageView.HasArgsStruct = true;
                var taken = new HashSet<string>(StringComparer.Ordinal);
                var fields = new List<string>();
                for (var i = 0; i < message.Arguments.Count; i++)
                {
                    var argument = message.Arguments[i];
                    var field = WireNames.RustIdentifier(Unique(WireNames.SnakeCase(argument.Name), taken));
                    var type = Owned(argument.Type, imports);
                    messageView.ArgsFields.Add(new FieldView { Name = field, Type = type });
                    fields.Add($"{field}: arg::<{type}>(args, {i})?");
                }

                messageView.Decode = $"Some({messageView.PayloadType} {{ {string.Join(", ", fields)} }})";
            }

            view.Messages.Add(messageView);
        }

        return view;
    }

    private static string Unique(string name, HashSet<string> taken)
    {
        var candidate = name;
        for (var i = 2; !taken.Add(candidate); i++)
        {
            candidate = name + "_" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return candidate;
    }
}
