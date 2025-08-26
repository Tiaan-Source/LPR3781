
using ORSolver.Models;
using ORSolver.Math.Simplex;
using System.Text;
using System;

namespace ORSolver.Math.Integer;

public sealed class CuttingPlaneSolver
{
    private readonly CanonicalFormBuilder _builder = new();
    private readonly PrimalSimplexSolver _simplex = new();

    public CuttingPlaneResult SolveWithGomory(LPModel model, int maxIterations = 30)
    {
        var iterations = new List<CuttingPlaneIteration>();
        var currentModel = _builder.Build(model);
        int iteration = 0;

        while (iteration < maxIterations)
        {
            iteration++;
            Console.WriteLine($"\n=== Cutting Plane Iteration {iteration} ===");

            try
            {
                var log = _simplex.Solve(currentModel);
                var objective = ExtractObjective(log);
                var solution = ExtractSolution(log, currentModel.VarNames);

                Console.WriteLine($"Objective: {objective:F3}");
                Console.WriteLine("Solution: " + string.Join(", ",
                    currentModel.VarNames.Select((name, i) => $"{name}={solution[i]:F3}")));

                // Check integrality
                bool allInteger = true;
                int mostFractionalRow = -1;
                double maxFractionality = 0;

                for (int i = 0; i < solution.Length; i++)
                {
                    var fractionality = System.Math.Abs(solution[i] - System.Math.Round(solution[i]));
                    if (fractionality > 1e-6)
                    {
                        allInteger = false;
                        if (fractionality > maxFractionality)
                        {
                            maxFractionality = fractionality;
                            mostFractionalRow = i;
                        }
                    }
                }

                iterations.Add(new CuttingPlaneIteration
                {
                    Iteration = iteration,
                    Canonical = currentModel,
                    Log = log,
                    Objective = objective,
                    Solution = solution
                });

                if (allInteger)
                {
                    Console.WriteLine("Integer solution found!");
                    return new CuttingPlaneResult
                    {
                        Objective = objective,
                        Solution = solution,
                        Iterations = iterations,
                        TotalIterations = iteration
                    };
                }

                // Generate Gomory cut
                if (mostFractionalRow >= 0)
                {
                    Console.WriteLine($"Generating Gomory cut for {currentModel.VarNames[mostFractionalRow]}");
                    var cut = GenerateGomoryCut(currentModel, log, mostFractionalRow);

                    // Add cut to model
                    var newModel = AddCutToModel(model, cut);
                    currentModel = _builder.Build(newModel);

                    Console.WriteLine($"Added cut: {CutToString(cut)}");
                }
            }
            catch (SimplexException ex)
            {
                Console.WriteLine("Infeasible solution");
                return new CuttingPlaneResult
                {
                    Objective = double.NegativeInfinity,
                    Solution = null,
                    Iterations = iterations,
                    TotalIterations = iteration,
                    IsInfeasible = true
                };
            }
        }

        Console.WriteLine("Maximum iterations reached");
        return new CuttingPlaneResult
        {
            Objective = iterations.Last().Objective,
            Solution = iterations.Last().Solution,
            Iterations = iterations,
            TotalIterations = maxIterations
        };
    }

    private Constraint GenerateGomoryCut(CanonicalizedModel cm, SimplexSolveLog log, int fractionalRow)
    {
        // Simplified Gomory cut generation
        // In practice, this would analyze the tableau to extract coefficients
        var solution = ExtractSolution(log, cm.VarNames);
        var fractionalValue = solution[fractionalRow];
        var floor = System.Math.Floor(fractionalValue);

        return new Constraint
        {
            Coeffs = Enumerable.Range(0, cm.DecisionsCount)
                .Select(j => j == fractionalRow ? 1.0 : 0.0)
                .ToArray(),
            Relation = Relation.LessOrEqual,
            RHS = floor
        };
    }

    private LPModel AddCutToModel(LPModel original, Constraint cut)
    {
        var newModel = new LPModel
        {
            Type = original.Type,
            Objective = original.Objective,
            Signs = original.Signs
        };

        // Copy original constraints
        foreach (var constraint in original.Constraints)
        {
            newModel.Constraints.Add(constraint);
        }

        // Add new cut
        newModel.Constraints.Add(cut);

        return newModel;
    }

    private string CutToString(Constraint cut)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < cut.Coeffs.Length; i++)
        {
            if (cut.Coeffs[i] != 0)
            {
                sb.Append($"{(cut.Coeffs[i] > 0 ? "+" : "")}{cut.Coeffs[i]:F2}x{i} ");
            }
        }
        sb.Append($"{cut.Relation} {cut.RHS}");
        return sb.ToString();
    }

    private static double ExtractValue(SimplexSolveLog log, string varName)
    {
        var report = log.FinalReportRounded3().Split('\n');
        foreach (var line in report)
        {
            if (line.StartsWith(varName))
            {
                var parts = line.Split('=');
                if (parts.Length == 2 && double.TryParse(parts[1], out var val))
                    return val;
            }
        }
        return 0.0;
    }

    private static double ExtractObjective(SimplexSolveLog log)
    {
        var report = log.FinalReportRounded3().Split('\n');
        foreach (var line in report)
        {
            if (line.StartsWith("Objective"))
            {
                var parts = line.Split('=');
                if (parts.Length == 2 && double.TryParse(parts[1], out var val))
                    return val;
            }
        }
        return double.NegativeInfinity;
    }

    private static double[] ExtractSolution(SimplexSolveLog log, string[] varNames)
    {
        var solution = new double[varNames.Length];
        for (int i = 0; i < varNames.Length; i++)
        {
            solution[i] = ExtractValue(log, varNames[i]);
        }
        return solution;
    }
}

public class CuttingPlaneResult
{
    public double Objective { get; set; }
    public double[]? Solution { get; set; }
    public List<CuttingPlaneIteration> Iterations { get; set; } = new();
    public int TotalIterations { get; set; }
    public bool IsInfeasible { get; set; }
}

public class CuttingPlaneIteration
{
    public int Iteration { get; set; }
    public CanonicalizedModel? Canonical { get; set; }
    public SimplexSolveLog? Log { get; set; }
    public double Objective { get; set; }
    public double[]? Solution { get; set; }
}