using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DAS.Api.Data;
using DAS.Api.Models;

namespace DAS.Api.Services
{
    public interface IPartnerService
    {
        Task<ExternalEntity> CreatePartnerAsync(ExternalEntity partner);
        Task<ExternalEntity?> GetPartnerByIdAsync(Guid id);
        Task<IEnumerable<ExternalEntity>> GetAllPartnersAsync();
        Task<ExternalEntity> UpdatePartnerAsync(ExternalEntity partner);
        Task<bool> DeletePartnerAsync(Guid id);
    }

    public class PartnerService : IPartnerService
    {
        private readonly AppDbContext _context;

        public PartnerService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ExternalEntity> CreatePartnerAsync(ExternalEntity partner)
        {
            partner.CreatedAt = DateTime.UtcNow;
            _context.ExternalEntities.Add(partner);
            await _context.SaveChangesAsync();
            return partner;
        }

        public async Task<ExternalEntity?> GetPartnerByIdAsync(Guid id)
        {
            return await _context.ExternalEntities.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
        }

        public async Task<IEnumerable<ExternalEntity>> GetAllPartnersAsync()
        {
            return await _context.ExternalEntities.Where(e => !e.IsDeleted).ToListAsync();
        }

        public async Task<ExternalEntity> UpdatePartnerAsync(ExternalEntity partner)
        {
            _context.Entry(partner).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return partner;
        }

        public async Task<bool> DeletePartnerAsync(Guid id)
        {
            var partner = await _context.ExternalEntities.FindAsync(id);
            if (partner == null) return false;

            partner.IsDeleted = true; // Soft Delete
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
