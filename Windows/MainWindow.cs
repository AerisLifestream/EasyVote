using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;

namespace EasyVote.Windows;

public sealed class MainWindow : Window
{
    private readonly Plugin plugin;
    private Configuration Config => plugin.Configuration;
    private VoteSession Session => plugin.Session;

    public MainWindow(Plugin plugin) : base("EasyVote##EasyVoteMain")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 480),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    private ChatSender Sender => plugin.ChatSender;

    private const string ConfirmPopup = "Shout results?##EasyVoteConfirm";
    private bool openConfirm;

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("##EasyVoteTabs"))
            return;

        if (ImGui.BeginTabItem("Vote"))
        {
            DrawControls();
            ImGui.Separator();
            DrawChannels();
            ImGui.Separator();
            DrawOptions();
            ImGui.Separator();
            DrawResults();
            ImGui.Separator();
            DrawVoters();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Settings"))
        {
            DrawSettings();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();

        DrawConfirmPopup();
    }

    private void DrawControls()
    {
        if (Session.IsOpen)
            ImGui.TextColored(ImGuiColors.HealerGreen, "VOTE OPEN");
        else
            ImGui.TextColored(ImGuiColors.DalamudGrey, "VOTE CLOSED");

        ImGui.SameLine();
        ImGui.TextUnformatted($"- {Session.TotalVotes} vote(s)");

        if (ImGui.Button(Session.IsOpen ? "Close vote" : "Open vote"))
        {
            if (Session.IsOpen) Session.Close();
            else Session.Open();
        }

        // Reset only while holding Ctrl
        ImGui.SameLine();
        var ctrl = ImGui.GetIO().KeyCtrl;
        ImGui.BeginDisabled(!ctrl);
        if (ImGui.Button("Reset votes"))
            Session.Reset();
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Hold Ctrl to reset");

        ImGui.SameLine();
        if (Sender.IsSending)
        {
            if (ImGui.Button($"Stop ({Sender.Remaining})"))
                Sender.Stop();
        }
        else if (ImGui.Button("Shout results"))
        {
            openConfirm = true;
        }
    }

    private void DrawConfirmPopup()
    {
        if (openConfirm)
        {
            ImGui.OpenPopup(ConfirmPopup);
            openConfirm = false;
        }

        if (!ImGui.BeginPopupModal(ConfirmPopup, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        var lines = Session.BuildMessageLines();
        ImGui.TextUnformatted("This will be sent in /sh:");
        ImGui.Separator();
        foreach (var line in lines)
            ImGui.TextUnformatted(line);
        ImGui.Separator();

        if (ImGui.Button("Shout"))
        {
            Sender.Enqueue(lines.Select(l => $"/sh {l}"));
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
            ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }

    private void DrawSettings()
    {
        ImGui.TextUnformatted("Result message ([Result] = vote results):");

        var template = Config.ResultTemplate;
        if (ImGui.InputTextMultiline("##template", ref template, 2000, new Vector2(-1, 150)))
            Config.ResultTemplate = template;
        if (ImGui.IsItemDeactivatedAfterEdit())
            Config.Save();

        if (ImGui.Button("Restore default"))
        {
            Config.ResultTemplate = Configuration.DefaultTemplate;
            Config.Save();
        }

        ImGui.Separator();
        ImGui.TextColored(ImGuiColors.DalamudGrey, "Preview (one /sh message per line, empty lines skipped):");
        foreach (var line in Session.BuildMessageLines())
            ImGui.TextUnformatted(line);
    }

    private void DrawChannels()
    {
        ImGui.TextUnformatted("Channels:");
        ImGui.SameLine();

        var say = Config.ListenSay;
        if (ImGui.Checkbox("Say", ref say)) { Config.ListenSay = say; Config.Save(); }
        ImGui.SameLine();

        var yell = Config.ListenYell;
        if (ImGui.Checkbox("Yell", ref yell)) { Config.ListenYell = yell; Config.Save(); }
        ImGui.SameLine();

        var shout = Config.ListenShout;
        if (ImGui.Checkbox("Shout", ref shout)) { Config.ListenShout = shout; Config.Save(); }

        var test = Config.TestMode;
        if (ImGui.Checkbox("Test mode", ref test)) { Config.TestMode = test; Config.Save(); }
        if (Config.TestMode)
        {
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudOrange, "Type ?VoterName-Option (e.g. ?Bob-Lyra)");
        }
    }

    // "+" input state
    private bool adding;
    private bool focusInput;
    private string newName = string.Empty;
    private string? addError;

    private void DrawOptions()
    {
        ImGui.TextUnformatted("Options (the audience types !Name):");

        var removeIndex = -1;
        for (var i = 0; i < Config.Options.Count; i++)
        {
            ImGui.PushID(i);
            if (ImGui.SmallButton("X"))
                removeIndex = i;
            ImGui.SameLine();
            ImGui.TextUnformatted($"!{Config.Options[i]}");
            ImGui.PopID();
        }

        if (removeIndex >= 0)
        {
            Config.Options.RemoveAt(removeIndex);
            Session.OnOptionRemoved(removeIndex);
            Config.Save();
        }

        if (adding)
            DrawAddInput();
        else
        {
            ImGui.BeginDisabled(Config.Options.Count >= VoteSession.MaxOptions);
            if (ImGui.Button("+"))
            {
                adding = true;
                focusInput = true;
                newName = string.Empty;
                addError = null;
            }
            ImGui.EndDisabled();
        }
    }

    private void DrawAddInput()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("!");
        ImGui.SameLine();

        if (focusInput)
        {
            ImGui.SetKeyboardFocusHere();
            focusInput = false;
        }

        ImGui.SetNextItemWidth(200);
        var submitted = ImGui.InputText("##newOption", ref newName, 64, ImGuiInputTextFlags.EnterReturnsTrue);

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            adding = false;
            return;
        }

        if (submitted)
        {
            var name = newName.Trim();
            if (name.Length == 0)
                addError = "Name is empty";
            else if (Session.NameExists(name))
                addError = "This option already exists";
            else
            {
                Config.Options.Add(name);
                Config.Save();
                adding = false;
                return;
            }
            focusInput = true; // keep typing after an error
        }

        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.DalamudGrey, "Enter = add, Esc = cancel");

        if (addError != null)
            ImGui.TextColored(ImGuiColors.DalamudRed, addError);
    }

    private void DrawResults()
    {
        ImGui.TextUnformatted("Results:");

        if (Config.Options.Count == 0)
        {
            ImGui.TextColored(ImGuiColors.DalamudGrey, "No options yet - click + to add one.");
            return;
        }

        var total = Session.TotalVotes;
        var max = total > 0 ?Enumerable.Range(0, Config.Options.Count).Max(Session.CountFor) : -1;

        for (var i = 0; i < Config.Options.Count; i++)
        {
            var count = Session.CountFor(i);
            var fraction = total > 0 ? (float)count / total : 0f;

            ImGui.PushID(1000 + i);

            if (count == max && max > 0)
                ImGui.TextColored(ImGuiColors.DalamudYellow, Session.OptionLabel(i));
            else
                ImGui.TextUnformatted(Session.OptionLabel(i));

            ImGui.ProgressBar(fraction, new Vector2(-1, 0), $"{count} ({fraction * 100:0}%)");

            ImGui.PopID();
        }
    }

    private void DrawVoters()
    {
        if (!ImGui.CollapsingHeader($"Voters ({Session.TotalVotes})"))
            return;

        if (!ImGui.BeginTable("##voters", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY,
                new Vector2(0, 200)))
            return;

        ImGui.TableSetupColumn("Player");
        ImGui.TableSetupColumn("Vote", ImGuiTableColumnFlags.WidthFixed, 110);
        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 70);
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableHeadersRow();

        foreach (var vote in Session.Votes.OrderByDescending(v => v.Time))
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            if (vote.IsTest)
            {
                ImGui.TextColored(ImGuiColors.DalamudOrange, "[TEST]");
                ImGui.SameLine();
            }
            ImGui.TextUnformatted(vote.VoterName);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(Session.OptionLabel(vote.OptionIndex));
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(vote.Time.ToString("HH:mm:ss"));
        }

        ImGui.EndTable();
    }
}
