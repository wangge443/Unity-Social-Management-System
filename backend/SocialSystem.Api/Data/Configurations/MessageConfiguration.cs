using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocialSystem.Api.Models.Entities;

namespace SocialSystem.Api.Data.Configurations;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> b)
    {
        b.ToTable("messages", t =>
        {
            t.HasCheckConstraint("ck_messages_different_users", "sender_id <> receiver_id");
            t.HasCheckConstraint("ck_messages_content", "CHAR_LENGTH(TRIM(content)) > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint").ValueGeneratedOnAdd();
        b.Property(x => x.SenderId).HasColumnName("sender_id").HasColumnType("bigint");
        b.Property(x => x.ReceiverId).HasColumnName("receiver_id").HasColumnType("bigint");
        b.Property(x => x.Content).HasColumnName("content").HasColumnType("varchar(2000)").HasMaxLength(2000).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        b.HasIndex(x => new { x.SenderId, x.ReceiverId, x.Id }).HasDatabaseName("ix_messages_sender_receiver_id");
        b.HasIndex(x => new { x.ReceiverId, x.SenderId, x.Id }).HasDatabaseName("ix_messages_receiver_sender_id");
        b.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_messages_sender");
        b.HasOne(x => x.Receiver).WithMany().HasForeignKey(x => x.ReceiverId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_messages_receiver");
    }
}

