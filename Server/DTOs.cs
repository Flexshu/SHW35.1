namespace MessengerServer.DTOs;

public record RegisterRequest(string Username, string Password, string DisplayName);
public record LoginRequest(string Username, string Password);
public record AuthResponse(bool Success, string Message, int UserId, string Username, string DisplayName);

public record SendMessageRequest(int ReceiverId, string Content, string MessageType, string? FileName, string? FileData);
public record SendGroupMessageRequest(int GroupId, string Content, string MessageType, string? FileName, string? FileData);

public record ContactDto(int Id, int UserId, string Username, string DisplayName, string Nickname, bool IsOnline);
public record UserDto(int Id, string Username, string DisplayName, bool IsOnline);
public record MessageDto(int Id, int SenderId, string SenderName, int? ReceiverId, int? GroupId, string Type, string Content, string? FileName, long? FileSize, DateTime SentAt, bool IsRead);
public record GroupDto(int Id, string Name, int CreatorId, string CreatorName, List<UserDto> Members);
public record InvitationDto(int Id, int SenderId, string SenderName, string SenderDisplayName, DateTime SentAt);

public record CreateGroupRequest(string Name, List<int> MemberIds);
public record RenameContactRequest(int ContactId, string NewNickname);
public record InviteUserRequest(int TargetUserId);
public record RespondInvitationRequest(int InvitationId, bool Accept);
public record AddToGroupRequest(int GroupId, int UserId);

public record FileUploadRequest(string FileName, string FileData, long FileSize);
public record FileDownloadResponse(string FileName, string FileData, long FileSize);