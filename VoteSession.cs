using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EasyVote;

public sealed record Vote(string VoterName, int OptionIndex, DateTime Time, bool IsTest);

public sealed class VoteSession
{
    public const int MaxOptions = 26;

    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    private readonly Configuration config;
    private readonly Dictionary<string, Vote> votes = new();

    public VoteSession(Configuration config) => this.config = config;

    public bool IsOpen { get; private set; }
    public int TotalVotes => votes.Count;
    public IEnumerable<Vote> Votes => votes.Values;

    public void Open() => IsOpen = true;
    public void Close() => IsOpen = false;
    public void Reset() => votes.Clear();

    private static string Normalize(string s) => Spaces.Replace(s.Trim(), " ").ToLowerInvariant();

    // Index of the option matching the text, or -1 (empty or duplicate names never match)
    private int FindOption(string choice)
    {
        var wanted = Normalize(choice);
        if (wanted.Length == 0)
            return -1;

        var found = -1;
        for (var i = 0; i < config.Options.Count; i++)
        {
            if (Normalize(config.Options[i]) != wanted)
                continue;
            if (found >= 0)
                return -1; // duplicate name
            found = i;
        }
        return found;
    }

    // Real vote: "!Option", voter = "First Last@World"
    public bool TryRegisterRealVote(string voterFullName, string text)
    {
        if (!IsOpen || string.IsNullOrWhiteSpace(voterFullName))
            return false;

        text = text.Trim();
        if (!text.StartsWith('!'))
            return false;

        var index = FindOption(text[1..]);
        if (index < 0)
            return false;

        votes[$"real:{voterFullName.ToLowerInvariant()}"] = new Vote(voterFullName, index, DateTime.Now, false);
        return true;
    }

    // Test vote: "?Voter Name-Option", voter = name in the command
    public bool TryRegisterTestVote(string text)
    {
        if (!IsOpen)
            return false;

        text = text.Trim();
        if (!text.StartsWith('?'))
            return false;

        var body = text[1..];
        var dash = body.LastIndexOf('-');
        if (dash <= 0 || dash == body.Length - 1)
            return false;

        var voter = Spaces.Replace(body[..dash].Trim(), " ");
        if (voter.Length == 0)
            return false;

        var index = FindOption(body[(dash + 1)..]);
        if (index < 0)
            return false;

        votes[$"test:{voter.ToLowerInvariant()}"] = new Vote(voter, index, DateTime.Now, true);
        return true;
    }

    public int CountFor(int index) => votes.Values.Count(v => v.OptionIndex == index);

    // Called when an option is removed: drop its votes and shift the following ones
    public void OnOptionRemoved(int index)
    {
        foreach (var key in votes.Keys.ToList())
        {
            var vote = votes[key];
            if (vote.OptionIndex == index)
                votes.Remove(key);
            else if (vote.OptionIndex > index)
                votes[key] = vote with { OptionIndex = vote.OptionIndex - 1 };
        }
    }

    public bool NameExists(string name)
    {
        var wanted = Normalize(name);
        return config.Options.Any(o => Normalize(o) == wanted);
    }

    public bool IsEmpty(int index) => Normalize(config.Options[index]).Length == 0;

    public bool IsDuplicate(int index)
    {
        var name = Normalize(config.Options[index]);
        return name.Length > 0 && config.Options.Where((_, i) => i != index).Any(o => Normalize(o) == name);
    }

    public string OptionLabel(int index)
    {
        var name = config.Options[index].Trim();
        return name.Length == 0 ? "(unnamed)" : name;
    }

    // [Result] block: "Name: 31% - 5 voters" per option + total
    public string BuildResultBlock()
    {
        var lines = new List<string>();
        for (var i = 0; i < config.Options.Count; i++)
        {
            var count = CountFor(i);
            var pct = TotalVotes > 0 ? count * 100.0 / TotalVotes : 0;
            lines.Add($"{OptionLabel(i)}: {pct:0}% - {Voters(count)}");
        }
        lines.Add($"Total: {Voters(TotalVotes)}");
        return string.Join("\n", lines);
    }

    private static string Voters(int n) => n == 1 ? "1 voter" : $"{n} voters";

    // Final message lines (empty lines removed)
    public List<string> BuildMessageLines()
    {
        var message = config.ResultTemplate.Replace("[Result]", BuildResultBlock());
        return message.Replace("\r", string.Empty)
                      .Split('\n')
                      .Select(l => l.Trim())
                      .Where(l => l.Length > 0)
                      .ToList();
    }
}
