using System.Text;
using System;
using ORSolver.Models;   

namespace ORSolver.Math.Simplex;

public sealed class SimplexException : Exception
{
    public SimplexSolveLog? Log { get; }
    public SimplexException(string message, SimplexSolveLog? log = null) : base(message) { Log = log; }
}

public sealed class SimplexSolveLog
{
    public List<double[,]> Tableaus { get; } = new();
    public List<int> EnteringHistory { get; } = new();
    public List<int> LeavingHistory { get; } = new();
    public List<int[]> BasisHistory { get; } = new();
    public string[] VarNames { get; }
    public double[] Costs { get; }           
    public int M { get; }
    public int N { get; }
    public int TotalCols { get; }
    public int RhsCol => TotalCols - 1;
    public bool IsMaximization { get; }

    public SimplexSolveLog(string[] varNames, int m, int totalCols, double[] costs,bool isMaximization)
    {
        VarNames = varNames;
        M = m;
        TotalCols = totalCols;
        N = varNames.Length;
        Costs = costs;
        IsMaximization = isMaximization;
    }

    public string LatestTableauAsText(int round = 3)
    {
        if (Tableaus.Count == 0) return "(no tableaus)";
        var T = Tableaus.Last();
        return TableauToText(T, round);
    }

    public string TableauToText(double[,] T, int round = 3)
    {
        var sb = new StringBuilder();
        int rows = T.GetLength(0);
        int cols = T.GetLength(1);
        string fmt(double v) => System.Math.Round(v, round).ToString("0." + new string('0', round));

        sb.Append("    |");
        for (int j = 0; j < cols - 1; j++)
        {
            var name = j < VarNames.Length ? VarNames[j] : $"v{j + 1}";
            sb.Append($" {name,10}");
        }
        sb.Append($" | {"RHS",10}\n");
        sb.Append(new string('-', 14 + 12 * (cols - 1)) + "\n");

        for (int i = 0; i < rows; i++)
        {
            sb.Append(i == rows - 1 ? " z  |" : $" xB |");
            for (int j = 0; j < cols; j++)
            {
                sb.Append($" {fmt(T[i, j]),10}");
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    public string FinalReportRounded3()
    {
        if (Tableaus.Count == 0) return "(no iterations)";
        var T = Tableaus.Last();
        int rows = T.GetLength(0);
        int cols = T.GetLength(1);
        int rhs = cols - 1;

    
        var values = new Dictionary<string, double>();
        for (int j = 0; j < rhs; j++) values[VarNames[j]] = 0.0;

  
        var basis = BasisHistory.LastOrDefault() ?? Array.Empty<int>();
        for (int i = 0; i < rows - 1 && i < basis.Length; i++)
        {
            string name = basis[i] < VarNames.Length ? VarNames[basis[i]] : $"v{basis[i] + 1}";
            values[name] = T[i, rhs];
        }

        
        double z = 0.0;
        for (int j = 0; j < rhs && j < Costs.Length; j++)
        {
            var name = VarNames[j];
            double xj = values.TryGetValue(name, out var v) ? v : 0.0;
            z += Costs[j] * xj;
        }

       
        double reportedZ = IsMaximization ? z : -z;

       
        var sb = new StringBuilder();
        sb.AppendLine($"Objective z = {System.Math.Round(reportedZ, 3):0.000}");
        foreach (var kv in values)
            sb.AppendLine($"{kv.Key} = {System.Math.Round(kv.Value, 3):0.000}");
        return sb.ToString();
    }

}

public sealed class PrimalSimplexSolver
{
    private const double EPS = 1e-9;

    public SimplexSolveLog Solve(CanonicalizedModel cm)
    {
        var T = (double[,])cm.Tableau.Clone();
        int m = T.GetLength(0) - 1;  
        int cols = T.GetLength(1);
        int rhs = cols - 1;

        var log = new SimplexSolveLog(cm.VarNames, m, cols, cm.Costs, cm.IsMaximization);
        int[] basis = (int[])cm.Basis.Clone();

       
        log.Tableaus.Add((double[,])T.Clone());
        log.BasisHistory.Add((int[])basis.Clone());

        while (true)
        {
            int entering = ChooseEnteringVariable(T);
            if (entering == -1)
                break; 

            int leaving = ChooseLeavingRow(T, entering);
            if (leaving == -1)
                throw new SimplexException("Unbounded problem detected.", log);

            Pivot(T, leaving, entering);
            basis[leaving] = entering;

            log.Tableaus.Add((double[,])T.Clone());
            log.EnteringHistory.Add(entering);
            log.LeavingHistory.Add(leaving);
            log.BasisHistory.Add((int[])basis.Clone());
        }

        
        if (cm.ArtificialCount > 0)
        {
            int artStart = cm.VarNames.Length - cm.ArtificialCount;
            for (int i = 0; i < m; i++)
            {
                int b = log.BasisHistory.Last()[i];
                if (b >= artStart && b < cm.VarNames.Length)
                {
                    double val = T[i, rhs];
                    if (val > 1e-6) 
                        throw new SimplexException("Infeasible problem (artificial variable remains positive in basis).", log);
                }
            }
        }

        return log;
    }

    private static int ChooseEnteringVariable(double[,] T)
    {
        int rows = T.GetLength(0);
        int cols = T.GetLength(1);
        int rhs = cols - 1;

        int entering = -1;
        for (int j = 0; j < rhs; j++)
        {
            if (T[rows - 1, j] > 1e-9)
            {
                entering = j;
                break; 
            }
        }
        return entering;
    }

    private static int ChooseLeavingRow(double[,] T, int entering)
    {
        int rows = T.GetLength(0);
        int cols = T.GetLength(1);
        int rhs = cols - 1;

        const double EPS_COEF = 1e-9;
        const double TOL = 1e-12;

        double bestRatio = double.PositiveInfinity;
        int leaving = -1;

        for (int i = 0; i < rows - 1; i++)
        {
            double a = T[i, entering];
            if (a > EPS_COEF)
            {
                double ratio = T[i, rhs] / a;
                if (ratio < bestRatio - TOL ||
                   (System.Math.Abs(ratio - bestRatio) <= TOL && (leaving == -1 || i < leaving)))
                {
                    bestRatio = ratio;
                    leaving = i;
                }
            }
        }
        return leaving;
    }

    private static void Pivot(double[,] T, int pivotRow, int pivotCol)
    {
        int rows = T.GetLength(0);
        int cols = T.GetLength(1);

        double p = T[pivotRow, pivotCol];
        for (int j = 0; j < cols; j++) T[pivotRow, j] /= p;

        for (int i = 0; i < rows; i++)
        {
            if (i == pivotRow) continue;
            double factor = T[i, pivotCol];
            if (System.Math.Abs(factor) > 1e-12)
            {
                for (int j = 0; j < cols; j++)
                    T[i, j] -= factor * T[pivotRow, j];
            }
        }
    }
}
