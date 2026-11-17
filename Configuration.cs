using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace EasyVote;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public const string DefaultTemplate = "VOTE RESULTS\n\n[Result]\n\nThanks for voting!";

    public int Version { get; set; } = 0;

    // Option names = vote commands (!Name)
    public List<string> Options { get; set; } = new();

    public bool ListenSay { get; set; } = true;
    public bool ListenYell { get; set; } = true;
    public bool ListenShout { get; set; } = true;

    // ?Voter-Option fake votes
    public bool TestMode { get; set; } = false;

    // Shouted message, [Result] is replaced by the results
    public string ResultTemplate { get; set; } = DefaultTemplate;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
