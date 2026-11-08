using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Automation.Actions;
using Servora.Application.Automation.Model;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Integration.DataSources;
using Servora.Domain.Automation;
using Servora.Domain.Common;
using Servora.Domain.Integration;

namespace Servora.Infrastructure.Automation;

/// <summary>
/// Seeds demo automation content (idempotent, by name): a public HTTPS weather data source and two inactive example
/// flows (a scheduled data source flow and a record-triggered flow with condition and e-mail).
/// </summary>
public sealed class AutomationSeeder : IDataSeeder
{
    /// <summary>The name of the demo data source.</summary>
    public const string DemoDataSourceName = "Open-Meteo Weather (demo)";

    /// <inheritdoc />
    public int Order => 300;

    /// <inheritdoc />
    public bool IsDemoData => true;

    /// <inheritdoc />
    public async Task SeedAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var source = await db.Set<DataSource>().FirstOrDefaultAsync(s => s.Name == DemoDataSourceName, cancellationToken);
        if (source is null)
        {
            source = new DataSource
            {
                Name = DemoDataSourceName,
                Description = "Public weather API without authentication. Demonstrates approved endpoints, parameter validation and response mapping.",
                BaseUrl = "https://api.open-meteo.com",
                AuthType = DataSourceAuthType.None,
                TimeoutSeconds = 15,
                MaxRetries = 1,
                MaxResponseBytes = 256 * 1024,
                RateLimitPerMinute = 30,
                IsEnabled = true,
            };
            db.Set<DataSource>().Add(source);
            db.Set<DataSourceEndpoint>().Add(new DataSourceEndpoint
            {
                DataSourceId = source.Id,
                Key = "current-weather",
                Name = "Current weather",
                Description = "Current temperature and wind for a coordinate.",
                Method = "GET",
                PathTemplate = "/v1/forecast?current_weather=true",
                ParametersJson = FlowJson.Serialize(new List<EndpointParameterDefinition>
                {
                    new() { Name = "latitude", Location = EndpointParameterLocation.Query, Type = EndpointParameterType.Number, Required = true, DefaultValue = "47.37", MaxLength = 12, Description = "Latitude (-90 to 90)." },
                    new() { Name = "longitude", Location = EndpointParameterLocation.Query, Type = EndpointParameterType.Number, Required = true, DefaultValue = "8.54", MaxLength = 12, Description = "Longitude (-180 to 180)." },
                }),
                ResponsePath = "current_weather",
                IsEnabled = true,
            });
        }

        if (!await db.Set<Flow>().AnyAsync(f => f.Name == "Daily weather digest (demo)", cancellationToken))
        {
            var steps = new List<FlowStepDefinition>
            {
                new()
                {
                    Key = "weather",
                    Type = CallDataSourceAction.TypeKey,
                    Name = "Get current weather",
                    RetryCount = 2,
                    RetryDelaySeconds = 10,
                    Config = new Dictionary<string, string?>
                    {
                        ["dataSourceId"] = source.Id.ToString(),
                        ["endpoint"] = "current-weather",
                        ["parameters"] = JsonSerializer.Serialize(new Dictionary<string, string> { ["latitude"] = "47.37", ["longitude"] = "8.54" }),
                    },
                },
                new()
                {
                    Key = "set1",
                    Type = SetVariableAction.TypeKey,
                    Name = "Remember temperature",
                    Config = new Dictionary<string, string?> { ["name"] = "temperature", ["value"] = "{{steps.weather.json.temperature}}" },
                },
                new()
                {
                    Key = "log1",
                    Type = LogAction.TypeKey,
                    Name = "Log digest",
                    Config = new Dictionary<string, string?> { ["level"] = "info", ["message"] = "Current temperature: {{vars.temperature}} °C, wind {{steps.weather.json.windspeed}} km/h." },
                },
            };
            db.Set<Flow>().Add(new Flow
            {
                Name = "Daily weather digest (demo)",
                Description = "Scheduled flow that calls the demo weather data source every weekday morning and logs the result.",
                IsActive = false,
                TriggerType = FlowTriggerType.Schedule,
                CronExpression = "0 7 * * 1-5",
                StepsJson = FlowJson.Serialize(steps),
                DefinitionVersion = 1,
                VersionNote = "Seeded example",
            });
        }

        if (!await db.Set<Flow>().AnyAsync(f => f.Name == "Welcome new users (demo)", cancellationToken))
        {
            var condition = new FlowCondition { Rules = [new FlowConditionRule { Left = "{{trigger.record.Email}}", Operator = FlowConditionOperator.IsNotEmpty }] };
            var ifStep = new FlowStepDefinition
            {
                Key = "if1",
                Type = ConditionAction.TypeKey,
                Name = "Has an e-mail address?",
                Config = new Dictionary<string, string?> { ["condition"] = FlowJson.Serialize(condition) },
                Branches =
                [
                    new FlowBranchDefinition
                    {
                        Key = ConditionAction.ThenBranch,
                        Label = "If true",
                        Steps =
                        [
                            new FlowStepDefinition
                            {
                                Key = "email1",
                                Type = SendEmailAction.TypeKey,
                                Name = "Send welcome e-mail",
                                Config = new Dictionary<string, string?>
                                {
                                    ["to"] = "{{trigger.record.Email}}",
                                    ["subject"] = "Welcome to Servora, {{trigger.record.DisplayName}}",
                                    ["body"] = "Hello {{trigger.record.FirstName | default:'there'}},\n\nyour Servora account '{{trigger.record.UserName}}' is ready.\n\nThe Service Desk",
                                },
                            },
                        ],
                    },
                    new FlowBranchDefinition
                    {
                        Key = ConditionAction.ElseBranch,
                        Label = "Otherwise",
                        Steps =
                        [
                            new FlowStepDefinition
                            {
                                Key = "log1",
                                Type = LogAction.TypeKey,
                                Name = "Log missing address",
                                Config = new Dictionary<string, string?> { ["level"] = "warning", ["message"] = "User {{trigger.record.UserName}} has no e-mail address." },
                            },
                        ],
                    },
                ],
            };
            db.Set<Flow>().Add(new Flow
            {
                Name = "Welcome new users (demo)",
                Description = "Record-triggered flow: when a user is created, send a welcome e-mail if an address is known.",
                IsActive = false,
                TriggerType = FlowTriggerType.RecordCreated,
                TriggerTable = TableNames.User,
                TriggerConditionJson = ConditionSerializer.Serialize(new ConditionGroup()),
                StepsJson = FlowJson.Serialize(new List<FlowStepDefinition> { ifStep }),
                DefinitionVersion = 1,
                VersionNote = "Seeded example",
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
