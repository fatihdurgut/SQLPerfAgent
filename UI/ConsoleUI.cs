namespace SQLPerfAgent.UI;

/// <summary>
/// Thrown when the user types "quit" at any prompt.
/// </summary>
internal sealed class QuitException : Exception
{
    public QuitException() : base("User requested quit.") { }
}

/// <summary>
/// Console display helpers for colored output, tables, and prompts.
/// </summary>
internal static class ConsoleUI
{
    /// <summary>
    /// Checks if the input is a quit command and throws <see cref="QuitException"/> if so.
    /// </summary>
    private static void CheckForQuit(string? input)
    {
        if (input is not null && input.Trim().Equals("quit", StringComparison.OrdinalIgnoreCase))
            throw new QuitException();
    }

    public static void WriteBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("""
        ╔═══════════════════════════════════════════════════╗
        ║          SQL Performance & Security Agent         ║
        ║            Powered by GitHub Copilot SDK          ║
        ╚═══════════════════════════════════════════════════╝
        """);
        Console.ResetColor();        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("        Type \"quit\" at any prompt to exit.");
        Console.ResetColor();    }

    public static void WriteHeader(string text)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"── {text} ──");
        Console.ResetColor();
    }

    public static void WriteSuccess(string text)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ {text}");
        Console.ResetColor();
    }

    public static void WriteError(string text)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  ✗ {text}");
        Console.ResetColor();
    }

    public static void WriteWarning(string text)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"  ⚠ {text}");
        Console.ResetColor();
    }

    public static void WriteInfo(string text)
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine($"  ℹ {text}");
        Console.ResetColor();
    }

    public static void WriteSql(string sql)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("  ┌─── SQL Script ───");
        foreach (var line in sql.Split('\n'))
        {
            Console.WriteLine($"  │ {line}");
        }
        Console.WriteLine("  └───────────────────");
        Console.ResetColor();
    }

    /// <summary>
    /// Prompts the user to pick from numbered choices. Returns 0-based index.
    /// </summary>
    public static int PromptChoice(string question, params string[] options)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(question);
        Console.ResetColor();

        for (int i = 0; i < options.Length; i++)
        {
            Console.WriteLine($"  [{i + 1}] {options[i]}");
        }

        while (true)
        {
            Console.Write("  > ");
            var line = Console.ReadLine()?.Trim();
            CheckForQuit(line);
            if (int.TryParse(line, out int choice) && choice >= 1 && choice <= options.Length)
            {
                return choice - 1;
            }
            WriteError($"Please enter a number between 1 and {options.Length}.");
        }
    }

    /// <summary>
    /// Prompts the user for text input.
    /// </summary>
    public static string PromptInput(string label, string? defaultValue = null)
    {
        var suffix = defaultValue is not null ? $" [{defaultValue}]" : "";
        Console.Write($"  {label}{suffix}: ");
        var input = Console.ReadLine()?.Trim();
        CheckForQuit(input);
        return string.IsNullOrEmpty(input) && defaultValue is not null ? defaultValue : input ?? "";
    }

    /// <summary>
    /// Prompts for a password (masked input).
    /// </summary>
    public static string PromptPassword(string label)
    {
        Console.Write($"  {label}: ");
        var password = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password.Length--;
                Console.Write("\b \b");
            }
            else if (key.Key != ConsoleKey.Backspace)
            {
                password.Append(key.KeyChar);
                Console.Write('*');
            }
        }
        Console.WriteLine();
        return password.ToString();
    }

    /// <summary>
    /// Prompts Yes/No/Skip. Returns "yes", "skip", or "abort".
    /// </summary>
    public static string PromptConfirm(string question)
    {
        Console.WriteLine();
        Console.Write($"  {question} [Y]es / [S]kip / [A]bort / [Q]uit: ");
        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;
            switch (key)
            {
                case ConsoleKey.Y:
                    Console.WriteLine("Yes");
                    return "yes";
                case ConsoleKey.S:
                    Console.WriteLine("Skip");
                    return "skip";
                case ConsoleKey.A:
                    Console.WriteLine("Abort");
                    return "abort";
                case ConsoleKey.Q:
                    Console.WriteLine("Quit");
                    throw new QuitException();
            }
        }
    }

    /// <summary>
    /// Prompts the user to select multiple items by entering comma-separated numbers.
    /// Returns the 0-based indices of chosen items.
    /// </summary>
    public static List<int> PromptMultiChoice(string question, string[] options)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(question);
        Console.ResetColor();

        for (int i = 0; i < options.Length; i++)
        {
            Console.WriteLine($"  [{i + 1}] {options[i]}");
        }

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Enter numbers separated by commas (e.g., 1,3,4) or 'all' for everything.");
        Console.ResetColor();

        while (true)
        {
            Console.Write("  > ");
            var line = Console.ReadLine()?.Trim();
            CheckForQuit(line);

            if (string.IsNullOrEmpty(line))
            {
                WriteError("Please enter at least one selection.");
                continue;
            }

            if (line.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return Enumerable.Range(0, options.Length).ToList();
            }

            var indices = new List<int>();
            var parts = line.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var valid = true;

            foreach (var part in parts)
            {
                if (int.TryParse(part, out int num) && num >= 1 && num <= options.Length)
                {
                    if (!indices.Contains(num - 1))
                        indices.Add(num - 1);
                }
                else
                {
                    valid = false;
                    break;
                }
            }

            if (valid && indices.Count > 0)
                return indices;

            WriteError($"Please enter valid numbers between 1 and {options.Length}, separated by commas.");
        }
    }
}
