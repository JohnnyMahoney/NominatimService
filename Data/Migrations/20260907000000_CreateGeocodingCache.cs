using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeocoderSolution.Data.Migrations;

[DbContext(typeof(GeocodingCacheDbContext))]
[Migration("20260907000000_CreateGeocodingCache")]
public sealed class CreateGeocodingCache : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "GeocodingCacheEntries",
            columns: table => new
            {
                CacheKey = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                Latitude = table.Column<double>(type: "REAL", nullable: false),
                Longitude = table.Column<double>(type: "REAL", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: true),
                DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                Strategy = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GeocodingCacheEntries", entry => entry.CacheKey);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "GeocodingCacheEntries");
    }
}
