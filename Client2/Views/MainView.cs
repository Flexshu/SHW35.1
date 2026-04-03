using Terminal.Gui;
using MessengerClient.Models;
using MessengerClient.Services;

namespace MessengerClient.Views;

public class MainView : Window
{
    private readonly HubService _hub;
    private readonly AppState _state;
    private ListView _sidebarList = null!;
    private TextView _chatArea = null!;
    private TextField _inputField = null!;
    private Label _chatTitleLabel = null!;
    private FrameView _chatFrame = null!;
    private List<SidebarItem> _sidebarItems = new();
    private int _selectedSidebarIndex = -1;

    public MainView(HubService hub, AppState state) : base("Messenger")
    {
        _hub = hub; _state = state;
        X = 0; Y = 0; Width = Dim.Fill(); Height = Dim.Fill();
        RegisterHubEvents();
        BuildUI();
        _ = LoadInitialDataAsync();
    }

    private void RegisterHubEvents()
    {
        _hub.MessageReceived += OnMessageReceived;
        _hub.GroupMessageReceived += OnGroupMessageReceived;
        _hub.InvitationReceived += OnInvitationReceived;
        _hub.InvitationAccepted += OnInvitationAccepted;
        _hub.ContactStatusChanged += OnContactStatusChanged;
    }

    private void BuildUI()
    {
        var menuBar = new MenuBar(new MenuBarItem[]
        {
            new("_File", new MenuItem[]
            {
                new("_Search Users", "", async () => await ShowSearchUsers()),
                new("_Invitations", "", async () => await ShowInvitations()),
                new("_Create Group", "", async () => await ShowCreateGroup()),
                new("_Quit", "", () => Application.RequestStop())
            }),
            new("_Contacts", new MenuItem[]
            {
                new("_Rename Contact", "", async () => await RenameSelectedContact()),
                new("_Remove Contact", "", async () => await RemoveSelectedContact()),
                new("_Add to Group", "", async () => await AddContactToGroup())
            })
        });

        var sidebarFrame = new FrameView($"Chats — {_state.DisplayName}")
        { X = 0, Y = 1, Width = 28, Height = Dim.Fill(2) };

        _sidebarList = new ListView() { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), AllowsMarking = false };
        _sidebarList.OpenSelectedItem += async (args) => await OpenSelectedChat(args.Value.ToString() ?? "");
        _sidebarList.SelectedItemChanged += (args) => _selectedSidebarIndex = args.Item;
        sidebarFrame.Add(_sidebarList);

        _chatFrame = new FrameView("Select a chat") { X = 28, Y = 1, Width = Dim.Fill(), Height = Dim.Fill(2) };
        _chatTitleLabel = new Label("No chat selected") { X = 1, Y = 0 };
        _chatArea = new TextView() { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill(3), ReadOnly = true, WordWrap = true };

        var inputFrame = new FrameView() { X = 0, Y = Pos.Bottom(_chatArea), Width = Dim.Fill(), Height = 3 };
        _inputField = new TextField("") { X = 0, Y = 0, Width = Dim.Fill(16), Height = 1 };
        var sendButton = new Button("Send [F5]") { X = Pos.Right(_inputField), Y = 0 };
        sendButton.Clicked += async () => await SendMessage();
        var fileButton = new Button("File [F6]") { X = Pos.Right(sendButton), Y = 0 };
        fileButton.Clicked += async () => await SendFile();
        inputFrame.Add(_inputField, sendButton, fileButton);

        _chatFrame.Add(_chatTitleLabel, _chatArea, inputFrame);
        var statusBar = new Label($" {_state.DisplayName} | F5: Send | F6: Send File | Enter: Open Chat")
        { X = 0, Y = Pos.AnchorEnd(1), Width = Dim.Fill() };

