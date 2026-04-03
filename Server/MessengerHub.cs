using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MessengerServer.Data;
using MessengerServer.Models;
using MessengerServer.DTOs;

namespace MessengerServer.Hubs;

public class MessengerHub : Hub
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public MessengerHub(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId);
        if (user != null)
        {
            user.IsOnline = false;
            user.ConnectionId = null;
            await _db.SaveChangesAsync();
            await NotifyContactsStatusChange(user.Id, false);
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task<AuthResponse> Register(string username, string password, string displayName)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(displayName))
            return new AuthResponse(false, "All fields are required", 0, "", "");

        if (await _db.Users.AnyAsync(u => u.Username == username))
            return new AuthResponse(false, "Username already taken", 0, "", "");

        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            DisplayName = displayName,
            IsOnline = true,
            ConnectionId = Context.ConnectionId
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return new AuthResponse(true, "Registered successfully", user.Id, user.Username, user.DisplayName);
    }

    public async Task<AuthResponse> Login(string username, string password)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return new AuthResponse(false, "Invalid username or password", 0, "", "");

        user.IsOnline = true;
        user.ConnectionId = Context.ConnectionId;
        await _db.SaveChangesAsync();
        await NotifyContactsStatusChange(user.Id, true);
        return new AuthResponse(true, "Logged in successfully", user.Id, user.Username, user.DisplayName);
    }

    public async Task<List<ContactDto>> GetContacts(int userId)
    {
        var contacts = await _db.Contacts
            .Where(c => c.OwnerId == userId)
            .Include(c => c.ContactUser)
            .ToListAsync();

        return contacts.Select(c => new ContactDto(
            c.Id, c.ContactUserId, c.ContactUser.Username,
            c.ContactUser.DisplayName,
            string.IsNullOrEmpty(c.Nickname) ? c.ContactUser.DisplayName : c.Nickname,
            c.ContactUser.IsOnline
        )).ToList();
    }

    public async Task<List<InvitationDto>> GetPendingInvitations(int userId)
    {
        var invitations = await _db.ContactInvitations
            .Where(i => i.ReceiverId == userId && i.Status == InvitationStatus.Pending)
            .Include(i => i.Sender)
            .ToListAsync();

        return invitations.Select(i => new InvitationDto(
            i.Id, i.SenderId, i.Sender.Username, i.Sender.DisplayName, i.SentAt
        )).ToList();
    }

    public async Task<string> SendInvitation(int senderId, int targetUserId)
    {
        if (senderId == targetUserId) return "Cannot invite yourself";
        var target = await _db.Users.FindAsync(targetUserId);
        if (target == null) return "User not found";
        if (await _db.Contacts.AnyAsync(c => c.OwnerId == senderId && c.ContactUserId == targetUserId))
            return "Already in contacts";
        if (await _db.ContactInvitations.AnyAsync(i => i.SenderId == senderId && i.ReceiverId == targetUserId && i.Status == InvitationStatus.Pending))
            return "Invitation already sent";

        var invitation = new ContactInvitation { SenderId = senderId, ReceiverId = targetUserId };
        _db.ContactInvitations.Add(invitation);
        await _db.SaveChangesAsync();

        var sender = await _db.Users.FindAsync(senderId);
        if (target.ConnectionId != null)
            await Clients.Client(target.ConnectionId).SendAsync("InvitationReceived",
                new InvitationDto(invitation.Id, senderId, sender!.Username, sender.DisplayName, invitation.SentAt));

        return "Invitation sent";
    }

    public async Task<string> RespondToInvitation(int userId, int invitationId, bool accept)
    {
        var invitation = await _db.ContactInvitations
            .Include(i => i.Sender).Include(i => i.Receiver)
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.ReceiverId == userId);
        if (invitation == null) return "Invitation not found";

        invitation.Status = accept ? InvitationStatus.Accepted : InvitationStatus.Declined;
        if (accept)
        {
            _db.Contacts.Add(new Contact { OwnerId = userId, ContactUserId = invitation.SenderId, Nickname = invitation.Sender.DisplayName });
            _db.Contacts.Add(new Contact { OwnerId = invitation.SenderId, ContactUserId = userId, Nickname = invitation.Receiver.DisplayName });
        }
        await _db.SaveChangesAsync();

        if (accept && invitation.Sender.ConnectionId != null)
        {
            var newContact = new ContactDto(0, userId, invitation.Receiver.Username, invitation.Receiver.DisplayName, invitation.Receiver.DisplayName, invitation.Receiver.IsOnline);
            await Clients.Client(invitation.Sender.ConnectionId).SendAsync("InvitationAccepted", newContact);
        }
        return accept ? "Invitation accepted" : "Invitation declined";
    }

    public async Task<string> RenameContact(int ownerId, int contactId, string newNickname)
    {
        var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.Id == contactId && c.OwnerId == ownerId);
        if (contact == null) return "Contact not found";
        contact.Nickname = newNickname;
        await _db.SaveChangesAsync();
        return "Contact renamed";
    }

    public async Task<string> RemoveContact(int ownerId, int contactId)
    {
        var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.Id == contactId && c.OwnerId == ownerId);
        if (contact == null) return "Contact not found";
        _db.Contacts.Remove(contact);
        await _db.SaveChangesAsync();
        return "Contact removed";
    }

    public async Task<List<MessageDto>> GetMessages(int userId, int contactUserId)
    {
        var messages = await _db.Messages
            .Where(m => (m.SenderId == userId && m.ReceiverId == contactUserId) ||
                        (m.SenderId == contactUserId && m.ReceiverId == userId))
            .Include(m => m.Sender).OrderBy(m => m.SentAt).ToListAsync();

        var unread = messages.Where(m => m.ReceiverId == userId && !m.IsRead).ToList();
        foreach (var msg in unread) msg.IsRead = true;
        if (unread.Any()) await _db.SaveChangesAsync();
        return messages.Select(MapMessage).ToList();
    }

    public async Task<List<MessageDto>> GetGroupMessages(int groupId)
    {
        var messages = await _db.Messages
            .Where(m => m.GroupId == groupId)
            .Include(m => m.Sender).OrderBy(m => m.SentAt).ToListAsync();
        return messages.Select(MapMessage).ToList();
    }

    public async Task<string> SendMessage(int senderId, int receiverId, string content, string messageType, string? fileName, string? fileData)
    {
        var msgType = Enum.Parse<MessageType>(messageType, true);
        string? filePath = null; long? fileSize = null;
        if (msgType != MessageType.Text && fileData != null)
        {
            var result = SaveFile(fileName!, fileData);
            filePath = result.path; fileSize = result.size;
        }
        var message = new Message { SenderId = senderId, ReceiverId = receiverId, Type = msgType,
            Content = content, FileName = fileName, FilePath = filePath, FileSize = fileSize };
        _db.Messages.Add(message);
        await _db.SaveChangesAsync();
        await _db.Entry(message).Reference(m => m.Sender).LoadAsync();
        var dto = MapMessage(message);
        var receiver = await _db.Users.FindAsync(receiverId);
        if (receiver?.ConnectionId != null)
            await Clients.Client(receiver.ConnectionId).SendAsync("MessageReceived", dto);
        return "sent";
    }

    public async Task<string> SendGroupMessage(int senderId, int groupId, string content, string messageType, string? fileName, string? fileData)
    {
        var group = await _db.Groups.Include(g => g.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == groupId);
        if (group == null) return "Group not found";
        var msgType = Enum.Parse<MessageType>(messageType, true);
        string? filePath = null; long? fileSize = null;
        if (msgType != MessageType.Text && fileData != null)
        {
            var result = SaveFile(fileName!, fileData);
            filePath = result.path; fileSize = result.size;
        }
        var message = new Message { SenderId = senderId, GroupId = groupId, Type = msgType,
            Content = content, FileName = fileName, FilePath = filePath, FileSize = fileSize };
        _db.Messages.Add(message);
        await _db.SaveChangesAsync();
        await _db.Entry(message).Reference(m => m.Sender).LoadAsync();
        var dto = MapMessage(message);
        foreach (var member in group.Members.Where(m => m.UserId != senderId && m.User.ConnectionId != null))
            await Clients.Client(member.User.ConnectionId!).SendAsync("GroupMessageReceived", dto);
        return "sent";
    }

    public async Task<GroupDto?> CreateGroup(int creatorId, string name, List<int> memberIds)
    {
        var creator = await _db.Users.FindAsync(creatorId);
        if (creator == null) return null;
        var group = new Group { Name = name, CreatorId = creatorId };
        _db.Groups.Add(group);
        await _db.SaveChangesAsync();
        foreach (var memberId in memberIds.Union(new[] { creatorId }).Distinct())
            _db.GroupMembers.Add(new GroupMember { GroupId = group.Id, UserId = memberId, IsAdmin = memberId == creatorId });
        await _db.SaveChangesAsync();
        return await GetGroupDto(group.Id);
    }

    public async Task<List<GroupDto>> GetGroups(int userId)
    {
        var groupIds = await _db.GroupMembers.Where(gm => gm.UserId == userId).Select(gm => gm.GroupId).ToListAsync();
        var groups = new List<GroupDto>();
        foreach (var id in groupIds) { var dto = await GetGroupDto(id); if (dto != null) groups.Add(dto); }
        return groups;
    }

    public async Task<string> AddToGroup(int requesterId, int groupId, int userId)
    {
        var isAdmin = await _db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == requesterId && gm.IsAdmin);
        if (!isAdmin) return "Not authorized";
        if (await _db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId)) return "Already a member";
        _db.GroupMembers.Add(new GroupMember { GroupId = groupId, UserId = userId });
        await _db.SaveChangesAsync();
        return "Added to group";
    }

    public async Task<List<UserDto>> SearchUsers(string query, int requesterId)
    {
        var users = await _db.Users
            .Where(u => u.Id != requesterId && (u.Username.Contains(query) || u.DisplayName.Contains(query)))
            .Take(20).ToListAsync();
        return users.Select(u => new UserDto(u.Id, u.Username, u.DisplayName, u.IsOnline)).ToList();
    }

    public async Task<FileDownloadResponse?> DownloadFile(int messageId)
    {
        var message = await _db.Messages.FindAsync(messageId);
        if (message?.FilePath == null || !File.Exists(message.FilePath)) return null;
        var bytes = await File.ReadAllBytesAsync(message.FilePath);
        return new FileDownloadResponse(message.FileName!, Convert.ToBase64String(bytes), message.FileSize ?? 0);
    }

    private (string path, long size) SaveFile(string fileName, string base64Data)
    {
        var uploadsDir = Path.Combine(_env.ContentRootPath, "uploads");
        Directory.CreateDirectory(uploadsDir);
        var bytes = Convert.FromBase64String(base64Data);
        var path = Path.Combine(uploadsDir, $"{Guid.NewGuid()}_{fileName}");
        File.WriteAllBytes(path, bytes);
        return (path, bytes.Length);
    }

    private async Task NotifyContactsStatusChange(int userId, bool isOnline)
    {
        var contactOwners = await _db.Contacts.Where(c => c.ContactUserId == userId).Include(c => c.Owner).ToListAsync();
        foreach (var contact in contactOwners.Where(c => c.Owner.ConnectionId != null))
            await Clients.Client(contact.Owner.ConnectionId!).SendAsync("ContactStatusChanged", userId, isOnline);
    }

    private async Task<GroupDto?> GetGroupDto(int groupId)
    {
        var group = await _db.Groups.Include(g => g.Creator)
            .Include(g => g.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == groupId);
        if (group == null) return null;
        var members = group.Members.Select(m => new UserDto(m.UserId, m.User.Username, m.User.DisplayName, m.User.IsOnline)).ToList();
        return new GroupDto(group.Id, group.Name, group.CreatorId, group.Creator.DisplayName, members);
    }

    private static MessageDto MapMessage(Message m) => new(
        m.Id, m.SenderId, m.Sender?.DisplayName ?? "Unknown",
        m.ReceiverId, m.GroupId, m.Type.ToString(), m.Content,
        m.FileName, m.FileSize, m.SentAt, m.IsRead);
}