using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DAS.DocumentService.Data;
using DAS.DocumentService.Entities;

namespace DAS.DocumentService.Services
{
    public class DocumentNumberGenerator : IDocumentNumberGenerator
    {
        private readonly DocumentDbContext _context;

        public DocumentNumberGenerator(DocumentDbContext context)
        {
            _context = context;
        }

        public async Task<string> GenerateDocumentNumberAsync(string docType, int? year = null)
        {
            var targetYear = year ?? DateTime.UtcNow.Year;
            var upperDocType = docType.ToUpperInvariant();

            string prefix = upperDocType switch
            {
                "INCOMING" => "CV-DEN",
                "OUTGOING" => "CV-DI",
                "INTERNAL" => "CV-NB",
                _ => "CV"
            };

            int nextVal = 1;

            // Execute in an isolated transaction using UPDLOCK & HOLDLOCK to ensure zero duplicate numbers under high concurrency
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Raw SQL with SQL Server locks: WITH (UPDLOCK, HOLDLOCK)
                var sql = @"
                    SELECT current_value 
                    FROM document.document_number_counters WITH (UPDLOCK, HOLDLOCK)
                    WHERE doc_type = {0} AND year = {1}";

                var currentCounter = await _context.DocumentNumberCounters
                    .FromSqlRaw(sql, upperDocType, targetYear)
                    .FirstOrDefaultAsync();

                if (currentCounter == null)
                {
                    currentCounter = new DocumentNumberCounterEntity
                    {
                        DocType = upperDocType,
                        Year = targetYear,
                        CurrentValue = 1
                    };
                    _context.DocumentNumberCounters.Add(currentCounter);
                    nextVal = 1;
                }
                else
                {
                    currentCounter.CurrentValue += 1;
                    nextVal = currentCounter.CurrentValue;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return $"{prefix}-{targetYear}-{nextVal:D4}";
        }
    }
}
