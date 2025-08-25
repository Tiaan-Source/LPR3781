using System.Text;
using ORSolver.Models;
using ORSolver.Math.Simplex;

namespace ORSolver.Output;

public sealed class ResultExporter
{
    public void Export(string path, LPModel model, CanonicalizedModel cm, SimplexSolveLog? log)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var sw = new StreamWriter(path, false, Encoding.UTF8);

        sw.WriteLine("=====================================================");
        sw.WriteLine(" solve.exe — Linear/Integer Programming (Person 1)");
        sw.WriteLine("=====================================================");
        sw.WriteLine();

       
        sw.WriteLine(cm.CanonicalText.TrimEnd());
        sw.WriteLine();

        if (log != null && log.Tableaus.Count > 0)
        {
            for (int k = 0; k < log.Tableaus.Count; k++)
            {
                sw.WriteLine($"===== Tableau Iteration {k} =====");

                if (k > 0)
                {
                    int ent = log.EnteringHistory[k - 1];
                    int lev = log.LeavingHistory[k - 1];
                    string enteringName = ent < log.VarNames.Length ? log.VarNames[ent] : $"v{ent + 1}";
                    sw.WriteLine($"Entering: {enteringName} (col {ent + 1})  |  Leaving row: {lev + 1}");
                }

                sw.WriteLine(FormatTableau(log.Tableaus[k], log.VarNames, 3));
                sw.WriteLine();
            }

            sw.WriteLine("===== Final Report =====");
            sw.WriteLine(log.FinalReportRounded3().TrimEnd());
        }
        else
        {
  
            sw.WriteLine("===== Initial Tableau (no iterations logged) =====");
            sw.WriteLine(FormatTableau(cm.Tableau, cm.VarNames, 3));
            sw.WriteLine();

            sw.WriteLine("===== Final Report (no solve log) =====");
            sw.WriteLine(GenerateMinimalReport(cm).TrimEnd());
        }
    }



    private static string FormatTableau(double[,] T, string[] varNames, int round)
    {
        var sb = new StringBuilder();
        int rows = T.GetLength(0);
        int cols = T.GetLength(1);

        string fmt(double v) => System.Math.Round(v, round).ToString("0." + new string('0', round));

        sb.Append("    |");
        for (int j = 0; j < cols - 1; j++)
        {
            var name = j < varNames.Length ? varNames[j] : $"v{j + 1}";
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

   
    private static string GenerateMinimalReport(CanonicalizedModel cm)
    {
        var T = cm.Tableau;
        int rows = T.GetLength(0);
        int cols = T.GetLength(1);
        int rhs = cols - 1;

       
        var values = new Dictionary<string, double>();
        for (int j = 0; j < rhs; j++)
        {
            string name = j < cm.VarNames.Length ? cm.VarNames[j] : $"v{j + 1}";
            values[name] = 0.0;
        }

        
        for (int i = 0; i < rows - 1 && i < cm.Basis.Length; i++)
        {
            int bcol = cm.Basis[i];
            string bname = bcol < cm.VarNames.Length ? cm.VarNames[bcol] : $"v{bcol + 1}";
            values[bname] = T[i, rhs];
        }

        
        double z;
        if (cm.Costs != null && cm.Costs.Length > 0 && cm.DecisionsCount > 0)
        {
            z = 0.0;
            for (int j = 0; j < cm.DecisionsCount && j < cm.Costs.Length && j < cm.VarNames.Length; j++)
            {
                string name = cm.VarNames[j];
                double xj = values.TryGetValue(name, out var v) ? v : 0.0;
                z += cm.Costs[j] * xj;
            }
        }
        else
        {
            z = T[rows - 1, rhs];
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Objective z = {System.Math.Round(z, 3):0.000}");
        foreach (var kv in values)
            sb.AppendLine($"{kv.Key} = {System.Math.Round(kv.Value, 3):0.000}");
        return sb.ToString();
    }
}
