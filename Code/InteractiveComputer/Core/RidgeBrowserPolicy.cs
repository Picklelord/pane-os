using System;
using System.Collections.Generic;
using System.Linq;

namespace PaneOS.InteractiveComputer.Core;

public static class RidgeBrowserPolicy
{
	private static readonly IReadOnlyList<string> DefaultCreditHosts = new[]
	{
		"github.com",
		"flaticon.com",
		"www.flaticon.com"
	};

	public static RidgePolicyResult Evaluate( string rawUrl, string? webRenderingEnabled, string? allowedHosts )
	{
		var normalizedUrl = NormalizeUrl( rawUrl );
		var hosts = MergeDefaultHosts( ParseHostList( allowedHosts ?? "" ) );
		var renderingEnabled = IsTruthy( webRenderingEnabled );

		if ( normalizedUrl.StartsWith( "paneos://", StringComparison.OrdinalIgnoreCase ) )
		{
			return new RidgePolicyResult
			{
				NormalizedUrl = normalizedUrl,
				Title = "Ridge",
				Body = "Website rendering is disabled by default. Enable web_rendering_enabled and add hosts to allowed_hosts in this app's installed settings to permit specific sites.",
				Status = "Ready",
				MessageClass = "home",
				AllowedHosts = hosts
			};
		}

		if ( !Uri.TryCreate( normalizedUrl, UriKind.Absolute, out var uri ) || string.IsNullOrWhiteSpace( uri.Host ) )
		{
			return new RidgePolicyResult
			{
				NormalizedUrl = normalizedUrl,
				Title = "This address is not valid",
				Body = "Enter a full http or https URL, or use paneos://home.",
				Status = "Invalid address",
				MessageClass = "blocked",
				AllowedHosts = hosts
			};
		}

		if ( uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps )
		{
			return new RidgePolicyResult
			{
				NormalizedUrl = normalizedUrl,
				Title = "Protocol blocked",
				Body = "Ridge only supports http and https URLs when website rendering is enabled.",
				Status = "Blocked",
				MessageClass = "blocked",
				AllowedHosts = hosts
			};
		}

		var isDefaultCreditHost = IsHostAllowed( uri.Host, DefaultCreditHosts );
		if ( !renderingEnabled && !isDefaultCreditHost )
		{
			return new RidgePolicyResult
			{
				NormalizedUrl = normalizedUrl,
				Title = "Website rendering disabled",
				Body = "This PaneOS computer is configured to avoid rendering external websites.",
				Status = "Rendering disabled",
				MessageClass = "blocked",
				AllowedHosts = hosts
			};
		}

		if ( !IsHostAllowed( uri.Host, hosts ) )
		{
			return new RidgePolicyResult
			{
				NormalizedUrl = normalizedUrl,
				Title = "Site not allowed",
				Body = $"{uri.Host} is not in this computer's Ridge allow list.",
				Status = "Blocked by allow list",
				MessageClass = "blocked",
				AllowedHosts = hosts
			};
		}

		return new RidgePolicyResult
		{
			CanRenderWebPanel = true,
			NormalizedUrl = normalizedUrl,
			Title = uri.Host,
			Status = $"Loaded {uri.Host}",
			MessageClass = "allowed",
			AllowedHosts = hosts
		};
	}

	public static string NormalizeUrl( string rawUrl )
	{
		var value = string.IsNullOrWhiteSpace( rawUrl ) ? "paneos://home" : rawUrl.Trim();
		if ( value.StartsWith( "paneos://", StringComparison.OrdinalIgnoreCase ) )
			return value;

		if ( value.Contains( "://" ) )
			return value;

		return $"https://{value}";
	}

	public static bool IsTruthy( string? value )
	{
		return value is not null && (
			value.Equals( "true", StringComparison.OrdinalIgnoreCase )
			|| value.Equals( "1", StringComparison.OrdinalIgnoreCase )
			|| value.Equals( "yes", StringComparison.OrdinalIgnoreCase )
			|| value.Equals( "on", StringComparison.OrdinalIgnoreCase ) );
	}

	public static IReadOnlyList<string> ParseHostList( string value )
	{
		return value
			.Split( new[] { ',', ';', '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries )
			.Select( x => x.ToLowerInvariant() )
			.Distinct()
			.ToArray();
	}

	public static bool IsHostAllowed( string host, IReadOnlyList<string> allowedHosts )
	{
		var normalizedHost = host.ToLowerInvariant();
		foreach ( var allowedHost in allowedHosts )
		{
			if ( normalizedHost == allowedHost )
				return true;

			if ( allowedHost.StartsWith( "*.", StringComparison.Ordinal ) && normalizedHost.EndsWith( allowedHost[1..], StringComparison.Ordinal ) )
				return true;
		}

		return false;
	}

	private static IReadOnlyList<string> MergeDefaultHosts( IReadOnlyList<string> configuredHosts )
	{
		return DefaultCreditHosts
			.Concat( configuredHosts )
			.Select( x => x.ToLowerInvariant() )
			.Distinct()
			.ToArray();
	}
}

public sealed class RidgePolicyResult
{
	public bool CanRenderWebPanel { get; init; }
	public string NormalizedUrl { get; init; } = "";
	public string Title { get; init; } = "";
	public string Body { get; init; } = "";
	public string Status { get; init; } = "";
	public string MessageClass { get; init; } = "";
	public IReadOnlyList<string> AllowedHosts { get; init; } = Array.Empty<string>();
}
