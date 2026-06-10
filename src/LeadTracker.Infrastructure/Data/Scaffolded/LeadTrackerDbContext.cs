using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace LeadTracker.Infrastructure.Data.Scaffolded;

public partial class LeadTrackerDbContext : DbContext
{
    public LeadTrackerDbContext(DbContextOptions<LeadTrackerDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Business> Businesses { get; set; }

    public virtual DbSet<BusinessMembership> BusinessMemberships { get; set; }

    public virtual DbSet<BusinessWhatsappNumber> BusinessWhatsappNumbers { get; set; }

    public virtual DbSet<Conversation> Conversations { get; set; }

    public virtual DbSet<Lead> Leads { get; set; }

    public virtual DbSet<MessageBuffer> MessageBuffers { get; set; }

    public virtual DbSet<Property> Properties { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("auth", "aal_level", new[] { "aal1", "aal2", "aal3" })
            .HasPostgresEnum("auth", "code_challenge_method", new[] { "s256", "plain" })
            .HasPostgresEnum("auth", "factor_status", new[] { "unverified", "verified" })
            .HasPostgresEnum("auth", "factor_type", new[] { "totp", "webauthn", "phone" })
            .HasPostgresEnum("auth", "oauth_authorization_status", new[] { "pending", "approved", "denied", "expired" })
            .HasPostgresEnum("auth", "oauth_client_type", new[] { "public", "confidential" })
            .HasPostgresEnum("auth", "oauth_registration_type", new[] { "dynamic", "manual" })
            .HasPostgresEnum("auth", "oauth_response_type", new[] { "code" })
            .HasPostgresEnum("auth", "one_time_token_type", new[] { "confirmation_token", "reauthentication_token", "recovery_token", "email_change_token_new", "email_change_token_current", "phone_change_token" })
            .HasPostgresEnum("realtime", "action", new[] { "INSERT", "UPDATE", "DELETE", "TRUNCATE", "ERROR" })
            .HasPostgresEnum("realtime", "equality_op", new[] { "eq", "neq", "lt", "lte", "gt", "gte", "in" })
            .HasPostgresEnum("storage", "buckettype", new[] { "STANDARD", "ANALYTICS", "VECTOR" })
            .HasPostgresExtension("extensions", "pg_stat_statements")
            .HasPostgresExtension("extensions", "pgcrypto")
            .HasPostgresExtension("extensions", "uuid-ossp")
            .HasPostgresExtension("pg_trgm")
            .HasPostgresExtension("vault", "supabase_vault");

        modelBuilder.Entity<Business>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("businesses_pkey");

            entity.ToTable("businesses");

            entity.HasIndex(e => e.Slug, "businesses_slug_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Active)
                .HasDefaultValue(true)
                .HasColumnName("active");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CrmAccount).HasColumnName("crm_account");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Slug).HasColumnName("slug");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.WhatsappNumber).HasColumnName("whatsapp_number");
        });

        modelBuilder.Entity<BusinessMembership>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("business_memberships_pkey");

            entity.ToTable("business_memberships");

            entity.HasIndex(e => new { e.BusinessId, e.UserId }, "business_memberships_business_id_user_id_key").IsUnique();

            entity.HasIndex(e => e.BusinessId, "idx_memberships_business");

            entity.HasIndex(e => e.UserId, "idx_memberships_user");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.BusinessId).HasColumnName("business_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Role)
                .HasDefaultValueSql("'member'::text")
                .HasColumnName("role");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Business).WithMany(p => p.BusinessMemberships)
                .HasForeignKey(d => d.BusinessId)
                .HasConstraintName("business_memberships_business_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.BusinessMemberships)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("business_memberships_user_id_fkey");
        });

        modelBuilder.Entity<BusinessWhatsappNumber>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("business_whatsapp_numbers_pkey");

            entity.ToTable("business_whatsapp_numbers");

            entity.HasIndex(e => e.WhatsappNumber, "business_whatsapp_numbers_whatsapp_number_key").IsUnique();

            entity.HasIndex(e => e.BusinessId, "idx_business_whatsapp_numbers_business");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Active)
                .HasDefaultValue(true)
                .HasColumnName("active");
            entity.Property(e => e.BusinessId).HasColumnName("business_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Label).HasColumnName("label");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.WhatsappNumber).HasColumnName("whatsapp_number");

            entity.HasOne(d => d.Business).WithMany(p => p.BusinessWhatsappNumbers)
                .HasForeignKey(d => d.BusinessId)
                .HasConstraintName("business_whatsapp_numbers_business_id_fkey");
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("conversations_pkey");

            entity.ToTable("conversations");

            entity.HasIndex(e => new { e.BusinessId, e.Phone }, "conversations_business_phone_key").IsUnique();

            entity.HasIndex(e => new { e.WhatsappNumberId, e.Phone }, "conversations_whatsapp_number_id_phone_key").IsUnique();

            entity.HasIndex(e => new { e.BusinessId, e.Phone }, "idx_conversations_biz_phone");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.BusinessId).HasColumnName("business_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.History)
                .HasDefaultValueSql("'[]'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("history");
            entity.Property(e => e.Phone).HasColumnName("phone");
            entity.Property(e => e.ProfileName).HasColumnName("profile_name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.WhatsappNumberId).HasColumnName("whatsapp_number_id");

            entity.HasOne(d => d.Business).WithMany(p => p.Conversations)
                .HasForeignKey(d => d.BusinessId)
                .HasConstraintName("conversations_business_id_fkey");

            entity.HasOne(d => d.WhatsappNumber).WithMany(p => p.Conversations)
                .HasForeignKey(d => d.WhatsappNumberId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("conversations_whatsapp_number_id_fkey");
        });

        modelBuilder.Entity<Lead>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("leads_pkey");

            entity.ToTable("leads");

            entity.HasIndex(e => new { e.BusinessId, e.Classification }, "idx_leads_biz_class");

            entity.HasIndex(e => new { e.BusinessId, e.ReadyForHandoff, e.HandoffDone }, "idx_leads_biz_handoff");

            entity.HasIndex(e => new { e.BusinessId, e.Phone }, "leads_business_phone_key").IsUnique();

            entity.HasIndex(e => new { e.WhatsappNumberId, e.Phone }, "leads_whatsapp_number_id_phone_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Bedrooms).HasColumnName("bedrooms");
            entity.Property(e => e.Budget).HasColumnName("budget");
            entity.Property(e => e.BudgetAmount).HasColumnName("budget_amount");
            entity.Property(e => e.BusinessId).HasColumnName("business_id");
            entity.Property(e => e.Classification)
                .HasDefaultValueSql("'frio'::text")
                .HasColumnName("classification");
            entity.Property(e => e.ConversationId).HasColumnName("conversation_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Financing).HasColumnName("financing");
            entity.Property(e => e.HandoffDone)
                .HasDefaultValue(false)
                .HasColumnName("handoff_done");
            entity.Property(e => e.LastMessage).HasColumnName("last_message");
            entity.Property(e => e.LastReply).HasColumnName("last_reply");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Operation).HasColumnName("operation");
            entity.Property(e => e.Phone).HasColumnName("phone");
            entity.Property(e => e.ProfileName).HasColumnName("profile_name");
            entity.Property(e => e.PropertyOfInterest).HasColumnName("property_of_interest");
            entity.Property(e => e.ReadyForHandoff)
                .HasDefaultValue(false)
                .HasColumnName("ready_for_handoff");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.Timeline).HasColumnName("timeline");
            entity.Property(e => e.Type).HasColumnName("type");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.WhatsappNumberId).HasColumnName("whatsapp_number_id");
            entity.Property(e => e.Zone).HasColumnName("zone");

            entity.HasOne(d => d.Business).WithMany(p => p.Leads)
                .HasForeignKey(d => d.BusinessId)
                .HasConstraintName("leads_business_id_fkey");

            entity.HasOne(d => d.Conversation).WithMany(p => p.Leads)
                .HasForeignKey(d => d.ConversationId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("leads_conversation_id_fkey");

            entity.HasOne(d => d.WhatsappNumber).WithMany(p => p.Leads)
                .HasForeignKey(d => d.WhatsappNumberId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("leads_whatsapp_number_id_fkey");
        });

        modelBuilder.Entity<MessageBuffer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("message_buffer_pkey");

            entity.ToTable("message_buffer");

            entity.HasIndex(e => new { e.BusinessId, e.Phone, e.Processed, e.CreatedAt }, "message_buffer_lookup_idx");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.BusinessId).HasColumnName("business_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.MessageSid).HasColumnName("message_sid");
            entity.Property(e => e.Phone).HasColumnName("phone");
            entity.Property(e => e.Processed)
                .HasDefaultValue(false)
                .HasColumnName("processed");

            entity.HasOne(d => d.Business).WithMany(p => p.MessageBuffers)
                .HasForeignKey(d => d.BusinessId)
                .HasConstraintName("message_buffer_business_id_fkey");
        });

        modelBuilder.Entity<Property>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("properties_pkey");

            entity.ToTable("properties");

            entity.HasIndex(e => new { e.BusinessId, e.Bedrooms }, "idx_properties_biz_beds");

            entity.HasIndex(e => new { e.BusinessId, e.Operation }, "idx_properties_biz_op");

            entity.HasIndex(e => new { e.BusinessId, e.Price }, "idx_properties_biz_price");

            entity.HasIndex(e => e.Type, "idx_properties_type_trgm")
                .HasMethod("gin")
                .HasOperators(new[] { "gin_trgm_ops" });

            entity.HasIndex(e => e.Zone, "idx_properties_zone_trgm")
                .HasMethod("gin")
                .HasOperators(new[] { "gin_trgm_ops" });

            entity.HasIndex(e => new { e.BusinessId, e.Ref }, "properties_business_id_ref_key").IsUnique();

            entity.HasIndex(e => new { e.BusinessId, e.Ref }, "properties_business_ref_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Bathrooms).HasColumnName("bathrooms");
            entity.Property(e => e.Bedrooms).HasColumnName("bedrooms");
            entity.Property(e => e.BusinessId).HasColumnName("business_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Currency)
                .HasDefaultValueSql("'USD'::text")
                .HasColumnName("currency");
            entity.Property(e => e.ExpensesAmount).HasColumnName("expenses_amount");
            entity.Property(e => e.ExpensesLabel).HasColumnName("expenses_label");
            entity.Property(e => e.Extra).HasColumnName("extra");
            entity.Property(e => e.IsSeasonal)
                .HasDefaultValue(false)
                .HasColumnName("is_seasonal");
            entity.Property(e => e.M2).HasColumnName("m2");
            entity.Property(e => e.Operation).HasColumnName("operation");
            entity.Property(e => e.PhotoUrl).HasColumnName("photo_url");
            entity.Property(e => e.Price).HasColumnName("price");
            entity.Property(e => e.PriceDay).HasColumnName("price_day");
            entity.Property(e => e.PriceFortnight).HasColumnName("price_fortnight");
            entity.Property(e => e.PriceLabel).HasColumnName("price_label");
            entity.Property(e => e.PriceWeek).HasColumnName("price_week");
            entity.Property(e => e.Ref).HasColumnName("ref");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.Title).HasColumnName("title");
            entity.Property(e => e.Type).HasColumnName("type");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.Url).HasColumnName("url");
            entity.Property(e => e.Zone).HasColumnName("zone");

            entity.HasOne(d => d.Business).WithMany(p => p.Properties)
                .HasForeignKey(d => d.BusinessId)
                .HasConstraintName("properties_business_id_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.FullName).HasColumnName("full_name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
