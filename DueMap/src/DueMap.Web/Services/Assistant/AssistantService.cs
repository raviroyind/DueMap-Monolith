using System.Text;
using System.Text.Json;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Options;

namespace DueMap.Web.Services.Assistant;

/// <summary>
/// Conversation engine for the PM assistant. Scoped — one instance per Blazor
/// circuit — so the API-shaped history lives here for the duration of the
/// user's session while the page keeps its own display models.
///
/// The loop is manual (create → execute tool_use blocks → feed tool_results
/// back) rather than the SDK tool runner because we want to surface per-tool
/// activity to the UI ("Checking overdue invoices…") between iterations.
/// </summary>
public sealed partial class AssistantService
{
    /// <summary>Safety valve: max model⇄tool round trips per user question.</summary>
    private const int MaxToolIterations = 8;

    private readonly AnthropicClientProvider _provider;
    private readonly AssistantOptions _options;
    private readonly AssistantDataTools _tools;
    private readonly PmContext _pm;
    private readonly ILogger<AssistantService> _log;

    private readonly List<MessageParam> _history = new();

    public AssistantService(
        AnthropicClientProvider provider,
        IOptions<AssistantOptions> options,
        AssistantDataTools tools,
        PmContext pm,
        ILogger<AssistantService> log)
    {
        _provider = provider;
        _options = options.Value;
        _tools = tools;
        _pm = pm;
        _log = log;
    }

    public bool IsConfigured => _provider.IsConfigured;

    public void ResetConversation() => _history.Clear();

    /// <summary>
    /// Send one user message and run the tool loop to completion. Returns the
    /// assistant's final text. <paramref name="onActivity"/> receives short
    /// status lines ("Checking invoices…") the page can show while waiting.
    /// </summary>
    public async Task<string> AskAsync(string userMessage, Action<string>? onActivity, CancellationToken ct)
    {
        var client = _provider.Client
            ?? throw new InvalidOperationException("Anthropic:ApiKey is not configured.");

        var pmId = await _pm.GetPmIdAsync();
        var pmName = await _pm.GetUserDisplayNameAsync();

        // Checkpoint so a mid-loop failure can rewind cleanly — the API rejects
        // a conversation that ends in an unanswered tool_use block.
        var checkpoint = _history.Count;
        _history.Add(new() { Role = Role.User, Content = userMessage });

        try
        {
            for (var iteration = 0; iteration < MaxToolIterations; iteration++)
            {
                onActivity?.Invoke(iteration == 0 ? "Thinking…" : "Putting the answer together…");

                var response = await client.Messages.Create(new MessageCreateParams
                {
                    Model = _options.Model,
                    MaxTokens = _options.MaxTokens,
                    System = BuildSystemPrompt(pmName),
                    Thinking = new ThinkingConfigAdaptive(),
                    Tools = ToolDefinitions,
                    Messages = _history.ToList()
                }, cancellationToken: ct);

                // Rebuild the assistant turn as params for the follow-up request.
                // Thinking blocks must round-trip with their signature intact or
                // the API rejects the next call in the loop.
                var assistantContent = new List<ContentBlockParam>();
                var toolCalls = new List<ToolUseBlock>();
                var text = new StringBuilder();

                foreach (var block in response.Content)
                {
                    if (block.TryPickText(out TextBlock? t))
                    {
                        assistantContent.Add(new TextBlockParam { Text = t.Text });
                        text.Append(t.Text);
                    }
                    else if (block.TryPickThinking(out ThinkingBlock? th))
                    {
                        assistantContent.Add(new ThinkingBlockParam
                        {
                            Thinking = th.Thinking,
                            Signature = th.Signature
                        });
                    }
                    else if (block.TryPickRedactedThinking(out RedactedThinkingBlock? rt))
                    {
                        assistantContent.Add(new RedactedThinkingBlockParam { Data = rt.Data });
                    }
                    else if (block.TryPickToolUse(out ToolUseBlock? tu))
                    {
                        assistantContent.Add(new ToolUseBlockParam
                        {
                            ID = tu.ID,
                            Name = tu.Name,
                            Input = tu.Input
                        });
                        toolCalls.Add(tu);
                    }
                }

                _history.Add(new() { Role = Role.Assistant, Content = assistantContent });

                if (response.StopReason != "tool_use" || toolCalls.Count == 0)
                {
                    if (response.StopReason == "refusal")
                    {
                        return "I can't help with that request. Try asking about your portfolio, tenants, leases, or invoices.";
                    }
                    return text.Length > 0
                        ? text.ToString()
                        : "I wasn't able to produce an answer for that — try rephrasing the question.";
                }

                // Execute every requested tool; ALL results go back in a single
                // user message (splitting them degrades parallel tool use).
                var results = new List<ContentBlockParam>();
                foreach (var call in toolCalls)
                {
                    onActivity?.Invoke(ActivityLabel(call.Name));
                    string result;
                    try
                    {
                        result = await _tools.ExecuteAsync(call.Name, call.Input, pmId, ct);
                        results.Add(new ToolResultBlockParam { ToolUseID = call.ID, Content = result });
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        LogToolFailed(ex, call.Name, pmId);
                        results.Add(new ToolResultBlockParam
                        {
                            ToolUseID = call.ID,
                            Content = $"Tool failed: {ex.Message}",
                            IsError = true
                        });
                    }
                }

                _history.Add(new() { Role = Role.User, Content = results });
            }

            return "That question needed more lookups than I allow in one turn. Try asking something more specific.";
        }
        catch
        {
            // Drop the dangling turn(s) for this question so the next question
            // starts from a consistent history.
            _history.RemoveRange(checkpoint, _history.Count - checkpoint);
            throw;
        }
    }

