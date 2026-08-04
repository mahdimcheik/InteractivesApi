using InteractivesApi.Models.Presentations;
using InteractivesApi.Models.Users;
using InteractivesApi.Utilities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InteractivesApi.Contexts
{
    public class MainContext : IdentityDbContext<UserApp, RoleApp, Guid>
    {
        public MainContext(DbContextOptions<MainContext> options)
            : base(options) { }

        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<StatusAccount> StatusAccounts => Set<StatusAccount>();
        public DbSet<Presentation> Presentations => Set<Presentation>();
        public DbSet<Slide> Slides => Set<Slide>();
        public DbSet<Element> Elements => Set<Element>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // One refresh token per user (rotated in place).
            builder
                .Entity<RefreshToken>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Entity<RefreshToken>().HasIndex(r => r.UserId).IsUnique();

            // Slides/Elements are owned by their parent — deleting the parent deletes them.
            builder
                .Entity<Slide>()
                .HasOne(s => s.Presentation)
                .WithMany(p => p.Slides)
                .HasForeignKey(s => s.PresentationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder
                .Entity<Element>()
                .HasOne(e => e.Slide)
                .WithMany(s => s.Elements)
                .HasForeignKey(e => e.SlideId)
                .OnDelete(DeleteBehavior.Cascade);

            // A user can't be deleted while owning presentations.
            builder
                .Entity<Presentation>()
                .HasOne(p => p.Creator)
                .WithMany()
                .HasForeignKey(p => p.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Slide>().HasIndex(s => new { s.PresentationId, s.Order });

            SeedLookups(builder);
        }

        private static void SeedLookups(ModelBuilder builder)
        {
            // Roles. NormalizedName must match Identity's normalizer (upper-invariant).
            builder
                .Entity<RoleApp>()
                .HasData(
                    new RoleApp
                    {
                        Id = HardCode.ROLE_SUPER_ADMIN,
                        Name = HardCode.ROLE_NAME_SUPER_ADMIN,
                        NormalizedName = HardCode.ROLE_NAME_SUPER_ADMIN.ToUpperInvariant(),
                        ConcurrencyStamp = HardCode.ROLE_SUPER_ADMIN.ToString(),
                    },
                    new RoleApp
                    {
                        Id = HardCode.ROLE_ADMIN,
                        Name = HardCode.ROLE_NAME_ADMIN,
                        NormalizedName = HardCode.ROLE_NAME_ADMIN.ToUpperInvariant(),
                        ConcurrencyStamp = HardCode.ROLE_ADMIN.ToString(),
                    },
                    new RoleApp
                    {
                        Id = HardCode.ROLE_CLIENT,
                        Name = HardCode.ROLE_NAME_CLIENT,
                        NormalizedName = HardCode.ROLE_NAME_CLIENT.ToUpperInvariant(),
                        ConcurrencyStamp = HardCode.ROLE_CLIENT.ToString(),
                    },
                    new RoleApp
                    {
                        Id = HardCode.ROLE_PROFESSIONAL,
                        Name = HardCode.ROLE_NAME_PROFESSIONAL,
                        NormalizedName = HardCode.ROLE_NAME_PROFESSIONAL.ToUpperInvariant(),
                        ConcurrencyStamp = HardCode.ROLE_PROFESSIONAL.ToString(),
                    }
                );

            // Account statuses (fixed CreatedAt so migrations stay deterministic).
            var seedDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            builder
                .Entity<StatusAccount>()
                .HasData(
                    new StatusAccount
                    {
                        Id = HardCode.ACCOUNT_ACTIVE,
                        Name = "Active",
                        Color = "#22c55e",
                        CreatedAt = seedDate,
                    },
                    new StatusAccount
                    {
                        Id = HardCode.ACCOUNT_PENDING,
                        Name = "Pending",
                        Color = "#eab308",
                        CreatedAt = seedDate,
                    },
                    new StatusAccount
                    {
                        Id = HardCode.ACCOUNT_BANNED,
                        Name = "Banned",
                        Color = "#ef4444",
                        CreatedAt = seedDate,
                    }
                );
        }
    }
}
