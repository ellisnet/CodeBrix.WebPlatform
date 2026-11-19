using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CodeBrix.RustTools.Licensing;

namespace CodeBrix.WebPlatform.LibraryChecker;

/// <summary>
/// The licenses the repository accepts in the third-party code it carries outside
/// <c>samples/</c>, read from <c>tools/licensing/license-allowlist.json</c>, in three tiers:
/// anywhere (including what is compiled into a platform application and the distributed tools),
/// vendored but not compiled into an application, and build tools only - plus the licenses accepted only for
/// named crates compiled into the distributed tools (<c>dx</c> and <c>wasm-bindgen</c>).
/// </summary>
public sealed class LicenseAllowlist
{
    /// <summary>Licenses accepted anywhere, including in the compiled application.</summary>
    public IReadOnlyList<OpenSourceLicense> Everywhere { get; init; } = Array.Empty<OpenSourceLicense>();

    /// <summary>Licenses accepted in a vendored crate that is not compiled into the application.</summary>
    public IReadOnlyList<OpenSourceLicense> VendoredOnly { get; init; } = Array.Empty<OpenSourceLicense>();

    /// <summary>Licenses accepted in the build tools' tree only.</summary>
    public IReadOnlyList<OpenSourceLicense> ToolingOnly { get; init; } = Array.Empty<OpenSourceLicense>();

    /// <summary>SPDX license exceptions accepted in the build tools' tree only.</summary>
    public IReadOnlyList<string> ToolingOnlyExceptions { get; init; } = Array.Empty<string>();

    /// <summary>Which license to elect when a crate offers a choice, most preferred first.</summary>
    public IReadOnlyList<OpenSourceLicense> Preferred { get; init; } = Array.Empty<OpenSourceLicense>();

    /// <summary>SPDX license exceptions accepted in the code compiled into the distributed tools.</summary>
    public IReadOnlyList<string> DistributedToolExceptions { get; init; } = Array.Empty<string>();

    /// <summary>Crates compiled into the distributed tools whose bundled license files were reviewed by hand (crate name to reason).</summary>
    public IReadOnlyDictionary<string, string> DistributedToolBundledOverrides { get; init; } = new Dictionary<string, string>();

    /// <summary>Licenses accepted in the code compiled into the distributed tools for named crates only.</summary>
    public IReadOnlyList<NamedCrateLicense> NamedCrateLicenses { get; init; } = Array.Empty<NamedCrateLicense>();

    /// <summary>Frontend crates whose bundled license files were reviewed by hand (crate name to reason).</summary>
    public IReadOnlyDictionary<string, string> ApplicationBundledOverrides { get; init; } = new Dictionary<string, string>();

