
using ORSolver.Models;
using ORSolver.Math.Simplex;
using System.Text;
using System;

namespace ORSolver.Math.Integer;

public sealed class BranchAndBoundSolver
{
    private readonly CanonicalFormBuilder _builder = new();
    private readonly PrimalSimplexSolver _simplex = new();
    private int _nodeCount = 0;
    private SimplexSolveLog? _bestSolution;
    private double _bestObjective = double.NegativeInfinity;
    private List<BBNodeTableRow> _nodeTable = new();

    public BranchAndBoundResult Solve(LPModel model, int maxNodes = 1000)
    {
        _nodeCount = 0;
        _bestSolution = null;
        _bestObjective = double.NegativeInfinity;
        _nodeTable = new List<BBNodeTableRow>();

        Console.WriteLine("Starting Branch & Bound Algorithm...\n");
        PrintTableHeader();

        var root = _builder.Build(model);
        var visitedNodes = new List<BBNode>();

        var result = Branch(root, visitedNodes, maxNodes);

        PrintTableFooter();

        return new BranchAndBoundResult
        {
            BestObjective = _bestObjective,
            BestSolution = _bestSolution != null ? ExtractSolution(_bestSolution, root.VarNames) : null,
            VisitedNodes = visitedNodes,
            TotalNodes = _nodeCount,
            NodeTable = _nodeTable
        };
    }

    private void PrintTableHeader()
    {
        Console.WriteLine("================================================================================================================");
        Console.WriteLine("| Node | Depth |   Objective   |  Status  | Branch Variable | Branch Value |   Left   |  Right  | Best Bound |");
        Console.WriteLine("================================================================================================================");
    }

    private void PrintTableFooter()
    {
        Console.WriteLine("================================================================================================================");
    }

    private void PrintNodeTableRow(int nodeId, int depth, double objective, string status,
                                 string branchVar, double branchValue, string leftStatus,
                                 string rightStatus, double bestBound)
    {
        var row = new BBNodeTableRow
        {
            NodeId = nodeId,
            Depth = depth,
            Objective = objective,
            Status = status,
            BranchVariable = branchVar,
            BranchValue = branchValue,
            LeftStatus = leftStatus,
            RightStatus = rightStatus,
            BestBound = bestBound
        };

        _nodeTable.Add(row);

        Console.WriteLine($"| {nodeId,4} | {depth,5} | {objective,12:F3} | {status,-8} | {branchVar,-15} | {branchValue,12:F3} | {leftStatus,-8} | {rightStatus,-7} | {bestBound,10:F3} |");
    }

