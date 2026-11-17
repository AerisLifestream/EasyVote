using System;
using System.Collections.Generic;
using System.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace EasyVote;

// Sends queued chat lines one by one, 1.1 seconds apart
public sealed unsafe class ChatSender : IDisposable
{
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(1.1);
    private const int MaxBytes = 500;

    private readonly IFramework framework;
    private readonly Queue<string> queue = new();
    private DateTime nextSend = DateTime.MinValue;

    public ChatSender(IFramework framework)
    {
        this.framework = framework;
        framework.Update += OnUpdate;
    }

    public bool IsSending => queue.Count > 0;
    public int Remaining => queue.Count;

    public void Enqueue(IEnumerable<string> lines)
    {
        foreach (var line in lines)
            queue.Enqueue(line);
    }

    public void Stop() => queue.Clear();

    public void Dispose()
    {
        framework.Update -= OnUpdate;
        queue.Clear();
    }

    private void OnUpdate(IFramework fw)
    {
        if (queue.Count == 0 || DateTime.UtcNow < nextSend)
            return;

        var line = queue.Dequeue();
        try
        {
            Send(line);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "EasyVote: failed to send chat message");
        }
        nextSend = DateTime.UtcNow + Delay;
    }

    private static void Send(string text)
    {
        text = Truncate(text);
        var uiModule = UIModule.Instance();
        if (uiModule == null)
            return;

        var str = Utf8String.FromString(text);
        str->SanitizeString((AllowedEntities)0x27F, null);
        uiModule->ProcessChatBoxEntry(str);
        str->Dtor(true);
    }

    private static string Truncate(string text)
    {
        while (Encoding.UTF8.GetByteCount(text) > MaxBytes)
            text = text[..^1];
        return text;
    }
}
