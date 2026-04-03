namespace MessengerClient.Models;

public record AuthResponse(bool Success, string Message, int UserId, string Username, string DisplayName);
public record ContactDto(int Id, int UserId, string Username, string DisplayName, string Nickname, bool IsOnline);
public record UserDto(int Id, string Username, string DisplayName, bool IsOnline);
public record MessageDto(int Id, int SenderId, string SenderName, int? ReceiverId, int? GroupId, string Type, string Content, string? FileName, long? FileSize, DateTime SentAt, bool IsRead);
public record GroupDto(int Id, string Name, int CreatorId, string CreatorName, List<UserDto> Members);
public record InvitationDto(int Id, int SenderId, string SenderName, string SenderDisplayName, DateTime SentAt);
public record FileDownloadResponse(string FileName, string FileData, long FileSize);

public class AppState
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsLoggedIn { get; set; }
    public List<ContactDto> Contacts { get; set; } = new();
    public List<GroupDto> Groups { get; set; } = new();
    public List<InvitationDto> PendingInvitations { get; set; } = new();
    public ChatTarget? CurrentChat { get; set; }
    public List<MessageDto> CurrentMessages { get; set; } = new();
}

public class ChatTarget
{
    public ChatType Type { get; set; }
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public enum ChatType { Direct, Group }