    private SimplexSolveLog? Branch(CanonicalizedModel cm, List<BBNode> visitedNodes, int maxNodes,
                                  List<Constraint>? branchConstraints = null, int depth = 0)
    {
        if (_nodeCount >= maxNodes)
            return null;

        _nodeCount++;
        var nodeId = _nodeCount;
        string branchVar = "-";
        double branchValue = 0;
        string leftStatus = "N/A";
        string rightStatus = "N/A";

        try
        {
            var log = _simplex.Solve(cm);
            var objective = ExtractObjective(log);

            var node = new BBNode
            {
                NodeId = nodeId,
                Depth = depth,
                Canonical = cm,
                Log = log,
                Objective = objective,
                IsInteger = true,
                BranchConstraints = branchConstraints ?? new List<Constraint>()
            };

            visitedNodes.Add(node);

            // Check if worse than current best
            if (objective < _bestObjective && cm.IsMaximization)
            {
                PrintNodeTableRow(nodeId, depth, objective, "FATHOMED", "-", 0, "-", "-", _bestObjective);
                return null;
            }

            // Check integrality
            bool allInteger = true;
            int branchingVarIndex = -1;
            double maxFractionality = 0;

            for (int j = 0; j < cm.DecisionsCount; j++)
            {
                var value = ExtractValue(log, cm.VarNames[j]);
                var fractionality = System.Math.Abs(value - System.Math.Round(value));

                if (fractionality > 1e-6)
                {
                    allInteger = false;
                    if (fractionality > maxFractionality)
                    {
                        maxFractionality = fractionality;
                        branchingVarIndex = j;
                    }
                }
            }

            if (allInteger)
            {
                if (objective > _bestObjective && cm.IsMaximization)
                {
                    _bestObjective = objective;
                    _bestSolution = log;
                }
                PrintNodeTableRow(nodeId, depth, objective, "INTEGER", "-", 0, "-", "-", _bestObjective);
                return log;
            }

            if (branchingVarIndex == -1)
            {
                PrintNodeTableRow(nodeId, depth, objective, "INVALID", "-", 0, "-", "-", _bestObjective);
                return null;
            }

            // Branching
            branchVar = cm.VarNames[branchingVarIndex];
            branchValue = ExtractValue(log, branchVar);
            var floor = System.Math.Floor(branchValue);
            var ceil = System.Math.Ceiling(branchValue);

            // Print node before branching
            PrintNodeTableRow(nodeId, depth, objective, "BRANCH", branchVar, branchValue, "PENDING", "PENDING", _bestObjective);

            // Left branch (<= floor)
            var leftConstraints = new List<Constraint>(branchConstraints ?? new List<Constraint>());
            leftConstraints.Add(new Constraint
            {
                Coeffs = Enumerable.Range(0, cm.DecisionsCount)
                    .Select(j => j == branchingVarIndex ? 1.0 : 0.0)
                    .ToArray(),
                Relation = Relation.LessOrEqual,
                RHS = floor
            });

            var leftModel = CloneWithConstraints(cm, leftConstraints);
            var leftResult = Branch(leftModel, visitedNodes, maxNodes, leftConstraints, depth + 1);
            leftStatus = leftResult != null ? "SOLVED" : "FATHOMED";

            // Right branch (>= ceil)
            var rightConstraints = new List<Constraint>(branchConstraints ?? new List<Constraint>());
            rightConstraints.Add(new Constraint
            {
                Coeffs = Enumerable.Range(0, cm.DecisionsCount)
                    .Select(j => j == branchingVarIndex ? 1.0 : 0.0)
                    .ToArray(),
                Relation = Relation.GreaterOrEqual,
                RHS = ceil
            });

            var rightModel = CloneWithConstraints(cm, rightConstraints);
            var rightResult = Branch(rightModel, visitedNodes, maxNodes, rightConstraints, depth + 1);
            rightStatus = rightResult != null ? "SOLVED" : "FATHOMED";

            // Update table row with final status
            UpdateNodeTableRow(nodeId, leftStatus, rightStatus);

            // Return best of both branches
            if (leftResult == null) return rightResult;
            if (rightResult == null) return leftResult;

            var leftObj = ExtractObjective(leftResult);
            var rightObj = ExtractObjective(rightResult);

            return leftObj >= rightObj ? leftResult : rightResult;
        }
        catch (SimplexException ex)
        {
            visitedNodes.Add(new BBNode
            {
                NodeId = nodeId,
                Depth = depth,
                Canonical = cm,
                Log = ex.Log,
                Objective = double.NegativeInfinity,
                IsInteger = false,
                BranchConstraints = branchConstraints ?? new List<Constraint>(),
                IsInfeasible = true
            });

            PrintNodeTableRow(nodeId, depth, double.NegativeInfinity, "INFEAS", branchVar, branchValue, leftStatus, rightStatus, _bestObjective);
            return null;
        }
    }

    private void UpdateNodeTableRow(int nodeId, string leftStatus, string rightStatus)
    {
        var row = _nodeTable.FirstOrDefault(r => r.NodeId == nodeId);
        if (row != null)
        {
            row.LeftStatus = leftStatus;
            row.RightStatus = rightStatus;

            // Find the row in the console and update it (this is simplified - in real UI you'd need to redraw)
            Console.SetCursorPosition(0, Console.CursorTop - (_nodeTable.Count - nodeId));
            Console.WriteLine($"| {row.NodeId,4} | {row.Depth,5} | {row.Objective,12:F3} | {"UPDATED",-8} | {row.BranchVariable,-15} | {row.BranchValue,12:F3} | {leftStatus,-8} | {rightStatus,-7} | {row.BestBound,10:F3} |");
        }
    }

    private CanonicalizedModel CloneWithConstraints(CanonicalizedModel cm, List<Constraint> constraints)
    {
        var newModel = new LPModel
        {
            Type = cm.IsMaximization ? ProblemType.Maximize : ProblemType.Minimize,
            Objective = cm.Costs.Take(cm.DecisionsCount).ToArray(),
            Signs = Enumerable.Repeat(SignRestriction.Integer, cm.DecisionsCount).ToArray()
        };

        // Add all constraints
        foreach (var constraint in constraints)
        {
            newModel.Constraints.Add(constraint);
        }

        return _builder.Build(newModel);
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

public class BranchAndBoundResult
{
    public double BestObjective { get; set; }
    public double[]? BestSolution { get; set; }
    public List<BBNode> VisitedNodes { get; set; } = new();
    public int TotalNodes { get; set; }
    public List<BBNodeTableRow> NodeTable { get; set; } = new();
}

public class BBNodeTableRow
{
    public int NodeId { get; set; }
    public int Depth { get; set; }
    public double Objective { get; set; }
    public string Status { get; set; } = "";
    public string BranchVariable { get; set; } = "";
    public double BranchValue { get; set; }
    public string LeftStatus { get; set; } = "";
    public string RightStatus { get; set; } = "";
    public double BestBound { get; set; }
}

public class BBNode
{
    public int NodeId { get; set; }
    public int Depth { get; set; }
    public CanonicalizedModel? Canonical { get; set; }
    public SimplexSolveLog? Log { get; set; }
    public double Objective { get; set; }
    public bool IsInteger { get; set; }
    public bool IsInfeasible { get; set; }
    public List<Constraint> BranchConstraints { get; set; } = new();
}