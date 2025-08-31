using ORSolver.Models;
using ORSolver.Parsing;
using ORSolver.Math.Simplex;
using ORSolver.Output;
using ORSolver.Math.Integer;
using ORSolver.Utilities;

namespace ORSolver;

internal class Program
{
    private static LPModel? _model;
    private static CanonicalizedModel? _canonical;
    private static SimplexSolveLog? _lastLog;
    private static string? _currentInputBaseName;

    static void Main(string[] args)
    {
        bool running = true;

        while (running)
        {
            RenderHeaderAndMenu();
            var input = Console.ReadLine();
            Console.WriteLine();

            switch (input)
            {
                case "1":
                    Console.Clear();
                    LoadFile();
                    PauseReturn();
                    break;

                case "2":
                    Console.Clear();
                    ShowParsed();
                    PauseReturn();
                    break;

                case "3":
                    Console.Clear();
                    ShowCanonical();
                    PauseReturn();
                    break;

                case "4":
                    Console.Clear();
                    SolveSimplex();
                    PauseReturn();
                    break;

                case "5":
                    Console.Clear();
                    RunIntegerSolversMenu();
                    PauseReturn();
                    break;

                case "6":
                    Console.Clear();
                    Export();
                    PauseReturn();
                    break;

                case "7":
                    Console.Clear();
                    ShowHelp();
                    PauseReturn();
                    break;

                case "0":
                    running = false;
                    break;

                default:
                    Console.WriteLine("Unknown option.");
                    PauseReturn();
                    break;
            }
        }
    }

    private static void RenderHeaderAndMenu()
    {
        Console.Clear();
        Console.WriteLine("=====================================================");
        Console.WriteLine("  solve.exe — Linear/Integer Programming (Person 1)  ");
        Console.WriteLine("=====================================================\n");

        Console.WriteLine("Menu:");
        Console.WriteLine(" 1) Load input file");
        Console.WriteLine(" 2) Show parsed model summary");
        Console.WriteLine(" 3) Show canonical form");
        Console.WriteLine(" 4) Solve (Primal Simplex)");
        Console.WriteLine(" 5) Solve (Integer Programming)");
        Console.WriteLine(" 6) Export results to output file");
        Console.WriteLine(" 7) Help (input format)");
        Console.WriteLine(" 0) Exit");
        Console.Write("Select option: ");
    }

    private static void PauseReturn()
    {
        Console.WriteLine();
        Console.Write("Press any key to return to the menu...");
        Console.ReadKey(true);
        Console.Clear();
    }

    static void LoadFile()
    {
        string samplesPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\Samples"));

        if (!Directory.Exists(samplesPath))
        {
            Console.WriteLine($"Samples folder not found: {samplesPath}");
            return;
        }

        var files = Directory.GetFiles(samplesPath, "*.txt");
        if (files.Length == 0)
        {
            Console.WriteLine("No .txt files found in Samples folder.");
            return;
        }

        Console.WriteLine("Available sample files:");
        for (int i = 0; i < files.Length; i++)
        {
            Console.WriteLine($" {i + 1}) {Path.GetFileName(files[i])}");
        }
        Console.WriteLine(" 0) Cancel / Back to menu");

        Console.Write("\nSelect a file: ");
        if (!int.TryParse(Console.ReadLine(), out int choice) || choice < 0 || choice > files.Length)
        {
            Console.WriteLine("Invalid choice.");
            return;
        }

        if (choice == 0)
        {
            Console.WriteLine("Cancelled, returning to menu.");
            return;
        }

        string path = files[choice - 1];

        try
        {
            var text = File.ReadAllText(path);
            _model = InputModelParser.Parse(text);
            _canonical = null;
            _lastLog = null;

            _currentInputBaseName = Path.GetFileNameWithoutExtension(path);

            Console.WriteLine($"Model successfully loaded from {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading file: {ex.Message}");
        }
    }

    static void ShowParsed()
    {
        if (_model == null)
        {
            Console.WriteLine("Load a model first.");
            return;
        }
        Console.WriteLine(_model.ToPrettyString());
    }

    static void ShowCanonical()
    {
        if (_model == null)
        {
            Console.WriteLine("Load a model first.");
            return;
        }

        try
        {
            var builder = new CanonicalFormBuilder();
            _canonical = builder.Build(_model);
            Console.WriteLine(_canonical.CanonicalText);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Canonicalization error: {ex.Message}");
        }
    }

