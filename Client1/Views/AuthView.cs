using Terminal.Gui;
using MessengerClient.Models;
using MessengerClient.Services;

namespace MessengerClient.Views;

public class AuthView : Window
{
    private readonly HubService _hub;
    private readonly Action<AppState> _onSuccess;
    private bool _isRegisterMode = false;

    private TextField _usernameField = null!;
    private TextField _passwordField = null!;
    private TextField _displayNameField = null!;
    private Label _statusLabel = null!;
    private Button _toggleButton = null!;
    private Label _displayNameLabel = null!;
    private Label _titleLabel = null!;

    public AuthView(HubService hub, Action<AppState> onSuccess) : base("Messenger")
    {
        _hub = hub; _onSuccess = onSuccess;
        X = 0; Y = 0; Width = Dim.Fill(); Height = Dim.Fill();
        BuildUI();
    }

    private void BuildUI()
    {
        var centerFrame = new FrameView("Welcome to Messenger")
        { X = Pos.Center(), Y = Pos.Center() - 8, Width = 50, Height = 20 };

        _titleLabel = new Label("Login to your account") { X = Pos.Center(), Y = 1 };
        var usernameLabel = new Label("Username:") { X = 2, Y = 3 };
        _usernameField = new TextField("") { X = 2, Y = 4, Width = Dim.Fill(2) };
        var passwordLabel = new Label("Password:") { X = 2, Y = 6 };
        _passwordField = new TextField("") { X = 2, Y = 7, Width = Dim.Fill(2), Secret = true };
        _displayNameLabel = new Label("Display Name:") { X = 2, Y = 9, Visible = false };
        _displayNameField = new TextField("") { X = 2, Y = 10, Width = Dim.Fill(2), Visible = false };

        var submitButton = new Button("Login") { X = Pos.Center(), Y = 13 };
        submitButton.Clicked += async () => await HandleSubmit();

        _toggleButton = new Button("No account? Register") { X = Pos.Center(), Y = 15 };
        _toggleButton.Clicked += ToggleMode;
        _statusLabel = new Label("") { X = 2, Y = 17, Width = Dim.Fill(2) };

        centerFrame.Add(_titleLabel, usernameLabel, _usernameField, passwordLabel, _passwordField,
            _displayNameLabel, _displayNameField, submitButton, _toggleButton, _statusLabel);
        Add(centerFrame);
        _usernameField.SetFocus();
    }

    private void ToggleMode()
    {
        _isRegisterMode = !_isRegisterMode;
        if (_isRegisterMode)
        {
            _titleLabel.Text = "Create a new account";
            _toggleButton.Text = "Have account? Login";
            _displayNameLabel.Visible = true;
            _displayNameField.Visible = true;
        }
        else
        {
            _titleLabel.Text = "Login to your account";
            _toggleButton.Text = "No account? Register";
            _displayNameLabel.Visible = false;
            _displayNameField.Visible = false;
        }
        Application.Refresh();
    }

    private async Task HandleSubmit()
    {
        var username = _usernameField.Text.ToString()?.Trim() ?? "";
        var password = _passwordField.Text.ToString()?.Trim() ?? "";
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        { SetStatus("Username and password are required", true); return; }

        SetStatus("Connecting...", false);
        AuthResponse result;
        if (_isRegisterMode)
        {
            var displayName = _displayNameField.Text.ToString()?.Trim() ?? username;
            result = await _hub.RegisterAsync(username, password, displayName);
        }
        else result = await _hub.LoginAsync(username, password);

        if (result.Success)
            _onSuccess(new AppState { UserId = result.UserId, Username = result.Username, DisplayName = result.DisplayName, IsLoggedIn = true });
        else
            SetStatus(result.Message, true);
    }

    private void SetStatus(string message, bool isError)
    {
        Application.MainLoop.Invoke(() =>
        {
            _statusLabel.Text = message;
            _statusLabel.ColorScheme = isError
                ? new ColorScheme { Normal = Terminal.Gui.Attribute.Make(Color.Red, Color.Black) }
                : new ColorScheme { Normal = Terminal.Gui.Attribute.Make(Color.Green, Color.Black) };
            Application.Refresh();
        });
    }
}