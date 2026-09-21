using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocialSystem.Api.Models.Entities;

namespace SocialSystem.Api.Data.Configurations;

public sealed class LikeConfiguration : IEntityTypeConfiguration<Like>
{
    public void Configure(EntityTypeBuilder<Like> b)
    {
        b.ToTable("likes", t =>
        {
        
        });
        b.HasKey(x => new { x.PostId, x.UserId });
        b.Property(x => x.PostId).HasColumnName("post_id").HasColumnType("bigint").ValueGeneratedNever();
        b.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("bigint").ValueGeneratedNever();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        b.HasIndex(x => x.UserId).HasDatabaseName("ix_likes_user_id");
        b.HasOne(x => x.Post).WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_likes_post");
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_likes_user");
    }
}

