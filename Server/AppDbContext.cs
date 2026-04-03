using Microsoft.EntityFrameworkCore;
using MessengerServer.Models;

namespace MessengerServer.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<ContactInvitation> ContactInvitations => Set<ContactInvitation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Contact>()
            .HasOne(c => c.Owner).WithMany(u => u.Contacts)
            .HasForeignKey(c => c.OwnerId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Contact>()
            .HasOne(c => c.ContactUser).WithMany(u => u.ContactOf)
            .HasForeignKey(c => c.ContactUserId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ContactInvitation>()
            .HasOne(i => i.Sender).WithMany(u => u.SentInvitations)
            .HasForeignKey(i => i.SenderId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ContactInvitation>()
            .HasOne(i => i.Receiver).WithMany(u => u.ReceivedInvitations)
            .HasForeignKey(i => i.ReceiverId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Sender).WithMany(u => u.SentMessages)
            .HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Receiver).WithMany(u => u.ReceivedMessages)
            .HasForeignKey(m => m.ReceiverId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Group).WithMany(g => g.Messages)
            .HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Group>()
            .HasOne(g => g.Creator).WithMany()
            .HasForeignKey(g => g.CreatorId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GroupMember>()
            .HasOne(gm => gm.Group).WithMany(g => g.Members)
            .HasForeignKey(gm => gm.GroupId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GroupMember>()
            .HasOne(gm => gm.User).WithMany(u => u.GroupMemberships)
            .HasForeignKey(gm => gm.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}