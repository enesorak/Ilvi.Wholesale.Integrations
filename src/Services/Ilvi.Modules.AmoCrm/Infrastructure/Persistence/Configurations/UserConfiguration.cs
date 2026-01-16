using Ilvi.Modules.AmoCrm.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ilvi.Modules.AmoCrm.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, val => new UserId(val))
            .ValueGeneratedNever();
            
        builder.Property(x => x.Raw).HasColumnType("nvarchar(max)");
    }
}