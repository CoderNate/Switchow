using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Switchow
{
    class Program
    {
        private const int DisplayLineCount = 5;
        private const string Prompt = "> ";

        static void Main(string[] args)
        {
            static string getWindowFilePath(IntPtr handle)
            {
                GetWindowThreadProcessId(handle, out var procId);
                var proc = System.Diagnostics.Process.GetProcessById((int)procId);
                string fullPath;
                try
                {
                    fullPath = proc.MainModule.FileName;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    fullPath = "";
                }
                return System.IO.Path.GetFileNameWithoutExtension(fullPath);
            }

            Console.WriteLine("Start typing to search for windows to switch to");
            Console.Write(Prompt);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var allWindows = OpenWindowGetter.GetOpenWindows().AsParallel()
                .WithDegreeOfParallelism(Math.Min(Environment.ProcessorCount, 4))
                .Select(a => new WindowInfo(a.Key, a.Value, getWindowFilePath(a.Key)))
                .Where(a => a.Entry.FileName != "Switchow")
                .ToArray();
            //Console.WriteLine($"Took {sw}");

            var chars = new List<char>();
            var handlesDict = new Dictionary<int, IntPtr>();
            while (true)
            {
                var keyInfo = Console.ReadKey(intercept: true);
                // Oem4 is Ctrl+[ which is like another way of hitting escape (especially in Vim world)
                if (keyInfo.Key == ConsoleKey.Escape || keyInfo.Key == ConsoleKey.Oem4)
                {
                    return;
                }
                var isAltShortcut = (keyInfo.Modifiers & ConsoleModifiers.Alt) == ConsoleModifiers.Alt;
                if (isAltShortcut) { }
                else if (keyInfo.Key == ConsoleKey.Enter)
                {
                    var windowIndex = 0;
                    if (handlesDict.TryGetValue(windowIndex, out var handle))
                    {
                        SetForegroundWindow(handle);
                        break;
                    }
                }
                else if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (chars.Count > 0)
                    {
                        chars.RemoveAt(chars.Count - 1);
                    }
                }
                else
                {
                    chars.Add(keyInfo.KeyChar);
                }
                var searchText = string.Join("", chars);
                var allSortedWindows = allWindows
                    .Select(wndInf => new { wndInf, score = GetScore(searchText, wndInf.Entry) })
                    .OrderByDescending(a => a.score)
                    .ToArray();
                if (isAltShortcut)
                {
                    foreach (var entry in allSortedWindows)
                    {
                        var wndInf = entry.wndInf;
                        var chr = GetShortcutChar(wndInf.Entry);
                        if (chr == keyInfo.KeyChar)
                        {
                            SetForegroundWindow(wndInf.Handle);
                            return;
                        }
                    }
                }

                var sortedWindows = allSortedWindows
                    .Where(a => a.score > 0)
                    .Select(a => a.wndInf)
                    .ToArray();
                handlesDict = sortedWindows
                    .Take(DisplayLineCount)
                    .Select((wndInf, index) => new { wndInf, index })
                    .ToDictionary(a => a.index, a => a.wndInf.Handle);
                RefreshDisplay(searchText, sortedWindows);
            }
        }

        private static void RefreshDisplay(string searchText, WindowInfo[] sortedWindows)
        {
            var startColor = Console.ForegroundColor;
            Console.Clear();
            Console.WriteLine("Press Enter to select the top match or Alt + label (case sensitive)");
            for (var i = 0; i < DisplayLineCount; i++)
            {
                if (i < sortedWindows.Length)
                {
                    var wndInf = sortedWindows[i];
                    var fileName = wndInf.Entry.FileName;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write(GetShortcutChar(wndInf.Entry));
                    Console.ForegroundColor = startColor;
                    var indexSets = GetStringMatchIndexSets(searchText, wndInf.Entry);
                    var bestSet = indexSets
                        .Select(indexSet => new { score = ScoreIndexSet(searchText, indexSet, fileName.Length), indexSet })
                        .OrderByDescending(a => a.score)
                        .First();
                    Console.Write($") ");
                    const bool showScoreForDebug = false;
                    if (showScoreForDebug)
                    {
                        Console.Write($"(score={bestSet.score}) ");
                    }
                    var text = fileName + wndInf.Entry.WindowTitle;
                    for (var charIndex = 0; charIndex < text.Length; charIndex++)
                    {
                        if (charIndex == fileName.Length)
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.Write(" | "); // Separator between executable file name and the window title
                            Console.ForegroundColor = startColor;
                        }
                        var isMatch = bestSet.indexSet.Contains(charIndex);
                        if (isMatch)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                        }
                        Console.Write(text[charIndex]);
                        if (isMatch)
                        {
                            Console.ForegroundColor = startColor;
                        }
                    }
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine();
                }
            }
            Console.Write(Prompt + searchText);
        }

        private static readonly Lazy<char[]> _allShortcutChars = new Lazy<char[]>(() =>
        {
            static IEnumerable<char> enumerateChars(char start, char end)
                => Enumerable.Range(start, end - start + 1).Select(a => (char)a);
            return enumerateChars('a', 'z')
                .Concat(enumerateChars('A', 'Z'))
                .Concat(new[] { ';', '[', ']', '\\', '/' }).ToArray();
        });
        static char GetShortcutChar(Entry entry)
        {
            var text = entry.FileName + entry.WindowTitle;
            var charIndex = Math.Abs(GetStableStringHashCode(text)) % _allShortcutChars.Value.Length;
            return _allShortcutChars.Value[charIndex];
        }

        static int ScoreIndexSet(string searchText, int[] set, int fileNameLength)
        {
            // Matches on the file name are weighted more than on the window title
            var fileNameIndicesCount = set.Count(charIndex => charIndex <= fileNameLength);
            var remainingIndicesCount = set.Length - fileNameIndicesCount;
            // Penalty for any unused characters
            var penalty = (searchText.Length - set.Length) * 5;
            var sequencesBonus = GetSequentialNumberSets(set).Sum(a => (int)(a.Length * 1.5));
            return sequencesBonus + (int)(fileNameIndicesCount * 2.5) + remainingIndicesCount * 1 - penalty;
        }
        static int GetScore(string searchText, Entry entry)
        {
            var indexSets = GetStringMatchIndexSets(searchText, entry);
            return indexSets.Select(set => ScoreIndexSet(searchText, set, entry.FileName.Length)).DefaultIfEmpty(0).Max();
        }

        /// <summary> Get all sequences of at least 2 consecutive numbers </summary>
        static int[][] GetSequentialNumberSets(int[] numbers)
        {
            var sequences = new List<int[]>();
            var curSequenceStartIndex = 0;
            for (var i = 1; i < numbers.Length; i++)
            {
                var isSequential = numbers[i] == numbers[i - 1] + 1;
                int curSequenceEndIndex = isSequential ? i : i - 1;
                var seqLength = curSequenceEndIndex - curSequenceStartIndex + 1;
                var isLastNumber = i == numbers.Length - 1;
                if (seqLength > 1 && (!isSequential || isLastNumber))
                {
                    sequences.Add(numbers.Skip(curSequenceStartIndex).Take(seqLength).ToArray());
                }
                if (!isSequential)
                {
                    curSequenceStartIndex = i;
                }
            }
            return sequences.ToArray();
        }

        static int[][] GetStringMatchIndexSets(string searchText, Entry entry)
        {
            return GetStringMatchIndexSets(entry.FileName + entry.WindowTitle, searchText.ToArray());
        }

        /// <summary>
        /// For example, given string abcbc and characters 'b' & 'c' would return 3 index sets: [1,2] [1, 4] [3, 4]
        /// </summary>
        static int[][] GetStringMatchIndexSets(string s, char[] characters)
        {
            var results = new List<int[]> { Array.Empty<int>() };
            for (var charIndex = 0; charIndex < characters.Length; charIndex++)
            {
                var newResults = new List<int[]>();
                foreach (var result in results)
                {
                    var stringStartIndex = result.Length > 0 ? result.Last() + 1 : 0;
                    for (var stringIndex = stringStartIndex; stringIndex < s.Length; stringIndex++)
                    {
                        if (char.ToLower(characters[charIndex]) == char.ToLower(s[stringIndex]))
                        {
                            newResults.Add(result.Append(stringIndex).ToArray());
                        }
                    }
                }
                results = newResults;
            }
            return results.ToArray();
        }

        private static int GetStableStringHashCode(string text)
        {
            unchecked
            {
                return text.Aggregate(23, (int acc, char c) => acc * 31 + c);
            }
        }

        public class WindowInfo
        {
            public WindowInfo(IntPtr handle, string windowTitle, string fileName)
            {
                Handle = handle;
                Entry = new Entry(windowTitle: windowTitle, fileName: fileName);
            }
            public IntPtr Handle { get; }
            public Entry Entry { get; }
        }
        public class Entry
        {
            public Entry(string windowTitle, string fileName)
            {
                WindowTitle = windowTitle;
                FileName = fileName;
            }
            public string WindowTitle { get; }
            public string FileName { get; }
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowThreadProcessId(IntPtr handle, out uint processId);

    }
}
