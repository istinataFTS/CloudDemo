using Garage.Data.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Garage.Data;

public class GarageDbContext(DbContextOptions<GarageDbContext> options)
    : IdentityDbContext<GarageUser>(options)
{
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Photo> Photos => Set<Photo>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Must come first: Identity configures its own seven tables here.
        base.OnModelCreating(builder);

        // Identity checks email uniqueness in code, which two registrations
        // racing each other can slip past. Login looks a user up by email,
        // and a duplicate would break login for both accounts — so the
        // database enforces it too.
        builder.Entity<GarageUser>()
            .HasIndex(u => u.NormalizedEmail)
            .IsUnique();

        builder.Entity<Post>(post =>
        {
            // Deleting an account deletes its posts. Nothing is left
            // pointing at a user who no longer exists.
            post.HasOne(p => p.Author)
                .WithMany(u => u.Posts)
                .HasForeignKey(p => p.AuthorId)
                .OnDelete(DeleteBehavior.Cascade);

            // The feed sorts everyone's posts by time; a profile sorts
            // one author's posts by time. One index for each question.
            post.HasIndex(p => p.CreatedAt);
            post.HasIndex(p => new { p.AuthorId, p.CreatedAt });
        });

        builder.Entity<Photo>(photo =>
        {
            photo.Property(p => p.Status)
                 .HasConversion(new SnakeCaseEnumConverter<PhotoStatus>());

            photo.HasOne(p => p.Post)
                 .WithMany(p => p.Photos)
                 .HasForeignKey(p => p.PostId)
                 .OnDelete(DeleteBehavior.Cascade);

            photo.HasIndex(p => p.PostId);
            photo.HasIndex(p => p.Status);
        });

        // UseSnakeCaseNamingConvention() renames every column, but it never
        // overrides a name someone set explicitly — and Identity names all
        // seven of its tables ("AspNetUsers") and three of its indexes
        // ("EmailIndex"). Rewrite every table and index name here, so one
        // casing rule covers the whole database, including anything a
        // package adds later.
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            if (entity.GetTableName() is { } table)
            {
                entity.SetTableName(SnakeCase.From(table));
            }

            foreach (var index in entity.GetIndexes())
            {
                if (index.GetDatabaseName() is { } name)
                {
                    index.SetDatabaseName(SnakeCase.From(name));
                }
            }
        }
    }
}
