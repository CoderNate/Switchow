# Switchow

A very simple Windows console app for switching between your open application windows.

## Usage

Just start typing and a list of matching application windows will be shown.  Press enter to set the top window in the list as the foreground window.  The format of the list is `EXECUTABLE_NAME | WINDOW_TITLE`.  Matches on the executable name are weighted more strongly than matches on the window title.  Example:
```
0) notepad | *Untitled - Notepad
1) devenv | Switchow - Microsoft Visual Studio
2) Explorer | Documents
3) msedge | Google

> e
```

## HotKeyHelper

The other project in the solution is a tiny app that listens for Alt+Space shortcut key combination and launches Switchow.  It runs invisibly so Task Manager is the easiest way to kill it.  You could put a shortcut to it in your Windows startup folder if you want it to start automatically.  If you're already using Autohotkey or Microsoft Powertoys to create shortcuts to apps then use that to launch Switchow instead.

### Alternatives

- **Switcheroo** - Really nice app but it felt like the fuzzy search was possibly a little too fuzzy for my liking (too easy to match against the wrong window title if you have a lot of windows open).
- **[FastWindowSwitcher](https://github.com/JochenBaier/fastwindowswitcher)** - I really like how few characters you need to type with FastWindowSwitcher but the drawback (if I remember correctly) is that the shortcut keys don't remain stable over time so you can't really build up a muscle memory for an app you switch to often.
- **Microsoft PowerToys Run** - Haven't really tried it.
