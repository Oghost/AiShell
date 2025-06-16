using Spectre.Console;
using AiShell.Storage;

namespace AiShell.UI;

public class ConsoleRenderer
{
    public void ShowWelcomeBanner()
    {
        var rule = new Rule("[bold blue]AiShell for Windows[/]")
        {
            Justification = Justify.Center
        };
        
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();
    }

    public void ShowWelcome()
    {
    }
    public void ShowWelcomeMessage()
    {
        var panel = new Panel(new Markup(            
            "[dim]Transform natural language into Windows commands using local AI.[/]\n\n" +
            "[yellow]Quick start:[/]\n" +
            " Type natural commands like 'list video files'\n" +
            " Use 'help' to see all available commands\n" +
            " Use 'vars' to see your variables\n" +
            " Use 'exit' to quit\n"))          
        {
            Header = new PanelHeader(""),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    public void ShowPrompt(string currentDirectory, bool newline = true)
    {
        var prompt = $"{currentDirectory}> ";
        
        if (newline)
            AnsiConsole.Markup(prompt); 
        else
            AnsiConsole.Markup(prompt.TrimEnd('\n'));
    }

    public void ShowGeneratedCommand(string command)
    {
        var prompt = $"{Environment.CurrentDirectory}> ";
        var markup = new Markup($"[blue]{command}[/]");
        AnsiConsole.Markup(prompt);
        AnsiConsole.Write(markup);
    }
        
    public void ShowCommandOutput(string output)
    {
        if (!string.IsNullOrWhiteSpace(output))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[blue]{Markup.Escape(output.Trim())}[/]");
        }
    }

    public void ShowCommandResult(bool success, int exitCode, TimeSpan executionTime)
    {
        var status = success ? "[green]Success[/]" : $"[red]Failed (Exit Code: {exitCode})[/]";
        var time = $"[dim]Execution time: {executionTime.TotalMilliseconds:F0}ms[/]";
        
        AnsiConsole.MarkupLine($"{status} {time}");
        AnsiConsole.WriteLine();
    }

    public void ShowError(string message)
    {
        //AnsiConsole.Write(new Panel(new Markup($"[bold red]{message}[/]"))
        //{
        //    Header = new PanelHeader("Error"),
        //    Border = BoxBorder.Rounded,
        //    BorderStyle = new Style(Color.Red)
        //});

        AnsiConsole.Write(new Markup($"[bold red]{message}[/]"));
    }

    public void ShowWarning(string message)
    {
        //AnsiConsole.Write(new Panel(new Markup($"[bold yellow]{message}[/]"))
        //{
        //    Header = new PanelHeader("Warning"),
        //    Border = BoxBorder.Rounded,
        //    BorderStyle = new Style(Color.Yellow)
        //});
        AnsiConsole.Write(new Markup($"[bold yellow]{message}[/]"));
    }

    public void ShowInfo(string message)
    {
        AnsiConsole.MarkupLine($"[dim]{message}[/]");
    }

    public void ShowSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[green]{message}[/]");
    }

    public void ShowHelp()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue);

        table.AddColumn("[bold]Command[/]");
        table.AddColumn("[bold]Description[/]");
        table.AddColumn("[bold]Example[/]");

        table.AddRow("[cyan]Natural Language[/]", "Describe what you want to do", "[dim]list video files[/]");
        table.AddRow("[cyan]help[/]", "Show this help message", "[dim]help[/]");
        table.AddRow("[cyan]vars[/]", "Show all variables", "[dim]vars[/]");
        table.AddRow("[cyan]alias <name> <cmd>[/]", "Create command alias", "[dim]alias ll dir[/]");
        table.AddRow("[cyan]config <key> <value>[/]", "Configure settings", "[dim]config theme dark[/]");
        table.AddRow("[cyan]status[/]", "Show system status", "[dim]status[/]");
        table.AddRow("[cyan]plugins[/]", "Show available plugins", "[dim]plugins[/]");
        table.AddRow("[cyan]clear[/]", "Clear the screen", "[dim]clear[/]");
        table.AddRow("[cyan]version[/]", "Show version info", "[dim]version[/]");
        table.AddRow("[cyan]exit[/]", "Exit AiShell", "[dim]exit[/]");

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold yellow]Cloud LLM Commands:[/]");
        AnsiConsole.MarkupLine("[dim]• gpt <request>  - Use GPT-4 for complex tasks[/]");
        AnsiConsole.MarkupLine("[dim]• claude <request> - Use Claude for analysis[/]");
        AnsiConsole.MarkupLine("[dim]• gemini <request> - Use Gemini for code tasks[/]");
        AnsiConsole.WriteLine();
    }

    public void ShowVariables(Dictionary<string, VariableEntry> variables)
    {
        if (!variables.Any())
        {
            ShowInfo("No variables defined yet.");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Aqua);

        table.AddColumn("[bold]Variable[/]");
        table.AddColumn("[bold]Value[/]");
        table.AddColumn("[bold]Category[/]");
        table.AddColumn("[bold]Last Used[/]");

        foreach (var var in variables.OrderBy(v => v.Key))
        {
            var value = var.Value.Value.Length > 50 
                ? var.Value.Value.Substring(0, 47) + "..." 
                : var.Value.Value;

            var category = var.Value.Category switch
            {
                "config" => "[blue]config[/]",
                "alias" => "[green]alias[/]",
                "system" => "[yellow]system[/]",
                _ => "[dim]user[/]"
            };

            table.AddRow(
                $"[cyan]{var.Key}[/]",
                $"[white]{value}[/]",
                category,
                $"[dim]{var.Value.LastUsed:yyyy-MM-dd HH:mm}[/]"
            );
        }

        AnsiConsole.Write(table);
    }

    public void ShowGoodbye()
    {
        var farewell = new FigletText("Goodbye!")
            .Centered()
            .Color(Color.Blue);

        AnsiConsole.Write(farewell);
        AnsiConsole.MarkupLine("[dim]Thank you for using AiShell![/]");
        AnsiConsole.WriteLine();
    }
}