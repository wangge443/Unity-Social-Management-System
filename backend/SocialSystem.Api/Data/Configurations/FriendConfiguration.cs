using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocialSystem.Api.Models.Entities;

namespace SocialSystem.Api.Data.Configurations;

public sealed class FriendConfiguration : IEntityTypeConfiguration<Friend>
{
    public void Configure(EntityTypeBuilder<Friend> b)
    {
        b.ToTable("friends", t =>
        {
            t.HasCheckConstraint("ck_friends_order", "user_low_id < user_high_id");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint").ValueGeneratedOnAdd();
        b.Property(x => x.UserLowId).HasColumnName("user_low_id").HasColumnType("bigint");
        b.Property(x => x.UserHighId).HasColumnName("user_high_id").HasColumnType("bigint");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        b.HasIndex(x => new { x.UserLowId, x.UserHighId }).IsUnique().HasDatabaseName("uq_friends_pair");
        b.HasIndex(x => x.UserHighId).HasDatabaseName("ix_friends_high_id");
        b.HasOne(x => x.LowUser).WithMany().HasForeignKey(x => x.UserLowId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_friends_low_user");
        b.HasOne(x => x.HighUser).WithMany().HasForeignKey(x => x.UserHighId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_friends_high_user");
    }
}

