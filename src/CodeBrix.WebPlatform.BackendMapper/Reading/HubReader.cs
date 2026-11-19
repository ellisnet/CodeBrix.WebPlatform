using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeBrix.WebPlatform.BackendMapper.Model;

namespace CodeBrix.WebPlatform.BackendMapper.Reading;

/// <summary>Reads the SignalR hubs of a backend assembly.</summary>
internal sealed class HubReader
{
    private readonly MapContext _context;

    public HubReader(MapContext context)
    {
        _context = context;
    }

    public static bool IsHub(Type type)
    {
        if (!type.IsClass || type.IsAbstract || !type.IsPublic || type.IsGenericTypeDefinition)
        {
            return false;
        }

        for (var current = type.BaseType; current != null; current = current.BaseType)
        {
            if (current.FullName == KnownNames.Hub)
            {
                return true;
            }
        }

        return false;
    }

    public HubMap Read(Type type)
    {
        var problemsBefore = _context.Problems.Count;
        var attributes = type.GetCustomAttributesData();
        var path = Attributes.Find(attributes, KnownNames.WireHub) is { } wireHub ? Attributes.FirstString(wireHub) : null;
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/'))
        {
            _context.Problem(type.Name, "the hub has no [WireHub(\"/path\")] attribute with a path that starts with '/'. The frontend needs the path, and the backend maps the hub with WireHubAttribute.GetPath.");
            path = "/";
        }

        var hub = new HubMap(type.Name, type.FullName, path);
        var classAttributes = Attributes.OfTypeAndBases(type).ToList();
        if (Attributes.Has(classAttributes, KnownNames.AllowAnonymous))
        {
            hub.RequiresSignIn = false;
        }
        else if (Attributes.Has(classAttributes, KnownNames.Authorize))
        {
            hub.RequiresSignIn = true;
            _context.SignInSeen = true;
        }
        else
        {
            _context.Problem(type.Name, "the hub does not say who may connect. Add [Authorize] or [AllowAnonymous] to the hub class.");
        }

        Type clientInterface = null;
        var chain = new List<Type>();
        for (var current = type; current != null && current.FullName != KnownNames.Hub; current = current.BaseType)
        {
            if (Attributes.FullNameOf(current) == KnownNames.HubOfT)
            {
                clientInterface = current.GetGenericArguments()[0];
                break;
            }

            chain.Add(current);
        }

        if (clientInterface == null)
        {
            _context.Problem(type.Name, "the hub derives from the untyped Hub, so what it sends to the frontend cannot be read. Derive from Hub<TClient> with an interface that names each message.");
        }
        else if (!clientInterface.IsInterface)
        {
            _context.Problem(type.Name, $"the hub's client type {clientInterface.Name} is not an interface.");
        }
        else
        {
            hub.ClientInterfaceClrName = clientInterface.FullName;
            ReadMessages(hub, clientInterface);
        }

        chain.Reverse();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var declaring in chain)
        {
            foreach (var method in declaring.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName || method.IsGenericMethodDefinition
                    || IsOverride(method)
                    || Attributes.Has(method.GetCustomAttributesData(), KnownNames.WireIgnore))
                {
                    continue;
                }

                var location = $"{type.Name}.{method.Name}";
                if (!names.Add(method.Name))
                {
                    _context.Problem(location, "the hub has two methods with this name; SignalR does not allow overloads.");
                    continue;
                }

                var hubMethod = ReadMethod(type, method, location);
                if (hubMethod != null)
                {
                    hub.MethodList.Add(hubMethod);
                }
            }
        }

        return _context.Problems.Count == problemsBefore ? hub : null;
    }

    private HubMethod ReadMethod(Type hubType, MethodInfo method, string location)
    {
        var methodAttributes = method.GetCustomAttributesData();
        if (Attributes.Has(methodAttributes, KnownNames.Authorize) || Attributes.Has(methodAttributes, KnownNames.AllowAnonymous))
        {
            _context.Problem(location, "who may call a hub is declared on the hub class only; remove the attribute from the method.");
        }

        var returned = ControllerReader.Unwrap(method.ReturnType);
        var kind = HubMethodKind.Invoke;
        if (returned != null && IsStream(returned))
        {
            kind = HubMethodKind.Stream;
            returned = returned.GetGenericArguments()[0];
        }

        var hubMethod = new HubMethod(method.Name, kind);
        foreach (var parameter in method.GetParameters())
        {
            if (parameter.ParameterType.FullName == KnownNames.CancellationToken)
            {
                continue;
            }

            var parameterLocation = $"{location}({parameter.Name})";
            if (IsStream(parameter.ParameterType))
            {
                _context.Problem(parameterLocation, "a stream from the frontend to the hub is not supported.");
                continue;
            }

            hubMethod.ArgumentList.Add(new HubArgument(parameter.Name, _context.TypeReader.Read(parameter.ParameterType, parameterLocation,
                parameter.GetCustomAttributesData(), Optionality.ForParameter(parameter, method, hubType))));
        }

        if (returned != null)
        {
            hubMethod.Result = kind == HubMethodKind.Stream
                ? _context.TypeReader.Read(returned, location + " (item)", null, null)
                : _context.TypeReader.Read(returned, location + " (result)", method.ReturnParameter.GetCustomAttributesData(), Optionality.ForReturn(method, hubType));
        }

        return hubMethod;
    }

    private void ReadMessages(HubMap hub, Type clientInterface)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var contract in new[] { clientInterface }.Concat(clientInterface.GetInterfaces()))
        {
            foreach (var method in contract.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName || Attributes.Has(method.GetCustomAttributesData(), KnownNames.WireIgnore))
                {
                    continue;
                }

                var location = $"{clientInterface.Name}.{method.Name}";
                if (!names.Add(method.Name))
                {
                    _context.Problem(location, "the client interface has two messages with this name.");
                    continue;
                }

                var returnName = Attributes.FullNameOf(method.ReturnType);
                if (returnName != KnownNames.Task && returnName != KnownNames.ValueTask && returnName != KnownNames.Void)
                {
                    _context.Problem(location, "a message that expects an answer from the frontend is not supported; a client interface method returns Task.");
                    continue;
                }

                var message = new HubMessage(method.Name);
                foreach (var parameter in method.GetParameters())
                {
                    if (parameter.ParameterType.FullName == KnownNames.CancellationToken)
                    {
                        continue;
                    }

                    message.ArgumentList.Add(new HubArgument(parameter.Name, _context.TypeReader.Read(parameter.ParameterType,
                        $"{location}({parameter.Name})", parameter.GetCustomAttributesData(), Optionality.ForParameter(parameter, method, contract))));
                }

                hub.MessageList.Add(message);
            }
        }
    }

    /// <summary>True for a method that overrides one of a base class (OnConnectedAsync, Dispose, ...).</summary>
    private static bool IsOverride(MethodInfo method)
        => (method.Attributes & MethodAttributes.Virtual) != 0 && (method.Attributes & MethodAttributes.NewSlot) == 0;

    private static bool IsStream(Type type)
    {
        var name = Attributes.FullNameOf(type);
        return name == KnownNames.AsyncEnumerableOfT || name == KnownNames.ChannelReaderOfT;
    }
}
