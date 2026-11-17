# EasyVote

A Dalamud plugin for FINAL FANTASY XIV that collects audience votes from chat — perfect for on-stage contests, venue events and shows.

## Installation

1. In game, type `/xlsettings` and open the **Experimental** tab.
2. Under **Custom Plugin Repositories**, add:
   ```
   https://raw.githubusercontent.com/AerisLifestream/EasyVote/main/pluginmaster.json
   ```
3. Click **+**, then **Save**.
4. Open `/xlplugins`, search for **EasyVote** and install it.

## Usage

Open the window with `/easyvote`.

1. Click **+**, type an option name (e.g. `Lyra`) and press **Enter**. Repeat for each option.
2. Click **Open vote**.
3. The audience votes by typing `!` followed by the option name in chat: `!Lyra`, `!Kai`...
   - Not case-sensitive (`!lyra` works too).
   - One vote per player (`First Last@World`); voting again replaces the previous vote.
4. Watch the results live (votes, percentages, voter list).
5. Click **Close vote**, then **Shout results** to announce them in `/sh`.

### Channels

EasyVote listens to **Say**, **Yell**, **Shout** and incoming **Tells** (each can be toggled). Votes are detected even if the channel is hidden in your chat tabs.

### Result message

In the **Settings** tab, write your own announcement. `[Result]` is replaced by:

```
Lyra: 31% - 5 voters
Kai: 69% - 11 voters
Total: 16 voters
```

Each line is sent as a separate `/sh` message, 1.1 seconds apart. Empty lines are skipped.

### Reset

**Reset votes** only works while holding **Ctrl**, to avoid accidents.

## License

AGPL-3.0-or-later — see [LICENSE](LICENSE).
