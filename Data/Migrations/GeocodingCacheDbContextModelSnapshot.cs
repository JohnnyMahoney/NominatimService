using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace GeocoderSolution.Data.Migrations;

[DbContext(typeof(GeocodingCacheDbContext))]
public sealed class GeocodingCacheDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.11");

        modelBuilder.Entity("GeocoderSolution.Data.GeocodingCacheEntry", entity =>
        {
            entity.Property<string>("CacheKey")
                .HasMaxLength(512)
                .HasColumnType("TEXT");

            entity.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("TEXT");

            entity.Property<string>("DisplayName")
                .HasColumnType("TEXT");

            entity.Property<double>("Latitude")
                .HasColumnType("REAL");

            entity.Property<double>("Longitude")
                .HasColumnType("REAL");

            entity.Property<string>("Name")
                .HasColumnType("TEXT");

            entity.Property<string>("Strategy")
                .IsRequired()
                .HasMaxLength(32)
                .HasColumnType("TEXT");

            entity.HasKey("CacheKey");
            entity.ToTable("GeocodingCacheEntries");
        });
    }
}
