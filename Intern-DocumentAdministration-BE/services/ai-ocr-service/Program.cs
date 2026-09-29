using AiOcrService.Services;
using AiOcrService.Services.Glyphs;
using AiOcrService.Services.FuzzyCorrection;
using AiOcrService.Services.Concurrency;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Đăng ký HttpClient để gọi giao tiếp inter-service
builder.Services.AddHttpClient();

// Đăng ký các Trạm OCR & Matcher & Dynamic Rule Engine & Glyph Normalizer & Fuzzy Corrector & Concurrency Gate
builder.Services.AddSingleton<IOcrConcurrencyGate>(_ => new SemaphoreOcrConcurrencyGate(maxConcurrentFiles: 2));
builder.Services.AddSingleton<IGlyphNormalizer, HandwrittenGlyphNormalizer>();
builder.Services.AddSingleton<IFuzzyTextCorrector>(_ => new TrieBkTreeFuzzyCorrector(VocabularySeed.FromLegacyAutoCorrectTable()));
builder.Services.AddSingleton<IOcrEngine, PaddleOcrEngine>();
builder.Services.AddSingleton<IPartnerMatcher, PartnerMatcher>();
builder.Services.AddSingleton<IOcrRuleService, OcrRuleService>();
builder.Services.AddSingleton<IDynamicFieldExtractor, DynamicFieldExtractor>();

builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy => {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.UseAuthorization();
app.MapControllers();

app.Run();
