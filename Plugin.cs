using System.Linq;
using System.Text;
using Dalamud.Game.Chat;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using EasyVote.Windows;

namespace EasyVote;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/easyvote";

    public Configuration Configuration { get; }
    public VoteSession Session { get; }
    public ChatSender ChatSender { get; }

    public readonly WindowSystem WindowSystem = new("EasyVote");
    private MainWindow MainWindow { get; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Options.RemoveAll(string.IsNullOrWhiteSpace);

        Session = new VoteSession(Configuration);
        ChatSender = new ChatSender(Framework);

        MainWindow = new MainWindow(this);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the EasyVote window."
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        ChatGui.ChatMessage += OnChatMessage;
    }

    public void Dispose()
    {
        ChatGui.ChatMessage -= OnChatMessage;
        ChatSender.Dispose();
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        WindowSystem.RemoveAllWindows();
        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args) => ToggleMainUi();

    public void ToggleMainUi() => MainWindow.Toggle();

    private bool IsListened(XivChatType type) => type switch
    {
        XivChatType.Say => Configuration.ListenSay,
        XivChatType.Yell => Configuration.ListenYell,
        XivChatType.Shout => Configuration.ListenShout,
        XivChatType.TellIncoming => Configuration.ListenTell,
        _ => false,
    };

    private void OnChatMessage(IHandleableChatMessage chat)
    {
        if (!Session.IsOpen || !IsListened(chat.LogKind))
            return;

        var text = chat.Message.TextValue.Trim();
        if (text.StartsWith('!'))
            Session.TryRegisterVote(GetSenderFullName(chat.Sender), text);
    }

    // "First Last@World"
    private static string GetSenderFullName(SeString sender)
    {
        var player = sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
        if (player != null)
        {
            var world = player.World.ValueNullable?.Name.ToString();
            return string.IsNullOrEmpty(world) ? player.PlayerName : $"{player.PlayerName}@{world}";
        }

        // No player payload (own messages): clean the text (removes party slot icons)
        var sb = new StringBuilder();
        foreach (var c in sender.TextValue)
        {
            if (char.IsLetter(c) || c == ' ' || c == '\'' || c == '-')
                sb.Append(c);
        }
        var name = sb.ToString().Trim();

        if (PlayerState.IsLoaded && name == PlayerState.CharacterName)
        {
            var home = PlayerState.HomeWorld.ValueNullable?.Name.ToString();
            return string.IsNullOrEmpty(home) ? name : $"{name}@{home}";
        }

        var current = PlayerState.CurrentWorld.ValueNullable?.Name.ToString();
        return string.IsNullOrEmpty(current) || name.Length == 0 ? name : $"{name}@{current}";
    }
}
