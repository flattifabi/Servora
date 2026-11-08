using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servora.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_agent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_agent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_conversation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LastMessageAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MessageCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_conversation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_message",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: false),
                    AgentKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsError = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    InputTokens = table.Column<int>(type: "int", nullable: true),
                    OutputTokens = table.Column<int>(type: "int", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_message", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_provider",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Deployment = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApiVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MaxTokens = table.Column<int>(type: "int", nullable: false),
                    Temperature = table.Column<double>(type: "float", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    SupportsTools = table.Column<bool>(type: "bit", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    LastTestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastTestSucceeded = table.Column<bool>(type: "bit", nullable: true),
                    LastTestMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_provider", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_tool_call",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AgentKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ToolName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ArgumentsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_tool_call", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "automation_flow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TriggerType = table.Column<int>(type: "int", nullable: false),
                    TriggerTable = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TriggerConditionJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    CronExpression = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NextRunAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastTriggeredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PollDataSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PollEndpointKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PollParametersJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    PollOnlyOnChange = table.Column<bool>(type: "bit", nullable: false),
                    LastPollHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    InputsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    StepsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 500000, nullable: false),
                    DefinitionVersion = table.Column<int>(type: "int", nullable: false),
                    VersionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_flow", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "automation_flow_run",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlowName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FlowVersion = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TriggerType = table.Column<int>(type: "int", nullable: false),
                    TriggerSummary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TriggerTable = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TriggerRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ParentRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Depth = table.Column<int>(type: "int", nullable: false),
                    EventDepth = table.Column<int>(type: "int", nullable: false),
                    StepsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 500000, nullable: false),
                    IsTest = table.Column<bool>(type: "bit", nullable: false),
                    StartedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ContextJson = table.Column<string>(type: "nvarchar(max)", maxLength: 300000, nullable: false),
                    QueuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResumeAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    WaitingStepId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    WaitReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancelRequested = table.Column<bool>(type: "bit", nullable: false),
                    StepCount = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_flow_run", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "automation_flow_run_step",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StepKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StepName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Attempt = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    InputJson = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: true),
                    OutputJson = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_flow_run_step", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "change_request",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Justification = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    ImplementationPlan = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    BackoutPlan = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    TestPlan = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    Risk = table.Column<int>(type: "int", nullable: false),
                    RiskScore = table.Column<int>(type: "int", nullable: false),
                    RiskAssessmentJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RiskAssessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PlannedStartAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PlannedEndAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ActualStartAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ActualEndAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovalGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Approval = table.Column<int>(type: "int", nullable: false),
                    CloseCode = table.Column<int>(type: "int", nullable: true),
                    CloseNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ShortDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true),
                    Impact = table.Column<int>(type: "int", nullable: false),
                    Urgency = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    AssignmentGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedToId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CustomFieldsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_change_request", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cmdb_ci",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ClassName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OperationalStatus = table.Column<int>(type: "int", nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OwnedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupportGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Fqdn = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Criticality = table.Column<int>(type: "int", nullable: false),
                    DiscoverySource = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LastDiscoveredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CustomFieldsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cmdb_ci", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cmdb_class",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ParentKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AttributesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cmdb_class", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "contract_sla",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TableName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TargetMinutes = table.Column<int>(type: "int", nullable: false),
                    StartConditionJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    PauseConditionJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    StopConditionJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    CancelConditionJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    ScheduleType = table.Column<int>(type: "int", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BusinessDays = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DayStartMinute = table.Column<int>(type: "int", nullable: false),
                    DayEndMinute = table.Column<int>(type: "int", nullable: false),
                    BreachNotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_sla", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "incident",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    CallerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Subcategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    BusinessServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsMajorIncident = table.Column<bool>(type: "bit", nullable: false),
                    ParentIncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProblemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OnHoldReason = table.Column<int>(type: "int", nullable: true),
                    ResolutionCode = table.Column<int>(type: "int", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    ReopenCount = table.Column<int>(type: "int", nullable: false),
                    ReassignmentCount = table.Column<int>(type: "int", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResolvedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ShortDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true),
                    Impact = table.Column<int>(type: "int", nullable: false),
                    Urgency = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    AssignmentGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedToId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CustomFieldsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incident", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "incident_category",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incident_category", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "int_data_source",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    BaseUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AuthType = table.Column<int>(type: "int", nullable: false),
                    ApiKeyHeaderName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BasicUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OAuthTokenUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OAuthClientId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OAuthScope = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DefaultHeadersJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    TimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    MaxRetries = table.Column<int>(type: "int", nullable: false),
                    MaxResponseBytes = table.Column<int>(type: "int", nullable: false),
                    RateLimitPerMinute = table.Column<int>(type: "int", nullable: false),
                    AllowHttp = table.Column<bool>(type: "bit", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_int_data_source", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "int_data_source_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataSourceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EndpointKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Host = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    ResponseBytes = table.Column<long>(type: "bigint", nullable: false),
                    Truncated = table.Column<bool>(type: "bit", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsTest = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_int_data_source_log", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "int_import_set",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    DataSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EndpointKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ParametersJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    RecordsPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TargetTable = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CoalesceField = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FieldMapsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: false),
                    CronExpression = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ScheduleEnabled = table.Column<bool>(type: "bit", nullable: false),
                    NextRunAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastRunAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_int_import_set", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "int_network_allow_list",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cidr = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_int_network_allow_list", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "kb_knowledge_base",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OwnerGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kb_knowledge_base", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "portal_announcement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    LinkUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LinkText = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portal_announcement", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "problem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BusinessServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsKnownError = table.Column<bool>(type: "bit", nullable: false),
                    Workaround = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    RootCause = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    FixNotes = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    ResolutionCode = table.Column<int>(type: "int", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResolvedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ShortDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true),
                    Impact = table.Column<int>(type: "int", nullable: false),
                    Urgency = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    AssignmentGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedToId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CustomFieldsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_problem", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sc_category",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sc_category", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sc_request",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedForId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ShortDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true),
                    Impact = table.Column<int>(type: "int", nullable: false),
                    Urgency = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    AssignmentGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedToId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CustomFieldsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sc_request", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_api_key",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    KeyId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SecretHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ServiceIdentity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Scopes = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AllowedIpRanges = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RateLimitPerMinute = table.Column<int>(type: "int", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RevocationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RotatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastUsedIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UsageCount = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_api_key", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_api_key_usage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApiKeyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Day = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RequestCount = table.Column<long>(type: "bigint", nullable: false),
                    FailedCount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_api_key_usage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_approval",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    RecordSummary = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RecordRoute = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecidedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_approval", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_attachment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UploadedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_attachment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_audit_event",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ActorType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TargetId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TargetName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PreviousHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_audit_event", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_custom_record",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    DisplayValue = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DataJson = table.Column<string>(type: "nvarchar(max)", maxLength: 64000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_custom_record", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_dashboard_pin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Wide = table.Column<bool>(type: "bit", nullable: false),
                    PinnedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_dashboard_pin", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_db_object",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PluralLabel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DisplayField = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    NumberPrefix = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_db_object", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_dictionary",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ChoicesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    ReferenceTable = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    DefaultValue = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    HelpText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MaxLength = table.Column<int>(type: "int", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_dictionary", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_directory_group_map",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DirectoryGroup = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_directory_group_map", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_directory_sync_run",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Trigger = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UsersRead = table.Column<int>(type: "int", nullable: false),
                    UsersCreated = table.Column<int>(type: "int", nullable: false),
                    UsersUpdated = table.Column<int>(type: "int", nullable: false),
                    UsersDeactivated = table.Column<int>(type: "int", nullable: false),
                    MembershipChanges = table.Column<int>(type: "int", nullable: false),
                    Errors = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_directory_sync_run", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_email_outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Recipients = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CcRecipients = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(998)", maxLength: 998, nullable: false),
                    HtmlBody = table.Column<string>(type: "nvarchar(max)", maxLength: 1000000, nullable: false),
                    TextBody = table.Column<string>(type: "nvarchar(max)", maxLength: 1000000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_email_outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_journal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: true),
                    ChangesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AuthorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_journal", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_mcp_invocation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Target = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ApiKeyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ClientName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_mcp_invocation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_mcp_tool",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToolName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_mcp_tool", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_notification_inbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Link = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TableName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_notification_inbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_number",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    NextValue = table.Column<long>(type: "bigint", nullable: false),
                    Digits = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_number", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_properties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ValueType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequiresRestart = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_properties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_record_field_value",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TextValue = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    NumberValue = table.Column<double>(type: "float", nullable: true),
                    DateValue = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_record_field_value", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_report",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TableName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConditionJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    GroupBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DateInterval = table.Column<int>(type: "int", nullable: false),
                    Aggregate = table.Column<int>(type: "int", nullable: false),
                    AggregateField = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ChartType = table.Column<int>(type: "int", nullable: false),
                    MaxGroups = table.Column<int>(type: "int", nullable: false),
                    Visibility = table.Column<int>(type: "int", nullable: false),
                    SharedWithRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_report", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_role",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_role", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_script",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TableName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Timing = table.Column<int>(type: "int", nullable: false),
                    OnInsert = table.Column<bool>(type: "bit", nullable: false),
                    OnUpdate = table.Column<bool>(type: "bit", nullable: false),
                    OnDelete = table.Column<bool>(type: "bit", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    ConditionJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    ActionsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_script", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_secret",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ProtectedValue = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    RotatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_secret", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_ui_layout",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FormJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    ListColumnsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_ui_layout", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_user",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Department = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Source = table.Column<int>(type: "int", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DistinguishedName = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    FailedSignInCount = table.Column<int>(type: "int", nullable: false),
                    LockoutEndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSignInAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Theme = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    CustomFieldsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_user", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_user_group",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Source = table.Column<int>(type: "int", nullable: false),
                    DistinguishedName = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ExternalId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CustomFieldsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_user_group", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sysevent_email_action",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TableName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    OnInsert = table.Column<bool>(type: "bit", nullable: false),
                    OnUpdate = table.Column<bool>(type: "bit", nullable: false),
                    OnDelete = table.Column<bool>(type: "bit", nullable: false),
                    ConditionJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: true),
                    RecipientFieldsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RecipientGroupIdsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RecipientUserIdsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RecipientEmails = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SendEmail = table.Column<bool>(type: "bit", nullable: false),
                    SendInApp = table.Column<bool>(type: "bit", nullable: false),
                    ExcludeActor = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sysevent_email_action", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sysevent_email_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sysevent_email_template", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "task_sla",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SlaDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordDisplay = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActiveSince = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ElapsedSeconds = table.Column<long>(type: "bigint", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PausedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StoppedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    HasBreached = table.Column<bool>(type: "bit", nullable: false),
                    BreachedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_sla", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "change_task",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    PlannedStartAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PlannedEndAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CloseNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ShortDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true),
                    Impact = table.Column<int>(type: "int", nullable: false),
                    Urgency = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    AssignmentGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedToId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CustomFieldsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_change_task", x => x.Id);
                    table.ForeignKey(
                        name: "FK_change_task_change_request_ChangeRequestId",
                        column: x => x.ChangeRequestId,
                        principalTable: "change_request",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "alm_asset",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Vendor = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AssetTag = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    AssignedToId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    WarrantyEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Entitlements = table.Column<int>(type: "int", nullable: true),
                    Installs = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RetiredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CustomFieldsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alm_asset", x => x.Id);
                    table.ForeignKey(
                        name: "FK_alm_asset_cmdb_ci_ConfigurationItemId",
                        column: x => x.ConfigurationItemId,
                        principalTable: "cmdb_ci",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "cmdb_rel_ci",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChildId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cmdb_rel_ci", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cmdb_rel_ci_cmdb_ci_ChildId",
                        column: x => x.ChildId,
                        principalTable: "cmdb_ci",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cmdb_rel_ci_cmdb_ci_ParentId",
                        column: x => x.ParentId,
                        principalTable: "cmdb_ci",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "int_data_source_endpoint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Method = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PathTemplate = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ParametersJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    BodyTemplate = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    ResponsePath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_int_data_source_endpoint", x => x.Id);
                    table.ForeignKey(
                        name: "FK_int_data_source_endpoint_int_data_source_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "int_data_source",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "int_import_run",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SourceDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    Inserted = table.Column<int>(type: "int", nullable: false),
                    Updated = table.Column<int>(type: "int", nullable: false),
                    Skipped = table.Column<int>(type: "int", nullable: false),
                    Errors = table.Column<int>(type: "int", nullable: false),
                    StartedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_int_import_run", x => x.Id);
                    table.ForeignKey(
                        name: "FK_int_import_run_int_import_set_ImportSetId",
                        column: x => x.ImportSetId,
                        principalTable: "int_import_set",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "kb_category",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KnowledgeBaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kb_category", x => x.Id);
                    table.ForeignKey(
                        name: "FK_kb_category_kb_knowledge_base_KnowledgeBaseId",
                        column: x => x.KnowledgeBaseId,
                        principalTable: "kb_knowledge_base",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sc_cat_item",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ShortDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeliveryDays = table.Column<int>(type: "int", nullable: false),
                    FulfillmentGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FulfillmentTasks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ApprovalRequired = table.Column<bool>(type: "bit", nullable: false),
                    ApprovalGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ManagerApproval = table.Column<bool>(type: "bit", nullable: false),
                    ApprovalMode = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sc_cat_item", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sc_cat_item_sc_category_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "sc_category",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "sys_role_permission",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Permission = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_role_permission", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sys_role_permission_sys_role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "sys_role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sys_user_has_role",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsFromDirectory = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_user_has_role", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sys_user_has_role_sys_role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "sys_role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sys_user_has_role_sys_user_UserId",
                        column: x => x.UserId,
                        principalTable: "sys_user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sys_group_has_role",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_group_has_role", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sys_group_has_role_sys_role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "sys_role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sys_group_has_role_sys_user_group_GroupId",
                        column: x => x.GroupId,
                        principalTable: "sys_user_group",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sys_user_grmember",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsFromDirectory = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_user_grmember", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sys_user_grmember_sys_user_UserId",
                        column: x => x.UserId,
                        principalTable: "sys_user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sys_user_grmember_sys_user_group_GroupId",
                        column: x => x.GroupId,
                        principalTable: "sys_user_group",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "int_import_row",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DataJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    TargetRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_int_import_row", x => x.Id);
                    table.ForeignKey(
                        name: "FK_int_import_row_int_import_run_RunId",
                        column: x => x.RunId,
                        principalTable: "int_import_run",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "kb_knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 100000, nullable: false),
                    KnowledgeBaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AuthorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RetiredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    ArticleVersion = table.Column<int>(type: "int", nullable: false),
                    ViewCount = table.Column<int>(type: "int", nullable: false),
                    HelpfulCount = table.Column<int>(type: "int", nullable: false),
                    NotHelpfulCount = table.Column<int>(type: "int", nullable: false),
                    Keywords = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CustomFieldsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kb_knowledge", x => x.Id);
                    table.ForeignKey(
                        name: "FK_kb_knowledge_kb_category_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "kb_category",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_kb_knowledge_kb_knowledge_base_KnowledgeBaseId",
                        column: x => x.KnowledgeBaseId,
                        principalTable: "kb_knowledge_base",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item_option_new",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Mandatory = table.Column<bool>(type: "bit", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    HelpText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Options = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    DefaultValue = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MaxLength = table.Column<int>(type: "int", nullable: true),
                    MinValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    MaxValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_option_new", x => x.Id);
                    table.ForeignKey(
                        name: "FK_item_option_new_sc_cat_item_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "sc_cat_item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sc_req_item",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedForId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VariablesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    Approval = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ShortDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true),
                    Impact = table.Column<int>(type: "int", nullable: false),
                    Urgency = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    AssignmentGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedToId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CustomFieldsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sc_req_item", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sc_req_item_sc_cat_item_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "sc_cat_item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sc_req_item_sc_request_RequestId",
                        column: x => x.RequestId,
                        principalTable: "sc_request",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "kb_feedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Helpful = table.Column<bool>(type: "bit", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kb_feedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_kb_feedback_kb_knowledge_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "kb_knowledge",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "kb_m2m_ci",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kb_m2m_ci", x => x.Id);
                    table.ForeignKey(
                        name: "FK_kb_m2m_ci_cmdb_ci_ConfigurationItemId",
                        column: x => x.ConfigurationItemId,
                        principalTable: "cmdb_ci",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_kb_m2m_ci_kb_knowledge_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "kb_knowledge",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "kb_version",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 100000, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    ChangeNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kb_version", x => x.Id);
                    table.ForeignKey(
                        name: "FK_kb_version_kb_knowledge_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "kb_knowledge",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sc_task",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    CloseNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ShortDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true),
                    Impact = table.Column<int>(type: "int", nullable: false),
                    Urgency = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    AssignmentGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedToId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CustomFieldsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sc_task", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sc_task_sc_req_item_RequestItemId",
                        column: x => x.RequestItemId,
                        principalTable: "sc_req_item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_AgentKey",
                table: "ai_agent",
                column: "AgentKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversation_UserId_LastMessageAt",
                table: "ai_conversation",
                columns: new[] { "UserId", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_message_ConversationId_CreatedAt",
                table: "ai_message",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_provider_Name",
                table: "ai_provider",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_tool_call_ConversationId_Status",
                table: "ai_tool_call",
                columns: new[] { "ConversationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_tool_call_MessageId",
                table: "ai_tool_call",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_alm_asset_AssetTag",
                table: "alm_asset",
                column: "AssetTag");

            migrationBuilder.CreateIndex(
                name: "IX_alm_asset_AssignedToId",
                table: "alm_asset",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_alm_asset_ConfigurationItemId",
                table: "alm_asset",
                column: "ConfigurationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_alm_asset_DisplayName",
                table: "alm_asset",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_alm_asset_Number",
                table: "alm_asset",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_alm_asset_SerialNumber",
                table: "alm_asset",
                column: "SerialNumber");

            migrationBuilder.CreateIndex(
                name: "IX_alm_asset_State_Category",
                table: "alm_asset",
                columns: new[] { "State", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_alm_asset_WarrantyEnd",
                table: "alm_asset",
                column: "WarrantyEnd");

            migrationBuilder.CreateIndex(
                name: "IX_automation_flow_IsActive_NextRunAt",
                table: "automation_flow",
                columns: new[] { "IsActive", "NextRunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_automation_flow_IsActive_TriggerType_TriggerTable",
                table: "automation_flow",
                columns: new[] { "IsActive", "TriggerType", "TriggerTable" });

            migrationBuilder.CreateIndex(
                name: "IX_automation_flow_Name",
                table: "automation_flow",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_automation_flow_run_FlowId_IdempotencyKey",
                table: "automation_flow_run",
                columns: new[] { "FlowId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_automation_flow_run_ParentRunId",
                table: "automation_flow_run",
                column: "ParentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_automation_flow_run_QueuedAt",
                table: "automation_flow_run",
                column: "QueuedAt");

            migrationBuilder.CreateIndex(
                name: "IX_automation_flow_run_Status_ResumeAt",
                table: "automation_flow_run",
                columns: new[] { "Status", "ResumeAt" });

            migrationBuilder.CreateIndex(
                name: "IX_automation_flow_run_step_RunId_Sequence",
                table: "automation_flow_run_step",
                columns: new[] { "RunId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_change_request_Active_Priority",
                table: "change_request",
                columns: new[] { "Active", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_change_request_AssignedToId",
                table: "change_request",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_change_request_AssignmentGroupId",
                table: "change_request",
                column: "AssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_change_request_ConfigurationItemId",
                table: "change_request",
                column: "ConfigurationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_change_request_Number",
                table: "change_request",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_change_request_OpenedById",
                table: "change_request",
                column: "OpenedById");

            migrationBuilder.CreateIndex(
                name: "IX_change_request_PlannedStartAt",
                table: "change_request",
                column: "PlannedStartAt");

            migrationBuilder.CreateIndex(
                name: "IX_change_request_RequestedById",
                table: "change_request",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_change_request_State",
                table: "change_request",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_change_task_Active_Priority",
                table: "change_task",
                columns: new[] { "Active", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_change_task_AssignedToId",
                table: "change_task",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_change_task_AssignmentGroupId",
                table: "change_task",
                column: "AssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_change_task_ChangeRequestId",
                table: "change_task",
                column: "ChangeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_change_task_ConfigurationItemId",
                table: "change_task",
                column: "ConfigurationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_change_task_Number",
                table: "change_task",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_change_task_OpenedById",
                table: "change_task",
                column: "OpenedById");

            migrationBuilder.CreateIndex(
                name: "IX_cmdb_ci_ClassName",
                table: "cmdb_ci",
                column: "ClassName");

            migrationBuilder.CreateIndex(
                name: "IX_cmdb_ci_Name",
                table: "cmdb_ci",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_cmdb_class_Key",
                table: "cmdb_class",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cmdb_rel_ci_ChildId",
                table: "cmdb_rel_ci",
                column: "ChildId");

            migrationBuilder.CreateIndex(
                name: "IX_cmdb_rel_ci_ParentId_ChildId_Type",
                table: "cmdb_rel_ci",
                columns: new[] { "ParentId", "ChildId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contract_sla_TableName_IsActive",
                table: "contract_sla",
                columns: new[] { "TableName", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_incident_Active_Priority",
                table: "incident",
                columns: new[] { "Active", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_incident_Active_State",
                table: "incident",
                columns: new[] { "Active", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_incident_AssignedToId",
                table: "incident",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_incident_AssignmentGroupId",
                table: "incident",
                column: "AssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_incident_CallerId",
                table: "incident",
                column: "CallerId");

            migrationBuilder.CreateIndex(
                name: "IX_incident_ConfigurationItemId",
                table: "incident",
                column: "ConfigurationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_incident_Number",
                table: "incident",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_incident_OpenedAt",
                table: "incident",
                column: "OpenedAt");

            migrationBuilder.CreateIndex(
                name: "IX_incident_OpenedById",
                table: "incident",
                column: "OpenedById");

            migrationBuilder.CreateIndex(
                name: "IX_incident_ParentIncidentId",
                table: "incident",
                column: "ParentIncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_incident_ProblemId",
                table: "incident",
                column: "ProblemId");

            migrationBuilder.CreateIndex(
                name: "IX_incident_ResolvedAt",
                table: "incident",
                column: "ResolvedAt");

            migrationBuilder.CreateIndex(
                name: "IX_incident_UpdatedAt",
                table: "incident",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_incident_category_ParentId_Name",
                table: "incident_category",
                columns: new[] { "ParentId", "Name" },
                unique: true,
                filter: "[ParentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_int_data_source_Name",
                table: "int_data_source",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_int_data_source_endpoint_DataSourceId_Key",
                table: "int_data_source_endpoint",
                columns: new[] { "DataSourceId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_int_data_source_log_DataSourceId_StartedAt",
                table: "int_data_source_log",
                columns: new[] { "DataSourceId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_int_data_source_log_StartedAt",
                table: "int_data_source_log",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_int_import_row_RunId_RowNumber",
                table: "int_import_row",
                columns: new[] { "RunId", "RowNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_int_import_run_ImportSetId_StartedAt",
                table: "int_import_run",
                columns: new[] { "ImportSetId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_int_import_set_Name",
                table: "int_import_set",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_int_import_set_ScheduleEnabled_NextRunAt",
                table: "int_import_set",
                columns: new[] { "ScheduleEnabled", "NextRunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_int_network_allow_list_Cidr",
                table: "int_network_allow_list",
                column: "Cidr",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_option_new_CatalogItemId_Name",
                table: "item_option_new",
                columns: new[] { "CatalogItemId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_kb_category_KnowledgeBaseId_Name",
                table: "kb_category",
                columns: new[] { "KnowledgeBaseId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_kb_feedback_ArticleId_UserId",
                table: "kb_feedback",
                columns: new[] { "ArticleId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_kb_knowledge_AuthorId",
                table: "kb_knowledge",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_kb_knowledge_CategoryId",
                table: "kb_knowledge",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_kb_knowledge_KnowledgeBaseId",
                table: "kb_knowledge",
                column: "KnowledgeBaseId");

            migrationBuilder.CreateIndex(
                name: "IX_kb_knowledge_Number",
                table: "kb_knowledge",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_kb_knowledge_State_KnowledgeBaseId",
                table: "kb_knowledge",
                columns: new[] { "State", "KnowledgeBaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_kb_knowledge_Title",
                table: "kb_knowledge",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_kb_knowledge_base_Name",
                table: "kb_knowledge_base",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_kb_m2m_ci_ArticleId_ConfigurationItemId",
                table: "kb_m2m_ci",
                columns: new[] { "ArticleId", "ConfigurationItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_kb_m2m_ci_ConfigurationItemId",
                table: "kb_m2m_ci",
                column: "ConfigurationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_kb_version_ArticleId_VersionNumber",
                table: "kb_version",
                columns: new[] { "ArticleId", "VersionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_portal_announcement_IsActive_StartsAt",
                table: "portal_announcement",
                columns: new[] { "IsActive", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_problem_Active_Priority",
                table: "problem",
                columns: new[] { "Active", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_problem_Active_State",
                table: "problem",
                columns: new[] { "Active", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_problem_AssignedToId",
                table: "problem",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_problem_AssignmentGroupId",
                table: "problem",
                column: "AssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_problem_ConfigurationItemId",
                table: "problem",
                column: "ConfigurationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_problem_IsKnownError",
                table: "problem",
                column: "IsKnownError");

            migrationBuilder.CreateIndex(
                name: "IX_problem_Number",
                table: "problem",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_problem_OpenedById",
                table: "problem",
                column: "OpenedById");

            migrationBuilder.CreateIndex(
                name: "IX_problem_UpdatedAt",
                table: "problem",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_sc_cat_item_CategoryId",
                table: "sc_cat_item",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_sc_cat_item_IsActive",
                table: "sc_cat_item",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_sc_cat_item_Name",
                table: "sc_cat_item",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_sc_category_Name",
                table: "sc_category",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sc_req_item_Active_Priority",
                table: "sc_req_item",
                columns: new[] { "Active", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_sc_req_item_AssignedToId",
                table: "sc_req_item",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_sc_req_item_AssignmentGroupId",
                table: "sc_req_item",
                column: "AssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_sc_req_item_CatalogItemId",
                table: "sc_req_item",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_sc_req_item_ConfigurationItemId",
                table: "sc_req_item",
                column: "ConfigurationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_sc_req_item_Number",
                table: "sc_req_item",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sc_req_item_OpenedById",
                table: "sc_req_item",
                column: "OpenedById");

            migrationBuilder.CreateIndex(
                name: "IX_sc_req_item_RequestedForId",
                table: "sc_req_item",
                column: "RequestedForId");

            migrationBuilder.CreateIndex(
                name: "IX_sc_req_item_RequestId",
                table: "sc_req_item",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_sc_req_item_State",
                table: "sc_req_item",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_sc_request_Active_Priority",
                table: "sc_request",
                columns: new[] { "Active", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_sc_request_AssignedToId",
                table: "sc_request",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_sc_request_AssignmentGroupId",
                table: "sc_request",
                column: "AssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_sc_request_ConfigurationItemId",
                table: "sc_request",
                column: "ConfigurationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_sc_request_Number",
                table: "sc_request",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sc_request_OpenedById",
                table: "sc_request",
                column: "OpenedById");

            migrationBuilder.CreateIndex(
                name: "IX_sc_request_RequestedForId",
                table: "sc_request",
                column: "RequestedForId");

            migrationBuilder.CreateIndex(
                name: "IX_sc_request_State",
                table: "sc_request",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_sc_task_Active_Priority",
                table: "sc_task",
                columns: new[] { "Active", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_sc_task_AssignedToId",
                table: "sc_task",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_sc_task_AssignmentGroupId",
                table: "sc_task",
                column: "AssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_sc_task_ConfigurationItemId",
                table: "sc_task",
                column: "ConfigurationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_sc_task_Number",
                table: "sc_task",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sc_task_OpenedById",
                table: "sc_task",
                column: "OpenedById");

            migrationBuilder.CreateIndex(
                name: "IX_sc_task_RequestId",
                table: "sc_task",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_sc_task_RequestItemId",
                table: "sc_task",
                column: "RequestItemId");

            migrationBuilder.CreateIndex(
                name: "IX_sc_task_State",
                table: "sc_task",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_sys_api_key_KeyId",
                table: "sys_api_key",
                column: "KeyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sys_api_key_Name",
                table: "sys_api_key",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_sys_api_key_OwnerUserId",
                table: "sys_api_key",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_sys_api_key_usage_ApiKeyId_Day",
                table: "sys_api_key_usage",
                columns: new[] { "ApiKeyId", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sys_api_key_usage_Day",
                table: "sys_api_key_usage",
                column: "Day");

            migrationBuilder.CreateIndex(
                name: "IX_sys_approval_ApproverId_State",
                table: "sys_approval",
                columns: new[] { "ApproverId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_sys_approval_BatchId",
                table: "sys_approval",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_sys_approval_TableName_RecordId",
                table: "sys_approval",
                columns: new[] { "TableName", "RecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_sys_attachment_TableName_RecordId",
                table: "sys_attachment",
                columns: new[] { "TableName", "RecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_sys_audit_event_ActorName",
                table: "sys_audit_event",
                column: "ActorName");

            migrationBuilder.CreateIndex(
                name: "IX_sys_audit_event_Category_Action",
                table: "sys_audit_event",
                columns: new[] { "Category", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_sys_audit_event_OccurredAt",
                table: "sys_audit_event",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_sys_audit_event_Sequence",
                table: "sys_audit_event",
                column: "Sequence");

            migrationBuilder.CreateIndex(
                name: "IX_sys_custom_record_Number",
                table: "sys_custom_record",
                column: "Number");

            migrationBuilder.CreateIndex(
                name: "IX_sys_custom_record_TableName_CreatedAt",
                table: "sys_custom_record",
                columns: new[] { "TableName", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sys_custom_record_TableName_DisplayValue",
                table: "sys_custom_record",
                columns: new[] { "TableName", "DisplayValue" });

            migrationBuilder.CreateIndex(
                name: "IX_sys_dashboard_pin_ReportId",
                table: "sys_dashboard_pin",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_sys_dashboard_pin_UserId_ReportId",
                table: "sys_dashboard_pin",
                columns: new[] { "UserId", "ReportId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sys_db_object_Name",
                table: "sys_db_object",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sys_dictionary_TableName_Name",
                table: "sys_dictionary",
                columns: new[] { "TableName", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sys_directory_group_map_GroupId",
                table: "sys_directory_group_map",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_sys_directory_group_map_RoleId",
                table: "sys_directory_group_map",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_sys_directory_sync_run_StartedAt",
                table: "sys_directory_sync_run",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_sys_email_outbox_CreatedAt",
                table: "sys_email_outbox",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_sys_email_outbox_Status_NextAttemptAt",
                table: "sys_email_outbox",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sys_group_has_role_GroupId_RoleId",
                table: "sys_group_has_role",
                columns: new[] { "GroupId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sys_group_has_role_RoleId",
                table: "sys_group_has_role",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_sys_journal_TableName_RecordId",
                table: "sys_journal",
                columns: new[] { "TableName", "RecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_sys_mcp_invocation_ApiKeyId",
                table: "sys_mcp_invocation",
                column: "ApiKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_sys_mcp_invocation_OccurredAt",
                table: "sys_mcp_invocation",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_sys_mcp_tool_ToolName",
                table: "sys_mcp_tool",
                column: "ToolName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sys_notification_inbox_UserId_CreatedAt",
                table: "sys_notification_inbox",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sys_notification_inbox_UserId_ReadAt",
                table: "sys_notification_inbox",
                columns: new[] { "UserId", "ReadAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sys_number_TableName",
                table: "sys_number",
                column: "TableName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sys_properties_Key",
                table: "sys_properties",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sys_record_field_value_TableName_Field_DateValue",
                table: "sys_record_field_value",
                columns: new[] { "TableName", "Field", "DateValue" });

            migrationBuilder.CreateIndex(
                name: "IX_sys_record_field_value_TableName_Field_NumberValue",
                table: "sys_record_field_value",
                columns: new[] { "TableName", "Field", "NumberValue" });

            migrationBuilder.CreateIndex(
                name: "IX_sys_record_field_value_TableName_Field_TextValue",
                table: "sys_record_field_value",
                columns: new[] { "TableName", "Field", "TextValue" });

            migrationBuilder.CreateIndex(
                name: "IX_sys_record_field_value_TableName_RecordId_Field",
                table: "sys_record_field_value",
                columns: new[] { "TableName", "RecordId", "Field" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sys_report_Name",
                table: "sys_report",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_sys_report_OwnerId",
                table: "sys_report",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_sys_report_Visibility",
                table: "sys_report",
                column: "Visibility");

            migrationBuilder.CreateIndex(
                name: "IX_sys_role_Name",
                table: "sys_role",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sys_role_permission_RoleId_Permission",
                table: "sys_role_permission",
                columns: new[] { "RoleId", "Permission" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sys_script_TableName_IsActive",
                table: "sys_script",
                columns: new[] { "TableName", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_sys_secret_Name",
                table: "sys_secret",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sys_ui_layout_TableName",
                table: "sys_ui_layout",
                column: "TableName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sys_user_Email",
                table: "sys_user",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_sys_user_ExternalId",
                table: "sys_user",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_sys_user_NormalizedUserName",
                table: "sys_user",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sys_user_grmember_GroupId_UserId",
                table: "sys_user_grmember",
                columns: new[] { "GroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sys_user_grmember_UserId",
                table: "sys_user_grmember",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_sys_user_group_ExternalId",
                table: "sys_user_group",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_sys_user_group_Name",
                table: "sys_user_group",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sys_user_has_role_RoleId",
                table: "sys_user_has_role",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_sys_user_has_role_UserId_RoleId",
                table: "sys_user_has_role",
                columns: new[] { "UserId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sysevent_email_action_TableName_IsActive",
                table: "sysevent_email_action",
                columns: new[] { "TableName", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_sysevent_email_template_Name",
                table: "sysevent_email_template",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_sla_SlaDefinitionId",
                table: "task_sla",
                column: "SlaDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_task_sla_Stage_HasBreached_DueAt",
                table: "task_sla",
                columns: new[] { "Stage", "HasBreached", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_task_sla_TableName_RecordId",
                table: "task_sla",
                columns: new[] { "TableName", "RecordId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_agent");

            migrationBuilder.DropTable(
                name: "ai_conversation");

            migrationBuilder.DropTable(
                name: "ai_message");

            migrationBuilder.DropTable(
                name: "ai_provider");

            migrationBuilder.DropTable(
                name: "ai_tool_call");

            migrationBuilder.DropTable(
                name: "alm_asset");

            migrationBuilder.DropTable(
                name: "automation_flow");

            migrationBuilder.DropTable(
                name: "automation_flow_run");

            migrationBuilder.DropTable(
                name: "automation_flow_run_step");

            migrationBuilder.DropTable(
                name: "change_task");

            migrationBuilder.DropTable(
                name: "cmdb_class");

            migrationBuilder.DropTable(
                name: "cmdb_rel_ci");

            migrationBuilder.DropTable(
                name: "contract_sla");

            migrationBuilder.DropTable(
                name: "incident");

            migrationBuilder.DropTable(
                name: "incident_category");

            migrationBuilder.DropTable(
                name: "int_data_source_endpoint");

            migrationBuilder.DropTable(
                name: "int_data_source_log");

            migrationBuilder.DropTable(
                name: "int_import_row");

            migrationBuilder.DropTable(
                name: "int_network_allow_list");

            migrationBuilder.DropTable(
                name: "item_option_new");

            migrationBuilder.DropTable(
                name: "kb_feedback");

            migrationBuilder.DropTable(
                name: "kb_m2m_ci");

            migrationBuilder.DropTable(
                name: "kb_version");

            migrationBuilder.DropTable(
                name: "portal_announcement");

            migrationBuilder.DropTable(
                name: "problem");

            migrationBuilder.DropTable(
                name: "sc_task");

            migrationBuilder.DropTable(
                name: "sys_api_key");

            migrationBuilder.DropTable(
                name: "sys_api_key_usage");

            migrationBuilder.DropTable(
                name: "sys_approval");

            migrationBuilder.DropTable(
                name: "sys_attachment");

            migrationBuilder.DropTable(
                name: "sys_audit_event");

            migrationBuilder.DropTable(
                name: "sys_custom_record");

            migrationBuilder.DropTable(
                name: "sys_dashboard_pin");

            migrationBuilder.DropTable(
                name: "sys_db_object");

            migrationBuilder.DropTable(
                name: "sys_dictionary");

            migrationBuilder.DropTable(
                name: "sys_directory_group_map");

            migrationBuilder.DropTable(
                name: "sys_directory_sync_run");

            migrationBuilder.DropTable(
                name: "sys_email_outbox");

            migrationBuilder.DropTable(
                name: "sys_group_has_role");

            migrationBuilder.DropTable(
                name: "sys_journal");

            migrationBuilder.DropTable(
                name: "sys_mcp_invocation");

            migrationBuilder.DropTable(
                name: "sys_mcp_tool");

            migrationBuilder.DropTable(
                name: "sys_notification_inbox");

            migrationBuilder.DropTable(
                name: "sys_number");

            migrationBuilder.DropTable(
                name: "sys_properties");

            migrationBuilder.DropTable(
                name: "sys_record_field_value");

            migrationBuilder.DropTable(
                name: "sys_report");

            migrationBuilder.DropTable(
                name: "sys_role_permission");

            migrationBuilder.DropTable(
                name: "sys_script");

            migrationBuilder.DropTable(
                name: "sys_secret");

            migrationBuilder.DropTable(
                name: "sys_ui_layout");

            migrationBuilder.DropTable(
                name: "sys_user_grmember");

            migrationBuilder.DropTable(
                name: "sys_user_has_role");

            migrationBuilder.DropTable(
                name: "sysevent_email_action");

            migrationBuilder.DropTable(
                name: "sysevent_email_template");

            migrationBuilder.DropTable(
                name: "task_sla");

            migrationBuilder.DropTable(
                name: "change_request");

            migrationBuilder.DropTable(
                name: "int_data_source");

            migrationBuilder.DropTable(
                name: "int_import_run");

            migrationBuilder.DropTable(
                name: "cmdb_ci");

            migrationBuilder.DropTable(
                name: "kb_knowledge");

            migrationBuilder.DropTable(
                name: "sc_req_item");

            migrationBuilder.DropTable(
                name: "sys_user_group");

            migrationBuilder.DropTable(
                name: "sys_role");

            migrationBuilder.DropTable(
                name: "sys_user");

            migrationBuilder.DropTable(
                name: "int_import_set");

            migrationBuilder.DropTable(
                name: "kb_category");

            migrationBuilder.DropTable(
                name: "sc_cat_item");

            migrationBuilder.DropTable(
                name: "sc_request");

            migrationBuilder.DropTable(
                name: "kb_knowledge_base");

            migrationBuilder.DropTable(
                name: "sc_category");
        }
    }
}
