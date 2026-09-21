using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocialSystem.Api.Models.Entities;

namespace SocialSystem.Api.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users", t =>
        {
            t.HasCheckConstraint("ck_users_username", "REGEXP_LIKE(username, '^[A-Za-z0-9_]{3,32}$', 'c')");
            t.HasCheckConstraint("ck_users_password_hash", "CHAR_LENGTH(TRIM(password_hash)) > 0");
            t.HasCheckConstraint("ck_users_nickname", "CHAR_LENGTH(TRIM(nickname)) > 0");
            t.HasCheckConstraint("ck_users_avatar_key", "CHAR_LENGTH(TRIM(avatar_key)) > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint").ValueGeneratedOnAdd();
        b.Property(x => x.Username).HasColumnName("username").HasColumnType("varchar(32)").HasMaxLength(32).IsRequired();
        b.Property(x => x.PasswordHash).HasColumnName("password_hash").HasColumnType("varchar(512)").HasMaxLength(512).IsRequired();
        b.Property(x => x.Nickname).HasColumnName("nickname").HasColumnType("varchar(32)").HasMaxLength(32).IsRequired();
        b.Property(x => x.Bio).HasColumnName("bio").HasColumnType("varchar(200)").HasMaxLength(200).IsRequired();
        b.Property(x => x.AvatarKey).HasColumnName("avatar_key").HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)");
        b.HasIndex(x => x.Username).IsUnique().HasDatabaseName("uq_users_username");
        b.HasIndex(x => x.Nickname).HasDatabaseName("ix_users_nickname");
        b.Property(x => x.Username).HasCharSet("ascii").UseCollation("ascii_general_ci");
        b.Property(x => x.PasswordHash).HasCharSet("ascii").UseCollation("ascii_bin");
        b.Property(x => x.Bio).HasDefaultValue("");
        b.Property(x => x.AvatarKey).HasDefaultValue("default");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)").ValueGeneratedOnAddOrUpdate();
        b.Property(x => x.UpdatedAt).Metadata.SetBeforeSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Ignore);
        b.Property(x => x.UpdatedAt).Metadata.SetAfterSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Ignore);
    }
}

