using System;
using System.IO;
using CodeBrix.WebPlatform.BackendMapper.Model;

namespace CodeBrix.WebPlatform.BackendMapper.Tests.Helpers;

/// <summary>The two fixture backends, which are built beside the test assembly.</summary>
public static class Backends
{
    private static readonly Lazy<BackendMap> SampleMap = new(() => BackendMapper.Map(new BackendMapOptions { AssemblyPath = SamplePath }));

    public static string SamplePath => Path.Combine(AppContext.BaseDirectory, "CodeBrix.WebPlatform.BackendMapper.Tests.SampleBackend.dll");

    public static string BrokenPath => Path.Combine(AppContext.BaseDirectory, "CodeBrix.WebPlatform.BackendMapper.Tests.BrokenBackend.dll");

    /// <summary>The sample backend, mapped once with the default options.</summary>
    public static BackendMap Sample => SampleMap.Value;
}
