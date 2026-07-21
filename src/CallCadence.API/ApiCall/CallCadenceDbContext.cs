using System.Text.Json;
using CallCadence.API.Auth;
using CallCadence.Domain.ApiCall;
using CallCadence.Domain.Tags;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CallCadence.Infrastructure.ApiCall;

/// <summary>
/// Database context for CallCadence application.
/// </summary>
public sealed class CallCadenceDbContext : IdentityDbContext<AdminUser>, IDataProtectionKeyContext
{
    private static readonly ValueConverter<List<NamedValue>, string> NamedValueListConverter =
        new(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<NamedValue>>(v, (JsonSerializerOptions?)null) ?? new List<NamedValue>());

    private static readonly ValueComparer<List<NamedValue>> NamedValueListComparer =
        new(
            (c1, c2) => JsonSerializer.Serialize(c1, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(c2, (JsonSerializerOptions?)null),
            c => c.Aggregate(0, (hash, item) => HashCode.Combine(hash, (item.Name ?? string.Empty).GetHashCode(), (item.Value ?? string.Empty).GetHashCode())),
            c => JsonSerializer.Deserialize<List<NamedValue>>(JsonSerializer.Serialize(c, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!);

    public CallCadenceDbContext(DbContextOptions<CallCadenceDbContext> options)
        : base(options)
    {
    }

    public DbSet<Domain.ApiCall.ApiCall> ApiCalls => Set<Domain.ApiCall.ApiCall>();
    public DbSet<ApiCallSchedule> ApiCallSchedules => Set<ApiCallSchedule>();
    public DbSet<ApiCallArchive> ApiCallArchives => Set<ApiCallArchive>();
    public DbSet<ApiCallLog> ApiCallLogs => Set<ApiCallLog>();
    public DbSet<SsoConfiguration> SsoConfigurations => Set<SsoConfiguration>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Domain.ApiCall.ApiCall>(entity =>
        {
            entity.ToTable("ApiCalls");
            entity.HasKey(e => e.PkId);
            entity.Property(e => e.PkId).UseIdentityColumn();
            entity.Property(e => e.Id).IsRequired();
            entity.HasIndex(e => e.Id).IsUnique();
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.HttpMethod).IsRequired().HasMaxLength(10);
            entity.Property(e => e.EndpointUrl).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Payload);
            entity.Property(e => e.Headers)
                .HasConversion(NamedValueListConverter, NamedValueListComparer)
                .HasColumnType("nvarchar(max)");
            entity.Property(e => e.Parameters)
                .HasConversion(NamedValueListConverter, NamedValueListComparer)
                .HasColumnType("nvarchar(max)");
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ModifiedAt).IsRequired();
        });

        modelBuilder.Entity<ApiCallSchedule>(entity =>
        {
            entity.ToTable("ApiCallSchedules");
            entity.HasKey(e => e.PkId);
            entity.Property(e => e.PkId).UseIdentityColumn();
            entity.Property(e => e.Id).IsRequired();
            entity.HasIndex(e => e.Id).IsUnique();
            entity.Property(e => e.ApiCallId).IsRequired();
            entity.Property(e => e.CronExpression).IsRequired().HasMaxLength(500);
            entity.Property(e => e.IsEnabled).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ModifiedAt).IsRequired();
            entity.HasIndex(e => e.ApiCallId);
        });

        modelBuilder.Entity<ApiCallArchive>(entity =>
        {
            entity.ToTable("ApiCallArchives");
            entity.HasKey(e => e.PkId);
            entity.Property(e => e.PkId).UseIdentityColumn();
            entity.Property(e => e.Id).IsRequired();
            entity.HasIndex(e => e.Id).IsUnique();
            entity.Property(e => e.ApiCallId).IsRequired();
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.HttpMethod).IsRequired().HasMaxLength(10);
            entity.Property(e => e.EndpointUrl).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Payload);
            entity.Property(e => e.Headers)
                .HasConversion(NamedValueListConverter, NamedValueListComparer)
                .HasColumnType("nvarchar(max)");
            entity.Property(e => e.Parameters)
                .HasConversion(NamedValueListConverter, NamedValueListComparer)
                .HasColumnType("nvarchar(max)");
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.ArchivedAt).IsRequired();
            entity.Property(e => e.OriginalCreatedAt).IsRequired();
            entity.Property(e => e.OriginalModifiedAt).IsRequired();
            entity.HasIndex(e => e.ApiCallId);
        });

        modelBuilder.Entity<ApiCallLog>(entity =>
        {
            entity.ToTable("ApiCallLogs");
            entity.HasKey(e => e.PkId);
            entity.Property(e => e.PkId).UseIdentityColumn();
            entity.Property(e => e.Id).IsRequired();
            entity.HasIndex(e => e.Id).IsUnique();
            entity.Property(e => e.ApiCallId).IsRequired();
            entity.Property(e => e.HttpMethod).IsRequired().HasMaxLength(10);
            entity.Property(e => e.RequestUri).HasMaxLength(4000);
            entity.Property(e => e.RequestHeaders)
                .HasConversion(NamedValueListConverter, NamedValueListComparer)
                .HasColumnType("nvarchar(max)");
            entity.Property(e => e.RequestParameters)
                .HasConversion(NamedValueListConverter, NamedValueListComparer)
                .HasColumnType("nvarchar(max)");
            entity.Property(e => e.RequestBody);
            entity.Property(e => e.ResponseCode).IsRequired();
            entity.Property(e => e.ResponseBody);
            entity.Property(e => e.ExecutedAt).IsRequired();
            entity.Property(e => e.DurationMs).IsRequired();
            entity.Property(e => e.Success).IsRequired();
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.HasIndex(e => e.ApiCallId);
            entity.HasIndex(e => e.ExecutedAt);
        });

        modelBuilder.Entity<SsoConfiguration>(entity =>
        {
            entity.ToTable("SsoConfigurations");
            entity.HasKey(e => e.PkId);
            entity.Property(e => e.PkId).UseIdentityColumn();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SchemeName).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.SchemeName).IsUnique();
            entity.Property(e => e.IsEnabled).IsRequired();
            entity.Property(e => e.Authority).HasMaxLength(2000);
            entity.Property(e => e.ClientId).HasMaxLength(200);
            entity.Property(e => e.ClientSecret).HasMaxLength(2000);
            entity.Property(e => e.MetadataAddress).HasMaxLength(2000);
            entity.Property(e => e.CallbackPath).HasMaxLength(200);
            entity.Property(e => e.UpdatedAt).IsRequired();
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.ToTable("Tags");
            entity.HasKey(e => e.PkId);
            entity.Property(e => e.PkId).UseIdentityColumn();
            entity.Property(e => e.Value).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.Value).IsUnique();
        });
    }
}
