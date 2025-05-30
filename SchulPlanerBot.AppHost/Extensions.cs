﻿using Microsoft.Extensions.Configuration;
using System.Net.Sockets;

namespace SchulPlanerBot.AppHost;

internal static class Extensions
{
    /// <summary>
    /// Will pass all keys of a specific configuration section as environment parameters to the resource.
    /// </summary>
    /// <remarks>
    /// The configs will be added an Aspire parameters.
    /// </remarks>
    /// <typeparam name="TResource">The target resource type.</typeparam>
    /// <param name="builder">The builder of the receiving resource.</param>
    /// <param name="configuration">The configuration section that will be passed.</param>
    /// <param name="prefix">A prefix that will be added to the configuration name of the receiving resource,</param>
    /// <param name="secretKeys">Specifies which keys should be added as a secret parameter.</param>
    /// <returns>The builder to chain.</returns>
    public static IResourceBuilder<TResource> WithConfiguration<TResource>(
        this IResourceBuilder<TResource> builder,
        IConfiguration configuration,
        string? prefix = null,
        params string[] secretKeys)
        where TResource : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        foreach ((string path, string? value) in configuration.AsEnumerable(makePathsRelative: true))
        {
            if (value is null)
                continue;

            string fullPath = configuration is IConfigurationSection section ? $"{section.Path}:{path}" : path;
            string paramName = fullPath.Replace(":", "-");
            bool isSecret = secretKeys.Any(path.Equals);

            IResourceBuilder<ParameterResource>? keyParamBuilder = builder.ApplicationBuilder.Resources
                .OfType<IResourceBuilder<ParameterResource>>()
                .FirstOrDefault(param => param.Resource.Name == paramName);
            keyParamBuilder ??= builder.ApplicationBuilder.AddParameterFromConfiguration(paramName, fullPath, secret: isSecret);

            string variableName = $"{prefix}:{fullPath}".TrimStart(':').Replace(":", "__");
            builder = builder.WithEnvironment(variableName, keyParamBuilder);
        }

        return builder;
    }

    public static IResourceBuilder<TResource> WithExternalTcpEndpoints<TResource>(this IResourceBuilder<TResource> builder)
        where TResource : IResourceWithEndpoints
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Resource.TryGetAnnotationsOfType(out IEnumerable<EndpointAnnotation>? endpoints) )
        {
            foreach (EndpointAnnotation endpoint in endpoints.Where(p => p.Protocol == ProtocolType.Tcp))
            {
                endpoint.IsExternal = true;
            }
        }

        return builder;
    }
}
