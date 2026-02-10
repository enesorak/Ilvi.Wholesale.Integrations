using Ilvi.Infrastructure.Data;
using Ilvi.Modules.AmoCrm.Domain.Contacts;
using Ilvi.Modules.AmoCrm.Domain.Events;
using Ilvi.Modules.AmoCrm.Domain.Leads;
using Ilvi.Modules.AmoCrm.Domain.Messages;
using Ilvi.Modules.AmoCrm.Domain.Pipelines;
using Ilvi.Modules.AmoCrm.Domain.Settings;
using Ilvi.Modules.AmoCrm.Domain.Tasks;
using Ilvi.Modules.AmoCrm.Domain.Users;
using Ilvi.Modules.AmoCrm.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Ilvi.Modules.AmoCrm.Infrastructure.Persistence.Data;

public class AmoCrmDbContext : AppDbContext
{
    public AmoCrmDbContext(DbContextOptions<AmoCrmDbContext> options) : base(options) { }

    public DbSet<Contact> Contacts { get; set; }
    public DbSet<Lead> Leads { get; set; }
    public DbSet<AmoTask> Tasks { get; set; }
    public DbSet<Pipeline> Pipelines { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<AmoEvent> Events { get; set; }
    public DbSet<AmoMessage> Messages { get; set; }
    public DbSet<AppSetting> AppSettings { get; set; }


protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(AmoCrmDbContext).Assembly);
    
    // İsim düzeltmelerini uygula
    modelBuilder.ApplyPowerBiNamingConvention();
}

}