using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Servora.Web.Api;

/// <summary>Sets title, description and the API key security schemes of the OpenAPI document.</summary>
public sealed class ServoraOpenApiDocumentTransformer : IOpenApiDocumentTransformer
{
    /// <summary>The name of the header security scheme.</summary>
    public const string ApiKeySchemeName = "ApiKey";

    /// <summary>The name of the bearer security scheme.</summary>
    public const string BearerSchemeName = "Bearer";

    /// <inheritdoc />
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "Servora REST API",
            Version = "v1",
            Description =
                "REST interface of Servora. Authenticate with an API key (Administration › API Keys) in the `X-Api-Key` header or as " +
                "`Authorization: Bearer srv_…`. Every call is limited by the key's scopes **and** the permissions of the key owner. " +
                "Errors use RFC 7807 problem details; every response carries an `X-Correlation-Id` header. List endpoints accept " +
                "`page` and `pageSize` (max 100).",
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[ApiKeySchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = ApiKeyAuthenticationDefaults.HeaderName,
            Description = "API key in the X-Api-Key header (format srv_<key id>_<secret>).",
        };
        document.Components.SecuritySchemes[BearerSchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            Description = "API key as bearer token: Authorization: Bearer srv_<key id>_<secret>.",
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(ApiKeySchemeName, document)] = [] });
        document.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(BearerSchemeName, document)] = [] });
        return Task.CompletedTask;
    }
}