    static void SolveSimplex()
    {
        if (_model == null)
        {
            Console.WriteLine("Load a model first.");
            return;
        }
        try
        {
            var builder = new CanonicalFormBuilder();
            _canonical = builder.Build(_model);

            var solver = new PrimalSimplexSolver();
            var log = solver.Solve(_canonical);
            _lastLog = log;

            Console.WriteLine("===== Optimal Solution (Primal Simplex) =====");
            Console.WriteLine(log.FinalReportRounded3());
        }
        catch (SimplexException sex)
        {
            Console.WriteLine($"Simplex status: {sex.Message}");
            if (sex.Log != null)
            {
                _lastLog = sex.Log;
                Console.WriteLine("--- Last tableau before failure ---");
                Console.WriteLine(sex.Log.LatestTableauAsText(3));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Solve error: {ex.Message}");
        }
    }

    private static void RunIntegerSolversMenu()
    {
        if (_model == null)
        {
            Console.WriteLine("Load a model first.");
            return;
        }

        Console.WriteLine("\n=== Integer Programming Solvers ===");
        Console.WriteLine("Choose an integer solver:");
        Console.WriteLine("1) Branch & Bound");
        Console.WriteLine("2) Cutting Plane (Gomory)");
        Console.WriteLine("3) Knapsack");
        Console.Write("Your choice: ");
        string? solverChoice = Console.ReadLine();

        Directory.CreateDirectory("out");
        var exporter = new ResultExporter();

        if (solverChoice == "1")
        {
            Console.WriteLine("\nRunning Branch & Bound...");
            try
            {
                var bnb = new BranchAndBoundSolver();
                var result = bnb.Solve(_model, maxNodes: 1000);

                Console.WriteLine("\n\n=== Branch & Bound Final Results ===");
                Console.WriteLine($"Best objective: {result.BestObjective:F3}");
                Console.WriteLine($"Best solution: {(result.BestSolution != null ? string.Join(", ", result.BestSolution.Select((v, i) => $"x{i + 1}={v:F3}")) : "None")}");
                Console.WriteLine($"Total nodes visited: {result.TotalNodes}");
                Console.WriteLine($"Integer nodes found: {result.VisitedNodes.Count(n => n.IsInteger && !n.IsInfeasible)}");
                Console.WriteLine($"Infeasible nodes: {result.VisitedNodes.Count(n => n.IsInfeasible)}");

                // Display summary table
                Console.WriteLine("\n=== Node Summary ===");
                foreach (var row in result.NodeTable)
                {
                    Console.WriteLine($"Node {row.NodeId}: {row.Status}, Obj: {row.Objective:F3}, Branch: {row.BranchVariable}");
                }

                // Export node logs
                int exported = 0;
                foreach (var node in result.VisitedNodes)
                {
                    if (node.Canonical != null && node.Log != null)
                    {
                        var file = Path.Combine("out", $"node_{node.NodeId}.txt");
                        exporter.Export(file, _model, node.Canonical, node.Log);
                        exported++;
                    }
                }
                Console.WriteLine($"Exported {exported} node logs to 'out' folder");

                _lastLog = result.VisitedNodes.LastOrDefault(n => n.Log != null)?.Log;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Branch & Bound failed: {ex.Message}");
            }
        }
        else if (solverChoice == "2")
        {
            // Cutting Plane implementation remains the same
            // ...
        }
        else if (solverChoice == "3")
        {
            Console.WriteLine("\nRunning Knapsack Branch & Bound...");

            try
            {
                var knapsack = new KnapsackSolver();
                var result = knapsack.Solve(_model);

                Console.WriteLine("1) Export results to output file");
                string? choice = Console.ReadLine();

                if (choice == "1")
                {
                    string resultsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\Results"));
                    if (!Directory.Exists(resultsPath))
                    {
                        Directory.CreateDirectory(resultsPath);
                    }

                    string baseName = _currentInputBaseName + "_knapsack_result";
                    string targetPath = Path.Combine(resultsPath, baseName + ".txt");
                    targetPath = MakeUniquePath(targetPath); 

                    try
                    {
                        var knapsackExporter = new KnapsackResultExporter();
                        knapsackExporter.Export(targetPath, _model, result);
                        Console.WriteLine($"Exported to: {targetPath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Export error: {ex.Message}");
                    }
                }
                else
                {
                    Console.Clear();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Knapsack Solver failed: {ex.Message}");
            }
        }

        else
        {
            Console.WriteLine("Invalid choice. Returning to main menu.");
        }
    }

    static void Export()
    {
        if (_model == null)
        {
            Console.WriteLine("Load a model first.");
            return;
        }

        if (_currentInputBaseName == null)
        {
            Console.WriteLine("No input file name is associated with this session. Please load a file again.");
            return;
        }

        if (_canonical == null)
        {
            Console.WriteLine("Canonical form not built yet. Building now...");
            var builder = new CanonicalFormBuilder();
            _canonical = builder.Build(_model);
        }

        string resultsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\Results"));
        if (!Directory.Exists(resultsPath))
        {
            Directory.CreateDirectory(resultsPath);
        }

        string baseName = _currentInputBaseName + "_result";
        string targetPath = Path.Combine(resultsPath, baseName + ".txt");
        targetPath = MakeUniquePath(targetPath);

        try
        {
            var exporter = new ResultExporter();
            exporter.Export(targetPath, _model, _canonical!, _lastLog);
            Console.WriteLine($"Exported to: {targetPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Export error: {ex.Message}");
        }
    }

    static string MakeUniquePath(string fullPath)
    {
        if (!File.Exists(fullPath)) return fullPath;

        string dir = Path.GetDirectoryName(fullPath)!;
        string name = Path.GetFileNameWithoutExtension(fullPath);
        string ext = Path.GetExtension(fullPath);

        int i = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            i++;
        } while (File.Exists(candidate));

        return candidate;
    }

    static void ShowHelp()
    {
        Console.WriteLine("""
INPUT TEXT FILE FORMAT (one space between items):

Line 1:  max|min  then signed objective coefficients
Example:
  max +2 +3 +3 +5 +2 +4

Next lines: constraints    [signed coeffs]  (<=|=|>=)  RHS
Example:
  +11 +8 +6 +14 +10 +10 <= 40

Last line: sign restrictions per variable in objective order
Use tokens: +  -  urs  int  bin
Example:
  bin bin bin bin bin bin

Notes:
- This parser supports any number of variables and constraints.
- Use '.' for decimal. Do not include commas.
- Extra blank lines are ignored.
""");
    }
}