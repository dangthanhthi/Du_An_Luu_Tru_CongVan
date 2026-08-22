using AiOcrService.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AiOcrService.Services
{
    public interface IOcrRuleService
    {
        Task<List<OcrPatternRule>> GetRulesAsync(string? ruleType = null, bool? isActive = null);
        Task<OcrPatternRule?> GetRuleByIdAsync(Guid id);
        Task<OcrPatternRule> CreateRuleAsync(CreateOcrRuleRequest request);
        Task<OcrPatternRule?> UpdateRuleAsync(Guid id, UpdateOcrRuleRequest request);
        Task<bool> DeleteRuleAsync(Guid id);
        Task<List<OcrPatternRule>> ResetToDefaultsAsync();
        object TestPattern(TestPatternRequest request);
    }
}
