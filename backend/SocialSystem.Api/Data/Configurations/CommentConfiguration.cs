using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocialSystem.Api.Models.Entities;

namespace SocialSystem.Api.Data.Configurations;

public sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> b)
    {
        b.ToTable("comments", t =>
        {
            t.HasCheckConstraint("ck_comments_content", "CHAR_LENGTH(TRIM(content)) > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint").ValueGeneratedOnAdd();
        b.Property(x => x.PostId).HasColumnName("post_id").HasColumnType("bigint");
        b.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("bigint");
        b.Property(x => x.Content).HasColumnName("content").HasColumnType("varchar(500)").HasMaxLength(500).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        b.HasIndex(x => new { x.PostId, x.Id }).HasDatabaseName("ix_comments_post_id");
        b.HasIndex(x => x.UserId).HasDatabaseName("ix_comments_user_id");
        b.HasOne(x => x.Post).WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_comments_post");
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_comments_user");
    }
}