    [LoggerMessage(EventId = 9801, Level = LogLevel.Error,
        Message = "Assistant tool {Tool} failed for PM {PmId}")]
    private partial void LogToolFailed(Exception ex, string tool, int pmId);

    private static string ActivityLabel(string toolName) => toolName switch
    {
        "get_portfolio_summary" => "Summarizing your portfolio…",
        "list_invoices"         => "Checking invoices…",
        "list_leases"           => "Reviewing leases…",
        "search_tenants"        => "Looking up tenants…",
        "get_lease_details"     => "Pulling lease details…",
        _                        => "Looking that up…"
    };

    private static string BuildSystemPrompt(string? pmName) => $"""
        You are the DueMap assistant, helping a property manager understand and manage their rental portfolio.

        About DueMap: it connects to the PM's accounting system (QuickBooks Online or Xero), syncs customers and rent invoices, links them to leases, sends rent reminders and late notices to tenants, and tracks late-fee assessments. Important: DueMap is notify-only for late fees — it warns tenants and tells the PM what fee applies, but never posts fees or edits invoices in QuickBooks/Xero. Autopay status is inferred from payment behavior, not read from the provider.

        Today's date is {DateTime.Today:yyyy-MM-dd}.{(string.IsNullOrWhiteSpace(pmName) ? "" : $" You are talking to {pmName}.")}

        You have read-only tools over this property manager's own data: portfolio summary, invoices, leases, and tenants. The data is already scoped to their workspace. Use the tools to answer — never invent numbers, names, or dates. If a tool returns no matching data, say so plainly. Amounts are in the invoice currency (USD unless stated).

        You cannot take actions (no sending notices, editing leases, or posting to the accounting system). When the user asks you to do something, explain where in DueMap to do it: notice settings are under "Notice preferences", customer↔lease linking under "Link customers" (or "Tenants"), invoice sync status under "Accounting", and daily-run history under "Processing status".

        Style: answer in plain text — no markdown headers, tables, bold, or code blocks. Short paragraphs and simple "-" bullet lists are fine. Lead with the direct answer, then supporting detail. Format money like $1,850.00 and dates like Mar 3, 2026. Keep answers brief; this is a chat panel, not a report.

        Only discuss this property manager's portfolio and DueMap. Politely decline unrelated requests.
        """;

    /// <summary>
    /// Tool schemas the model sees. Names/descriptions matter — they are how
    /// the model decides what to call. Kept in lockstep with
    /// <see cref="AssistantDataTools.ExecuteAsync"/>.
    /// </summary>
    private static readonly List<ToolUnion> ToolDefinitions =
    [
        new Tool
        {
            Name = "get_portfolio_summary",
            Description =
                "Get headline numbers for the property manager's portfolio as of today: active lease count, " +
                "monthly rent roll, customer count, open/overdue invoice counts and balances, autopay counts. " +
                "Call this for any 'how is my portfolio doing' or overview question.",
            InputSchema = new() { Properties = new Dictionary<string, JsonElement>() }
        },
        new Tool
        {
            Name = "list_invoices",
            Description =
                "List rent invoices with customer names. Filter by status: 'overdue' (open, unpaid, past due — the default), " +
                "'open' (unpaid, any due date), 'paid', or 'all'. Returns due dates, balances, and days overdue. " +
                "Call this when asked who owes rent, who is late, or about payments.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["status"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "string",
                        @enum = new[] { "overdue", "open", "paid", "all" },
                        description = "Which invoices to return. Default overdue."
                    }),
                    ["limit"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "integer",
                        description = "Max rows to return, 1-100. Default 25."
                    })
                }
            }
        },
        new Tool
        {
            Name = "list_leases",
            Description =
                "List active leases with tenant name, unit, monthly rent, start/end dates, and autopay status. " +
                "Pass expiring_within_days to get only leases ending within that many days (sorted by end date) — " +
                "use that for renewal/expiration questions.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["expiring_within_days"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "integer",
                        description = "Only leases whose end date falls within this many days from today."
                    })
                }
            }
        },
        new Tool
        {
            Name = "search_tenants",
            Description =
                "Find tenants (customers) by name or email, with their active leases. " +
                "Call this whenever the user mentions a tenant by name.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["query"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "string",
                        description = "Part of the tenant's name or email address."
                    })
                },
                Required = ["query"]
            }
        },
        new Tool
        {
            Name = "get_lease_details",
            Description =
                "Full detail for one lease by its id: rent, term, tenant contact info, late-fee profile, current due date, " +
                "and recent invoice history. Use after list_leases or search_tenants when the user drills into one lease.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["lease_id"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "integer",
                        description = "The lease id."
                    })
                },
                Required = ["lease_id"]
            }
        }
    ];
}