        Add(menuBar, sidebarFrame, _chatFrame, statusBar);
        KeyPress += (args) =>
        {
            if (args.KeyEvent.Key == Key.F5) { args.Handled = true; _ = SendMessage(); }
            if (args.KeyEvent.Key == Key.F6) { args.Handled = true; _ = SendFile(); }
        };
        _inputField.SetFocus();
    }

    private async Task LoadInitialDataAsync()
    {
        _state.Contacts = await _hub.GetContactsAsync(_state.UserId);
        _state.Groups = await _hub.GetGroupsAsync(_state.UserId);
        _state.PendingInvitations = await _hub.GetPendingInvitationsAsync(_state.UserId);
        RefreshSidebar();
        if (_state.PendingInvitations.Any()) ShowNotification($"You have {_state.PendingInvitations.Count} pending invitation(s)!");
    }

    private void RefreshSidebar()
    {
        Application.MainLoop.Invoke(() =>
        {
            _sidebarItems.Clear();
            var items = new List<string>();
            if (_state.Contacts.Any())
            {
                items.Add("── Contacts ──"); _sidebarItems.Add(new SidebarItem { IsHeader = true });
                foreach (var c in _state.Contacts)
                {
                    items.Add($"  {(c.IsOnline ? "●" : "○")} {c.Nickname}");
                    _sidebarItems.Add(new SidebarItem { IsGroup = false, Id = c.UserId, Name = c.Nickname, ContactId = c.Id });
                }
            }
            if (_state.Groups.Any())
            {
                items.Add("── Groups ──"); _sidebarItems.Add(new SidebarItem { IsHeader = true });
                foreach (var g in _state.Groups)
                {
                    items.Add($"  # {g.Name}");
                    _sidebarItems.Add(new SidebarItem { IsGroup = true, Id = g.Id, Name = g.Name });
                }
            }
            _sidebarList.SetSource(items);
            Application.Refresh();
        });
    }

    private async Task OpenSelectedChat(string _)
    {
        if (_selectedSidebarIndex < 0 || _selectedSidebarIndex >= _sidebarItems.Count) return;
        var item = _sidebarItems[_selectedSidebarIndex];
        if (item.IsHeader) return;
        _state.CurrentChat = new ChatTarget { Type = item.IsGroup ? ChatType.Group : ChatType.Direct, Id = item.Id, Name = item.Name };
        _state.CurrentMessages = item.IsGroup
            ? await _hub.GetGroupMessagesAsync(item.Id)
            : await _hub.GetMessagesAsync(_state.UserId, item.Id);
        RefreshChatArea();
    }

    private void RefreshChatArea()
    {
        Application.MainLoop.Invoke(() =>
        {
            if (_state.CurrentChat == null) return;
            _chatFrame.Title = _state.CurrentChat.Name;
            _chatTitleLabel.Text = _state.CurrentChat.Type == ChatType.Group
                ? $"Group: {_state.CurrentChat.Name}" : $"Chat with: {_state.CurrentChat.Name}";
            var sb = new System.Text.StringBuilder();
            foreach (var msg in _state.CurrentMessages)
            {
                var time = msg.SentAt.ToLocalTime().ToString("HH:mm");
                var sender = msg.SenderId == _state.UserId ? "You" : msg.SenderName;
                sb.AppendLine(msg.Type == "Text"
                    ? $"[{time}] {sender}: {msg.Content}"
                    : $"[{time}] {sender}: [file] {msg.FileName} ({FormatSize(msg.FileSize ?? 0)}) ID:{msg.Id}");
            }
            _chatArea.Text = sb.ToString();
            _chatArea.MoveEnd();
            Application.Refresh();
        });
    }

    private async Task SendMessage()
    {
        if (_state.CurrentChat == null) { ShowNotification("Select a chat first"); return; }
        var text = _inputField.Text.ToString()?.Trim() ?? "";
        if (string.IsNullOrEmpty(text)) return;
        if (_state.CurrentChat.Type == ChatType.Direct)
            await _hub.SendMessageAsync(_state.UserId, _state.CurrentChat.Id, text);
        else
            await _hub.SendGroupMessageAsync(_state.UserId, _state.CurrentChat.Id, text);
        _state.CurrentMessages.Add(new MessageDto(0, _state.UserId, "You",
            _state.CurrentChat.Type == ChatType.Direct ? _state.CurrentChat.Id : null,
            _state.CurrentChat.Type == ChatType.Group ? _state.CurrentChat.Id : null,
            "Text", text, null, null, DateTime.Now, true));
        _inputField.Text = "";
        RefreshChatArea();
    }

    private async Task SendFile()
    {
        if (_state.CurrentChat == null) { ShowNotification("Select a chat first"); return; }
        var dialog = new OpenDialog("Send File", "Choose a file to send");
        Application.Run(dialog);
        if (dialog.FilePath == null) return;
        var filePath = dialog.FilePath.ToString()!;
        if (!File.Exists(filePath)) { ShowNotification("File not found"); return; }
        var info = new FileInfo(filePath);
        if (info.Length > 50 * 1024 * 1024) { ShowNotification("File too large (max 50 MB)"); return; }
        var bytes = await File.ReadAllBytesAsync(filePath);
        var base64 = Convert.ToBase64String(bytes);
        var fileName = Path.GetFileName(filePath);
        var ext = Path.GetExtension(filePath).ToLower();
        var msgType = ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" ? "Image" : "File";
        if (_state.CurrentChat.Type == ChatType.Direct)
            await _hub.SendMessageAsync(_state.UserId, _state.CurrentChat.Id, fileName, msgType, fileName, base64);
        else
            await _hub.SendGroupMessageAsync(_state.UserId, _state.CurrentChat.Id, fileName, msgType, fileName, base64);
        _state.CurrentMessages.Add(new MessageDto(0, _state.UserId, "You",
            _state.CurrentChat.Type == ChatType.Direct ? _state.CurrentChat.Id : null,
            _state.CurrentChat.Type == ChatType.Group ? _state.CurrentChat.Id : null,
            msgType, fileName, fileName, info.Length, DateTime.Now, true));
        RefreshChatArea();
        ShowNotification($"File '{fileName}' sent!");
    }

    private async Task ShowSearchUsers()
    {
        var dialog = new Dialog("Search Users", 60, 16);
        var searchField = new TextField("") { X = 2, Y = 1, Width = Dim.Fill(2) };
        var resultsList = new ListView() { X = 2, Y = 3, Width = Dim.Fill(2), Height = 6 };
        var searchBtn = new Button("Search") { X = Pos.Center(), Y = 2 };
        var inviteBtn = new Button("Send Invitation") { X = Pos.Center(), Y = 10 };
        var closeBtn = new Button("Close") { X = Pos.Center(), Y = 12 };
        List<UserDto> foundUsers = new();
        searchBtn.Clicked += async () =>
        {
            var query = searchField.Text.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(query)) return;
            foundUsers = await _hub.SearchUsersAsync(query, _state.UserId);
            resultsList.SetSource(foundUsers.Select(u => $"{u.DisplayName} (@{u.Username}) {(u.IsOnline ? "●" : "○")}").ToList());
        };
        inviteBtn.Clicked += async () =>
        {
            if (resultsList.SelectedItem < 0 || resultsList.SelectedItem >= foundUsers.Count) return;
            var result = await _hub.SendInvitationAsync(_state.UserId, foundUsers[resultsList.SelectedItem].Id);
            ShowNotification(result);
        };
        closeBtn.Clicked += () => Application.RequestStop();
        dialog.Add(new Label("Search by username or name:") { X = 2, Y = 0 }, searchField, searchBtn, resultsList, inviteBtn, closeBtn);
        Application.Run(dialog);
    }

    private async Task ShowInvitations()
    {
        _state.PendingInvitations = await _hub.GetPendingInvitationsAsync(_state.UserId);
        if (!_state.PendingInvitations.Any()) { ShowNotification("No pending invitations"); return; }
        var dialog = new Dialog("Pending Invitations", 60, 16);
        var list = new ListView() { X = 2, Y = 1, Width = Dim.Fill(2), Height = 8 };
        list.SetSource(_state.PendingInvitations.Select(i => $"{i.SenderDisplayName} (@{i.SenderName})").ToList());
        var acceptBtn = new Button("Accept") { X = 5, Y = 10 };
        var declineBtn = new Button("Decline") { X = 16, Y = 10 };
        var closeBtn = new Button("Close") { X = Pos.Center(), Y = 12 };
        acceptBtn.Clicked += async () =>
        {
            if (list.SelectedItem < 0) return;
            var inv = _state.PendingInvitations[list.SelectedItem];
            ShowNotification(await _hub.RespondToInvitationAsync(_state.UserId, inv.Id, true));
            _state.PendingInvitations.RemoveAt(list.SelectedItem);
            list.SetSource(_state.PendingInvitations.Select(i => $"{i.SenderDisplayName} (@{i.SenderName})").ToList());
            _state.Contacts = await _hub.GetContactsAsync(_state.UserId);
            RefreshSidebar();
        };
        declineBtn.Clicked += async () =>
        {
            if (list.SelectedItem < 0) return;
            var inv = _state.PendingInvitations[list.SelectedItem];
            ShowNotification(await _hub.RespondToInvitationAsync(_state.UserId, inv.Id, false));
            _state.PendingInvitations.RemoveAt(list.SelectedItem);
            list.SetSource(_state.PendingInvitations.Select(i => $"{i.SenderDisplayName} (@{i.SenderName})").ToList());
        };
        closeBtn.Clicked += () => Application.RequestStop();
        dialog.Add(new Label("Invitations:") { X = 2, Y = 0 }, list, acceptBtn, declineBtn, closeBtn);
        Application.Run(dialog);
    }

    private async Task ShowCreateGroup()
    {
        if (!_state.Contacts.Any()) { ShowNotification("Add contacts first"); return; }
        var dialog = new Dialog("Create Group", 60, 20);
        var nameField = new TextField("") { X = 2, Y = 2, Width = Dim.Fill(2) };
        var memberList = new ListView() { X = 2, Y = 5, Width = Dim.Fill(2), Height = 8, AllowsMarking = true };
        memberList.SetSource(_state.Contacts.Select(c => c.Nickname).ToList());
        var createBtn = new Button("Create") { X = Pos.Center(), Y = 14 };
        var closeBtn = new Button("Close") { X = Pos.Center(), Y = 16 };
        createBtn.Clicked += async () =>
        {
            var name = nameField.Text.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(name)) { ShowNotification("Enter group name"); return; }
            var memberIds = Enumerable.Range(0, _state.Contacts.Count)
                .Where(i => memberList.Source.IsMarked(i)).Select(i => _state.Contacts[i].UserId).ToList();
            if (!memberIds.Any()) { ShowNotification("Select at least one member"); return; }
            var group = await _hub.CreateGroupAsync(_state.UserId, name, memberIds);
            if (group != null) { _state.Groups.Add(group); RefreshSidebar(); ShowNotification($"Group '{name}' created!"); }
            Application.RequestStop();
        };
        closeBtn.Clicked += () => Application.RequestStop();
        dialog.Add(new Label("Group Name:") { X = 2, Y = 1 }, nameField,
            new Label("Select Members (Space to mark):") { X = 2, Y = 4 }, memberList, createBtn, closeBtn);
        Application.Run(dialog);
    }

    private async Task RenameSelectedContact()
    {
        if (_selectedSidebarIndex < 0 || _selectedSidebarIndex >= _sidebarItems.Count) return;
        var item = _sidebarItems[_selectedSidebarIndex];
        if (item.IsHeader || item.IsGroup) return;
        var dialog = new Dialog("Rename Contact", 50, 10);
        var field = new TextField(item.Name) { X = 2, Y = 2, Width = Dim.Fill(2) };
        var okBtn = new Button("OK") { X = Pos.Center(), Y = 5 };
        var cancelBtn = new Button("Cancel") { X = Pos.Center(), Y = 7 };
        okBtn.Clicked += async () =>
        {
            var newName = field.Text.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(newName)) return;
            ShowNotification(await _hub.RenameContactAsync(_state.UserId, item.ContactId, newName));
            var idx = _state.Contacts.FindIndex(c => c.UserId == item.Id);
            if (idx >= 0) { _state.Contacts[idx] = _state.Contacts[idx] with { Nickname = newName }; RefreshSidebar(); }
            Application.RequestStop();
        };
        cancelBtn.Clicked += () => Application.RequestStop();
        dialog.Add(new Label("New nickname:") { X = 2, Y = 1 }, field, okBtn, cancelBtn);
        Application.Run(dialog);
    }

    private async Task RemoveSelectedContact()
    {
        if (_selectedSidebarIndex < 0 || _selectedSidebarIndex >= _sidebarItems.Count) return;
        var item = _sidebarItems[_selectedSidebarIndex];
        if (item.IsHeader || item.IsGroup) return;
        if (MessageBox.Query("Confirm", $"Remove '{item.Name}' from contacts?", "Yes", "No") != 0) return;
        ShowNotification(await _hub.RemoveContactAsync(_state.UserId, item.ContactId));
        _state.Contacts.RemoveAll(c => c.UserId == item.Id);
        RefreshSidebar();
    }

    private async Task AddContactToGroup()
    {
        if (_selectedSidebarIndex < 0 || _selectedSidebarIndex >= _sidebarItems.Count) return;
        var item = _sidebarItems[_selectedSidebarIndex];
        if (item.IsHeader || item.IsGroup) return;
        if (!_state.Groups.Any()) { ShowNotification("Create a group first"); return; }
        var dialog = new Dialog("Add to Group", 50, 14);
        var groupList = new ListView() { X = 2, Y = 2, Width = Dim.Fill(2), Height = 6 };
        groupList.SetSource(_state.Groups.Select(g => g.Name).ToList());
        var addBtn = new Button("Add") { X = Pos.Center(), Y = 9 };
        var cancelBtn = new Button("Cancel") { X = Pos.Center(), Y = 11 };
        addBtn.Clicked += async () =>
        {
            if (groupList.SelectedItem < 0) return;
            ShowNotification(await _hub.AddToGroupAsync(_state.UserId, _state.Groups[groupList.SelectedItem].Id, item.Id));
            Application.RequestStop();
        };
        cancelBtn.Clicked += () => Application.RequestStop();
        dialog.Add(new Label($"Add '{item.Name}' to:") { X = 2, Y = 1 }, groupList, addBtn, cancelBtn);
        Application.Run(dialog);
    }

    private void OnMessageReceived(MessageDto msg)
    {
        Application.MainLoop.Invoke(() =>
        {
            if (_state.CurrentChat?.Type == ChatType.Direct && _state.CurrentChat.Id == msg.SenderId)
            { _state.CurrentMessages.Add(msg); RefreshChatArea(); }
            else ShowNotification($"New message from {msg.SenderName}");
        });
    }

    private void OnGroupMessageReceived(MessageDto msg)
    {
        Application.MainLoop.Invoke(() =>
        {
            if (_state.CurrentChat?.Type == ChatType.Group && _state.CurrentChat.Id == msg.GroupId)
            { _state.CurrentMessages.Add(msg); RefreshChatArea(); }
            else ShowNotification($"New message in {_state.Groups.FirstOrDefault(g => g.Id == msg.GroupId)?.Name ?? "group"} from {msg.SenderName}");
        });
    }

    private void OnInvitationReceived(InvitationDto inv)
    {
        Application.MainLoop.Invoke(() =>
        { _state.PendingInvitations.Add(inv); ShowNotification($"Invitation from {inv.SenderDisplayName}"); });
    }

    private void OnInvitationAccepted(ContactDto contact)
    {
        Application.MainLoop.Invoke(() =>
        { _state.Contacts.Add(contact); RefreshSidebar(); ShowNotification($"{contact.DisplayName} accepted your invitation!"); });
    }

    private void OnContactStatusChanged(int userId, bool isOnline)
    {
        Application.MainLoop.Invoke(() =>
        {
            var idx = _state.Contacts.FindIndex(c => c.UserId == userId);
            if (idx >= 0) { _state.Contacts[idx] = _state.Contacts[idx] with { IsOnline = isOnline }; RefreshSidebar(); }
        });
    }

    private void ShowNotification(string message)
    {
        Application.MainLoop.Invoke(() => MessageBox.Query("", message, "OK"));
    }

    private static string FormatSize(long bytes) =>
        bytes < 1024 ? $"{bytes} B" : bytes < 1024 * 1024 ? $"{bytes / 1024} KB" : $"{bytes / (1024 * 1024)} MB";
}

public class SidebarItem
{
    public bool IsHeader { get; set; }
    public bool IsGroup { get; set; }
    public int Id { get; set; }
    public int ContactId { get; set; }
    public string Name { get; set; } = string.Empty;
}