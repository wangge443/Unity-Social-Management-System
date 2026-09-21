using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocialSystem.Api.Models.Entities;

namespace SocialSystem.Api.Data.Configurations;

public sealed class FriendRequestConfiguration : IEntityTypeConfiguration<FriendRequest>
{
    public void Configure(EntityTypeBuilder<FriendRequest> b)
    {
        b.ToTable("friend_requests", t =>
        {
            t.HasCheckConstraint("ck_friend_requests_different_users", "sender_id <> receiver_id");
            t.HasCheckConstraint("ck_friend_requests_status", "status IN (0, 1, 2)");
            t.HasCheckConstraint("ck_friend_requests_handled_at", "(status = 0 AND handled_at IS NULL) OR (status IN (1, 2) AND handled_at IS NOT NULL AND handled_at >= created_at)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint").ValueGeneratedOnAdd();
        b.Property(x => x.SenderId).HasColumnName("sender_id").HasColumnType("bigint");
        b.Property(x => x.ReceiverId).HasColumnName("receiver_id").HasColumnType("bigint");
        b.Property(x => x.Status).HasColumnName("status").HasColumnType("tinyint");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        b.Property(x => x.HandledAt).HasColumnName("handled_at").HasColumnType("datetime(6)");
        b.Property(x => x.PendingLowId).HasColumnName("pending_low_id").HasColumnType("bigint");
        b.Property(x => x.PendingHighId).HasColumnName("pending_high_id").HasColumnType("bigint");
        b.Property(x => x.Status).HasConversion<sbyte>().HasDefaultValue(FriendRequestStatus.Pending).HasComment("0=Pending, 1=Accepted, 2=Rejected");
        b.Property(x => x.PendingLowId).HasComputedColumnSql("CASE WHEN status = 0 THEN LEAST(sender_id, receiver_id) ELSE NULL END", stored: false);
        b.Property(x => x.PendingHighId).HasComputedColumnSql("CASE WHEN status = 0 THEN GREATEST(sender_id, receiver_id) ELSE NULL END", stored: false);
        b.HasIndex(x => new { x.PendingLowId, x.PendingHighId }).IsUnique().HasDatabaseName("uq_friend_requests_pending_pair");
        b.HasIndex(x => new { x.ReceiverId, x.Status, x.Id }).HasDatabaseName("ix_friend_requests_receiver_status_id");
        b.HasIndex(x => new { x.SenderId, x.Id }).HasDatabaseName("ix_friend_requests_sender_id");
        b.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_friend_requests_sender");
        b.HasOne(x => x.Receiver).WithMany().HasForeignKey(x => x.ReceiverId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_friend_requests_receiver");
    }
}

