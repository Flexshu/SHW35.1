using Microsoft.AspNetCore.SignalR.Client;
using MessengerClient.Models;

namespace MessengerClient.Services;

public class HubService
{
    private HubConnection? _connection;
    private const string ServerUrl = "http://localhost:5000/hub";

    public event Action<MessageDto>? MessageReceived;
    public event Action<MessageDto>? GroupMessageReceived;
    public event Action<InvitationDto>? InvitationReceived;
    public event Action<ContactDto>? InvitationAccepted;
    public event Action<int, bool>? ContactStatusChanged;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task ConnectAsync()
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(ServerUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.On<MessageDto>("MessageReceived", dto => MessageReceived?.Invoke(dto));
        _connection.On<MessageDto>("GroupMessageReceived", dto => GroupMessageReceived?.Invoke(dto));
        _connection.On<InvitationDto>("InvitationReceived", dto => InvitationReceived?.Invoke(dto));
        _connection.On<ContactDto>("InvitationAccepted", dto => InvitationAccepted?.Invoke(dto));
        _connection.On<int, bool>("ContactStatusChanged", (id, online) => ContactStatusChanged?.Invoke(id, online));

        await _connection.StartAsync();
    }

    public async Task<AuthResponse> RegisterAsync(string username, string password, string displayName)
        => await _connection!.InvokeAsync<AuthResponse>("Register", username, password, displayName);

    public async Task<AuthResponse> LoginAsync(string username, string password)
        => await _connection!.InvokeAsync<AuthResponse>("Login", username, password);

    public async Task<List<ContactDto>> GetContactsAsync(int userId)
        => await _connection!.InvokeAsync<List<ContactDto>>("GetContacts", userId);

    public async Task<List<InvitationDto>> GetPendingInvitationsAsync(int userId)
        => await _connection!.InvokeAsync<List<InvitationDto>>("GetPendingInvitations", userId);

    public async Task<string> SendInvitationAsync(int senderId, int targetUserId)
        => await _connection!.InvokeAsync<string>("SendInvitation", senderId, targetUserId);

    public async Task<string> RespondToInvitationAsync(int userId, int invitationId, bool accept)
        => await _connection!.InvokeAsync<string>("RespondToInvitation", userId, invitationId, accept);

    public async Task<string> RenameContactAsync(int ownerId, int contactId, string newNickname)
        => await _connection!.InvokeAsync<string>("RenameContact", ownerId, contactId, newNickname);

    public async Task<string> RemoveContactAsync(int ownerId, int contactId)
        => await _connection!.InvokeAsync<string>("RemoveContact", ownerId, contactId);

    public async Task<List<MessageDto>> GetMessagesAsync(int userId, int contactUserId)
        => await _connection!.InvokeAsync<List<MessageDto>>("GetMessages", userId, contactUserId);

    public async Task<List<MessageDto>> GetGroupMessagesAsync(int groupId)
        => await _connection!.InvokeAsync<List<MessageDto>>("GetGroupMessages", groupId);

    public async Task<string> SendMessageAsync(int senderId, int receiverId, string content,
        string messageType = "Text", string? fileName = null, string? fileData = null)
        => await _connection!.InvokeAsync<string>("SendMessage", senderId, receiverId, content, messageType, fileName, fileData);

    public async Task<string> SendGroupMessageAsync(int senderId, int groupId, string content,
        string messageType = "Text", string? fileName = null, string? fileData = null)
        => await _connection!.InvokeAsync<string>("SendGroupMessage", senderId, groupId, content, messageType, fileName, fileData);

    public async Task<GroupDto?> CreateGroupAsync(int creatorId, string name, List<int> memberIds)
        => await _connection!.InvokeAsync<GroupDto?>("CreateGroup", creatorId, name, memberIds);

    public async Task<List<GroupDto>> GetGroupsAsync(int userId)
        => await _connection!.InvokeAsync<List<GroupDto>>("GetGroups", userId);

    public async Task<string> AddToGroupAsync(int requesterId, int groupId, int userId)
        => await _connection!.InvokeAsync<string>("AddToGroup", requesterId, groupId, userId);

    public async Task<List<UserDto>> SearchUsersAsync(string query, int requesterId)
        => await _connection!.InvokeAsync<List<UserDto>>("SearchUsers", query, requesterId);

    public async Task<FileDownloadResponse?> DownloadFileAsync(int messageId)
        => await _connection!.InvokeAsync<FileDownloadResponse?>("DownloadFile", messageId);

    public async Task DisconnectAsync()
    {
        if (_connection != null) await _connection.StopAsync();
    }
}