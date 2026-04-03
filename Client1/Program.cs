using Terminal.Gui;
using MessengerClient.Models;
using MessengerClient.Services;
using MessengerClient.Views;

var hub = new HubService();

try
{
    Application.Init();

    var connectingDialog = new Dialog("Connecting", 40, 7);
    connectingDialog.Add(new Label("Connecting to server...") { X = Pos.Center(), Y = 2 });

    bool connected = false;
    Exception? connectError = null;

    Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(100), (_) =>
    {
        if (!connected) return true;
        Application.RequestStop(connectingDialog);
        return false;
    });

    _ = Task.Run(async () =>
    {
        try { await hub.ConnectAsync(); connected = true; }
        catch (Exception ex) { connectError = ex; connected = true; }
    });

    Application.Run(connectingDialog);

    if (connectError != null)
    {
        MessageBox.ErrorQuery("Connection Error",
            $"Cannot connect to server:\n{connectError.Message}\n\nMake sure the server is running on port 5000.", "OK");
        Application.Shutdown();
        return;
    }

    AppState? appState = null;
    void ShowMainApp(AppState state) { appState = state; Application.RequestStop(); }

    Application.Run(new AuthView(hub, ShowMainApp));

    if (appState != null)
        Application.Run(new MainView(hub, appState));

    await hub.DisconnectAsync();
    Application.Shutdown();
}
catch (Exception ex)
{
    Console.WriteLine($"Fatal error: {ex.Message}\n{ex.StackTrace}");
}