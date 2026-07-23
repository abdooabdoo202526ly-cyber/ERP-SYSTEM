using System.Text.Json;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Modules.Finance.Infrastructure;

namespace ERPSystem.Modules.Finance.Application.Services;

public sealed class PostingRulesService : IPostingRulesService
{
    private readonly IPostingRuleRepository _rules;
    private readonly IAccountRepository _accounts;
    private readonly IJournalEntryService _journalService;
    private readonly ILogger<PostingRulesService> _logger;

    public PostingRulesService(
        IPostingRuleRepository rules,
        IAccountRepository accounts,
        IJournalEntryService journalService,
        ILogger<PostingRulesService> logger)
    {
        _rules = rules;
        _accounts = accounts;
        _journalService = journalService;
        _logger = logger;
    }

    public async Task<FinanceResult<PostingRule>> CreateAsync(Guid tenantId, CreatePostingRuleRequest request, CancellationToken ct)
    {
        // التحقق من صحة كل account code في الـ template
        foreach (var line in request.Template.Lines)
        {
            var acc = await _accounts.GetByCodeAsync(tenantId, line.AccountCode, ct);
            if (acc == null)
            {
                return FinanceResult<PostingRule>.Fail(
                    $"كود الحساب '{line.AccountCode}' غير موجود في دليل الحسابات.",
                    FinanceErrorCode.NotFound);
            }
            if (!acc.IsPostable)
            {
                return FinanceResult<PostingRule>.Fail(
                    $"الحساب '{line.AccountCode}' تجميعي — لا يصلح للقيد.",
                    FinanceErrorCode.InvalidAccount);
            }
        }

        var now = DateTime.UtcNow;
        var rule = new PostingRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            EventType = request.EventType,
            IsActive = true,
            TemplateJson = JsonSerializer.Serialize(request.Template),
            CreatedAt = now,
            UpdatedAt = now
        };
        await _rules.InsertAsync(rule, ct);
        _logger.LogInformation("تم إنشاء قاعدة ترحيل {Name} ({EventType})", rule.Name, rule.EventType);
        return FinanceResult<PostingRule>.Ok(rule);
    }

    public async Task<FinanceResult<IReadOnlyList<PostingRule>>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        var list = await _rules.ListAsync(tenantId, ct);
        return FinanceResult<IReadOnlyList<PostingRule>>.Ok(list);
    }

    public async Task<FinanceResult<PostingRule>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var r = await _rules.GetByIdAsync(id, ct);
        if (r == null || r.TenantId != tenantId)
            return FinanceResult<PostingRule>.Fail("القاعدة غير موجودة.", FinanceErrorCode.NotFound);
        return FinanceResult<PostingRule>.Ok(r);
    }

    public async Task<FinanceResult<PostingRule>> UpdateAsync(Guid tenantId, Guid id, CreatePostingRuleRequest request, CancellationToken ct)
    {
        var r = await _rules.GetByIdAsync(id, ct);
        if (r == null || r.TenantId != tenantId)
            return FinanceResult<PostingRule>.Fail("القاعدة غير موجودة.", FinanceErrorCode.NotFound);
        r.Name = request.Name;
        r.Description = request.Description;
        r.EventType = request.EventType;
        if (request.IsActive.HasValue) r.IsActive = request.IsActive.Value;
        if (!string.IsNullOrEmpty(request.TemplateJson)) r.TemplateJson = request.TemplateJson;
        r.UpdatedAt = DateTime.UtcNow;
        await _rules.UpdateAsync(r, ct);
        return FinanceResult<PostingRule>.Ok(r);
    }

    public async Task<int> ApplyRulesAsync(Guid tenantId, Guid userId, TriggeringEvent eventType, EventPayload payload, CancellationToken ct)
    {
        var rules = await _rules.ListActiveByEventAsync(tenantId, eventType, ct);
        if (rules.Count == 0)
        {
            _logger.LogDebug("لا توجد قواعد نشطة لـ {EventType} للمستأجر {TenantId}", eventType, tenantId);
            return 0;
        }

        var posted = 0;
        foreach (var rule in rules)
        {
            PostingRuleTemplate? template;
            try
            {
                template = JsonSerializer.Deserialize<PostingRuleTemplate>(rule.TemplateJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "قاعدة {RuleId} لها template_json غير صالح", rule.Id);
                continue;
            }
            if (template == null || template.Lines.Count < 2) continue;

            // حساب السطور
            var req = new PostJournalEntryRequest
            {
                EntryDate = payload.EntryDate,
                Description = string.IsNullOrEmpty(payload.Description) ? template.Description : payload.Description,
                Reference = payload.Reference ?? template.Reference,
                Lines = new List<PostJournalLineRequest>()
            };

            decimal totalDebit = 0, totalCredit = 0;
            foreach (var line in template.Lines)
            {
                var acc = await _accounts.GetByCodeAsync(tenantId, line.AccountCode, ct);
                if (acc == null)
                {
                    _logger.LogWarning("تخطي سطر: الحساب {Code} غير موجود", line.AccountCode);
                    continue;
                }

                var amount = EvaluateFormula(line.AmountFormula, payload);
                var isDebit = line.Side.Equals("debit", StringComparison.OrdinalIgnoreCase);
                req.Lines.Add(new PostJournalLineRequest
                {
                    AccountId = acc.Id,
                    Debit = isDebit ? amount : 0,
                    Credit = isDebit ? 0 : amount
                });
                if (isDebit) totalDebit += amount; else totalCredit += amount;
            }

            // معادلة الـ double-entry
            if (totalDebit != totalCredit)
            {
                _logger.LogError("قاعدة {RuleId} تنتج قيد غير متوازن: D={D} C={C} — تم التخطي",
                    rule.Id, totalDebit, totalCredit);
                continue;
            }

            var result = await _journalService.CreateDraftAsync(tenantId, userId, req, ct);
            if (!result.Succeeded)
            {
                _logger.LogError("فشل إنشاء القيد من القاعدة {RuleId}: {Error}", rule.Id, result.Error);
                continue;
            }

            // ترحيل فوري
            var post = await _journalService.PostAsync(tenantId, userId, result.Value!.Id, ct);
            if (post.Succeeded)
            {
                posted++;
                _logger.LogInformation("تم تطبيق القاعدة {RuleId} وإنشاء القيد {EntryNumber}",
                    rule.Id, post.Value!.EntryNumber);
            }
        }
        return posted;
    }

    public async Task EnsureDefaultRulesAsync(Guid tenantId, CancellationToken ct)
    {
        // ====== StockReceived → Inventory Debit / Accounts Payable Credit ======
        var existingStock = (await _rules.ListActiveByEventAsync(tenantId, TriggeringEvent.StockReceived, ct))
            .FirstOrDefault();
        if (existingStock == null)
        {
            var stockRule = new PostingRule
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "استلام بضاعة (افتراضي)",
                Description = "عند استلام بضاعة، مدين المخزون ودائن الدائنون",
                EventType = TriggeringEvent.StockReceived,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                TemplateJson = JsonSerializer.Serialize(new PostingRuleTemplate
                {
                    Description = "استلام بضاعة",
                    Lines = new()
                    {
                        new() { AccountCode = "1300", Side = "debit", AmountFormula = "{amount}" },
                        new() { AccountCode = "2100", Side = "credit", AmountFormula = "{amount}" }
                    }
                })
            };
            await _rules.InsertAsync(stockRule, ct);
        }
    }

    /// <summary>صيغ بسيطة: {amount} → payload.Amount، أو رقم خام</summary>
    private decimal EvaluateFormula(string formula, EventPayload payload)
    {
        if (string.IsNullOrWhiteSpace(formula)) return 0;
        var trimmed = formula.Trim();
        if (trimmed == "{amount}") return payload.Amount;
        if (trimmed == "{amount}*2") return payload.Amount * 2;
        if (decimal.TryParse(trimmed, out var n)) return n;
        _logger.LogWarning("صيغة مبلغ غير معروفة: {Formula}", formula);
        return 0;
    }
}