    /// <summary>Build-tool crates whose bundled license files were reviewed by hand (crate name to reason).</summary>
    public IReadOnlyDictionary<string, string> ToolingBundledOverrides { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Reads the allowlist file.
    /// </summary>
    /// <param name="path">The JSON file.</param>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="InvalidDataException">The file is not valid, or names a license that is
    /// not a known SPDX identifier.</exception>
    public static LicenseAllowlist Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"The license allowlist '{path}' does not exist.", path);
        }

        return Parse(File.ReadAllText(path));
    }

    /// <summary>
    /// Reads the allowlist from JSON text.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <exception cref="InvalidDataException">The text is not valid, or names a license that is
    /// not a known SPDX identifier.</exception>
    public static LicenseAllowlist Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The license allowlist is not valid JSON: " + exception.Message, exception);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("The license allowlist must be a JSON object.");
            }

            var overrides = root.TryGetProperty("bundledLicenseOverrides", out var o) && o.ValueKind == JsonValueKind.Object ? o : default;
            var distributed = default(JsonElement);
            if (root.TryGetProperty("compiledIntoTheDistributedTools", out var d))
            {
                if (d.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException("'compiledIntoTheDistributedTools' in the license allowlist must be an object.");
                }

                distributed = d;
            }

            return new LicenseAllowlist
            {
                Everywhere = ReadLicenses(root, "everywhere"),
                VendoredOnly = ReadLicenses(root, "vendoredButNotCompiledIntoTheApplication"),
                ToolingOnly = ReadLicenses(root, "toolingOnly"),
                ToolingOnlyExceptions = ReadStrings(root, "toolingOnlyExceptions"),
                Preferred = ReadLicenses(root, "preferredWhenACrateOffersAChoice"),
                DistributedToolExceptions = distributed.ValueKind == JsonValueKind.Object ? ReadStrings(distributed, "exceptions") : Array.Empty<string>(),
                NamedCrateLicenses = ReadNamedCrateLicenses(distributed),
                DistributedToolBundledOverrides = ReadMap(distributed, "bundledLicenseOverrides"),
                ApplicationBundledOverrides = ReadMap(overrides, "application"),
                ToolingBundledOverrides = ReadMap(overrides, "tooling"),
            };
        }
    }

    /// <summary>
    /// The policy for crates compiled into the application: the "everywhere" tier only.
    /// </summary>
    public LicensePolicy BuildApplicationPolicy() => BuildPolicy(Everywhere, Array.Empty<string>(), ApplicationBundledOverrides);

    /// <summary>
    /// The policy for crates vendored for the frontend: the "everywhere" and "vendored" tiers.
    /// </summary>
    public LicensePolicy BuildVendoredPolicy() => BuildPolicy(Everywhere.Concat(VendoredOnly), Array.Empty<string>(), ApplicationBundledOverrides);

    /// <summary>
    /// The policy for the build tools' crates: all three tiers and the tooling exceptions.
    /// </summary>
    public LicensePolicy BuildToolingPolicy() => BuildPolicy(Everywhere.Concat(VendoredOnly).Concat(ToolingOnly), ToolingOnlyExceptions, ToolingBundledOverrides);

    /// <summary>
    /// The policy for the crates compiled into a distributed tool: the "everywhere" tier, the
    /// distributed-tool exceptions, the licenses accepted for named crates of that tool, and the
    /// tooling and distributed-tool bundled-license overrides. A license that is in the policy only because named
    /// crates may use it must still be checked crate by crate (see
    /// <see cref="BuildDistributedToolBasePolicy"/> and <see cref="NamedCrateLicenses"/>).
    /// </summary>
    /// <param name="tool">The tool's package name (for example <c>dioxus-cli</c>).</param>
    public LicensePolicy BuildDistributedToolPolicy(string tool)
    {
        var named = NamedCrateLicenses.Where(n => n.AppliesToTool(tool)).Select(n => n.License);
        return BuildPolicy(Everywhere.Concat(named).Distinct(), DistributedToolExceptions, DistributedOverrides);
    }

    /// <summary>
    /// The policy for the crates compiled into a distributed tool WITHOUT the licenses accepted
    /// for named crates: what every crate that is not named must satisfy.
    /// </summary>
    public LicensePolicy BuildDistributedToolBasePolicy() => BuildPolicy(Everywhere, DistributedToolExceptions, DistributedOverrides);

    /// <summary>
    /// The base distributed-tool policy plus the licenses accepted for one named crate of a tool:
    /// what that crate must satisfy.
    /// </summary>
    /// <param name="tool">The tool's package name.</param>
    /// <param name="crateName">The crate's name.</param>
    public LicensePolicy BuildDistributedToolPolicyFor(string tool, string crateName)
    {
        var named = NamedCrateLicenses.Where(n => n.AppliesTo(tool, crateName)).Select(n => n.License);
        return BuildPolicy(Everywhere.Concat(named).Distinct(), DistributedToolExceptions, DistributedOverrides);
    }

    private IReadOnlyDictionary<string, string> DistributedOverrides
    {
        get
        {
            var all = new Dictionary<string, string>(ToolingBundledOverrides, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in DistributedToolBundledOverrides)
            {
                all[pair.Key] = pair.Value;
            }

            return all;
        }
    }

    private LicensePolicy BuildPolicy(IEnumerable<OpenSourceLicense> allowed, IEnumerable<string> exceptions, IReadOnlyDictionary<string, string> bundledOverrides)
    {
        var policy = new LicensePolicy
        {
            UnresolvedLicenseHandling = UnresolvedLicenseHandling.Reject,
            BundledLicenseHandling = BundledLicenseHandling.Enforce,
            ExemptWorkspaceMembers = true,
        };
        policy.Allow(allowed.ToArray());
        policy.AllowException(exceptions.ToArray());
        policy.Prefer(Preferred.ToArray());
        foreach (var pair in bundledOverrides)
        {
            policy.AcceptBundledLicenses(pair.Key, pair.Value);
        }

        return policy;
    }

    private static IReadOnlyList<OpenSourceLicense> ReadLicenses(JsonElement root, string name)
    {
        var result = new List<OpenSourceLicense>();
        foreach (var id in ReadStrings(root, name))
        {
            if (!OpenSourceLicenseInfo.TryParse(id, out var license))
            {
                throw new InvalidDataException($"The license allowlist names '{id}' under '{name}', which is not a known SPDX license identifier.");
            }

            result.Add(license);
        }

        return result;
    }

    private static IReadOnlyList<NamedCrateLicense> ReadNamedCrateLicenses(JsonElement distributed)
    {
        var result = new List<NamedCrateLicense>();
        if (distributed.ValueKind != JsonValueKind.Object || !distributed.TryGetProperty("namedCrates", out var array))
        {
            return result;
        }

        if (array.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("'compiledIntoTheDistributedTools.namedCrates' in the license allowlist must be an array.");
        }

        foreach (var entry in array.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object
                || !entry.TryGetProperty("license", out var id) || id.ValueKind != JsonValueKind.String
                || !OpenSourceLicenseInfo.TryParse(id.GetString(), out var license))
            {
                throw new InvalidDataException("Every entry of 'compiledIntoTheDistributedTools.namedCrates' needs a 'license' that is a known SPDX license identifier.");
            }

            var crates = ReadStrings(entry, "crates");
            if (crates.Count == 0)
            {
                throw new InvalidDataException($"The '{id.GetString()}' entry of 'compiledIntoTheDistributedTools.namedCrates' must name its crates.");
            }

            if (!entry.TryGetProperty("reason", out var reason) || reason.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(reason.GetString()))
            {
                throw new InvalidDataException($"The '{id.GetString()}' entry of 'compiledIntoTheDistributedTools.namedCrates' must give a reason.");
            }

            result.Add(new NamedCrateLicense { License = license, Tools = ReadStrings(entry, "tools"), Crates = crates, Reason = reason.GetString() });
        }

        return result;
    }

    private static IReadOnlyList<string> ReadStrings(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element))
        {
            return Array.Empty<string>();
        }

        if (element.ValueKind != JsonValueKind.Array || element.EnumerateArray().Any(e => e.ValueKind != JsonValueKind.String))
        {
            throw new InvalidDataException($"'{name}' in the license allowlist must be an array of strings.");
        }

        return element.EnumerateArray().Select(e => e.GetString().Trim()).Where(s => s.Length > 0).ToList();
    }

    private static IReadOnlyDictionary<string, string> ReadMap(JsonElement parent, string name)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var element))
        {
            return result;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"'bundledLicenseOverrides.{name}' in the license allowlist must be an object.");
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.Value.GetString()))
            {
                throw new InvalidDataException($"The bundled-license override for '{property.Name}' must give a reason.");
            }

            result[property.Name] = property.Value.GetString();
        }

        return result;
    }
}
