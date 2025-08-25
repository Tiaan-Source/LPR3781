using ORSolver.Models;
using System.Text;
using System.Linq;

namespace ORSolver.Math.Simplex;

public sealed class CanonicalizedModel
{
    public string CanonicalText { get; set; } = "";
    public double[,] Tableau { get; set; } = new double[0, 0];   
    public int[] Basis { get; set; } = Array.Empty<int>();        
    public string[] VarNames { get; set; } = Array.Empty<string>();

    public int OriginalVarCount { get; set; }
    public int SlackCount { get; set; }
    public int ArtificialCount { get; set; }
    public bool IsMaximization { get; set; } = true;
    public double BigM { get; set; } = 1e6;
    public int ConstraintCount { get; set; }

   
    public double[] Costs { get; set; } = Array.Empty<double>();  
    public int DecisionsCount { get; set; }                       
}

public sealed class CanonicalFormBuilder
{
    public CanonicalizedModel Build(LPModel model)
    {
       
        bool isMax = model.Type == ProblemType.Maximize;

        
        var c = (double[])model.Objective.Clone();
        if (!isMax)
        {
            for (int j = 0; j < c.Length; j++) c[j] = -c[j];
        }


       
        var A = new List<double[]>();
        var b = new List<double>();
        var relations = new List<Relation>();
        foreach (var con in model.Constraints)
        {
            A.Add((double[])con.Coeffs.Clone());
            b.Add(con.RHS);
            relations.Add(con.Relation);
        }

       
        var expandedC = new List<double>();
        var mapping = new List<string>();
        for (int j = 0; j < c.Length; j++)
        {
            switch (model.Signs[j])
            {
                case SignRestriction.NonNegative:
                case SignRestriction.Integer:
                case SignRestriction.Binary:
                    expandedC.Add(c[j]);
                    mapping.Add($"x{j + 1}");
                    break;

                case SignRestriction.NonPositive:
                    
                    expandedC.Add(-c[j]);
                    mapping.Add($"x{j + 1}(-)");
                    for (int i = 0; i < A.Count; i++) A[i][j] = -A[i][j];
                    break;

                case SignRestriction.Free:
                    
                    expandedC.Add(c[j]); mapping.Add($"x{j + 1}+");
                    expandedC.Add(-c[j]); mapping.Add($"x{j + 1}-");
                    for (int i = 0; i < A.Count; i++)
                    {
                        var old = A[i].ToList();
                        var val = old[j];
                        old[j] = val;
                        old.Insert(j + 1, -val);
                        A[i] = old.ToArray();
                    }
                    break;
            }
        }

        int n = expandedC.Count;   
        int m = A.Count;          

        var normA = new List<double[]>(m);
        var normB = new double[m];
        var normRel = new Relation[m];
        for (int i = 0; i < m; i++)
        {
            var row = (double[])A[i].Clone();
            var rhs = b[i];
            var rel = relations[i];

           
            if (rhs < 0)
            {
                for (int j = 0; j < row.Length; j++) row[j] = -row[j];
                rhs = -rhs;
                rel = rel == Relation.LessOrEqual ? Relation.GreaterOrEqual
                     : rel == Relation.GreaterOrEqual ? Relation.LessOrEqual
                     : Relation.Equal;
            }

            normA.Add(row);
            normB[i] = rhs;
            normRel[i] = rel; 
        }


     
        int slackCount = 0;
        int artCount = 0;

        for (int i = 0; i < m; i++)
        {
            if (normRel[i] == Relation.LessOrEqual) slackCount += 1;
            else if (normRel[i] == Relation.Equal) artCount += 1;
            else if (normRel[i] == Relation.GreaterOrEqual) { slackCount += 1; artCount += 1; }
        }

      
        var varNames = new List<string>(mapping);
        int totalCols = n + slackCount + artCount + 1;  
        double[,] tableau = new double[m + 1, totalCols];

        
        for (int i = 0; i < m; i++)
        {
            for (int j = 0; j < n; j++) tableau[i, j] = normA[i][j];
            tableau[i, totalCols - 1] = normB[i];
        }

        
        int slackIndex = n;
        int artIndex = n + slackCount;
        int rowSlackCursor = 0;
        var basis = new int[m];

        for (int i = 0; i < m; i++)
        {
            if (normRel[i] == Relation.LessOrEqual)
            {
                int sCol = slackIndex + rowSlackCursor;
                tableau[i, sCol] = 1.0;               
                basis[i] = sCol;
                varNames.Add($"s{rowSlackCursor + 1}");
                rowSlackCursor++;
            }
            else if (normRel[i] == Relation.Equal)
            {
                int aCol = artIndex++;
                tableau[i, aCol] = 1.0;               
                basis[i] = aCol;
                varNames.Add($"a{aCol - (n + slackCount) + 1}");
            }
            else if (normRel[i] == Relation.GreaterOrEqual)
            {
              
                int sCol = slackIndex + rowSlackCursor;
                tableau[i, sCol] = -1.0;              
                varNames.Add($"s{rowSlackCursor + 1}");
                rowSlackCursor++;

                int aCol = artIndex++;
                tableau[i, aCol] = 1.0;               
                basis[i] = aCol;
                varNames.Add($"a{aCol - (n + slackCount) + 1}");
            }
        }


        
        double baseScale = expandedC.Select(v => System.Math.Abs(v)).DefaultIfEmpty(1.0).Max();
        double rhsScale = normB.Select(v => System.Math.Abs(v)).DefaultIfEmpty(1.0).Max();
        double coefScale = normA.SelectMany(r => r).Select(v => System.Math.Abs(v)).DefaultIfEmpty(1.0).Max();
        double scale = System.Math.Max(1.0, System.Math.Max(baseScale, System.Math.Max(rhsScale, coefScale)));
        double BigM = 1e6 * scale;

        var fullCosts = new double[n + slackCount + artCount];
        for (int j = 0; j < n; j++) fullCosts[j] = expandedC[j]; 
        for (int j = n; j < n + slackCount; j++) fullCosts[j] = 0.0; 
        for (int j = n + slackCount; j < n + slackCount + artCount; j++) fullCosts[j] = -BigM;


        
        for (int j = 0; j < totalCols; j++) tableau[m, j] = 0.0;

        double[,] Bmat = new double[m, m];
        for (int i = 0; i < m; i++)
            for (int k = 0; k < m; k++)
                Bmat[i, k] = tableau[i, basis[k]];

        double[,] Binv = (m > 0) ? Invert(Bmat) : new double[0, 0];

        double[] cB = new double[m];
        for (int k = 0; k < m; k++) cB[k] = fullCosts[basis[k]];

        double[] yT = (m > 0) ? MultiplyRowVectorByMatrix(cB, Binv) : Array.Empty<double>();

        
        for (int j = 0; j < totalCols - 1; j++)
        {
            double sum = 0.0;
            for (int i = 0; i < m; i++) sum += yT[i] * tableau[i, j];
            tableau[m, j] = fullCosts[j] - sum;
        }
        
        double zVal = 0.0;
        for (int i = 0; i < m; i++) zVal += yT[i] * tableau[i, totalCols - 1];
        tableau[m, totalCols - 1] = zVal;

        
        var sb = new StringBuilder();
        sb.AppendLine("===== Canonical Form (for Primal Simplex) =====");
        sb.AppendLine("Maximize: z = " + string.Join(" ", expandedC.Select((v, j) =>
            $"{(v >= 0 ? "+" : "")}{v} {varNames.ElementAtOrDefault(j) ?? $"x{j + 1}"}")));
        sb.AppendLine("Subject to:");
        for (int i = 0, sId = 1, aId = 1; i < m; i++)
        {
            var rowTerms = new List<string>();
            for (int j = 0; j < n; j++)
            {
                double val = normA[i][j];
                rowTerms.Add($"{(val >= 0 ? "+" : "")}{val} {varNames.ElementAtOrDefault(j) ?? $"x{j + 1}"}");
            }
            if (normRel[i] == Relation.LessOrEqual) rowTerms.Add($"+1 s{sId++}");
            else if (normRel[i] == Relation.Equal) rowTerms.Add($"+1 a{aId++}");
            sb.AppendLine("  " + string.Join(" ", rowTerms) + $" = {normB[i]}");
        }
        sb.AppendLine("with all introduced variables nonnegative; integer/binary restrictions (if any) kept for reference only.");
        sb.AppendLine();

 
        return new CanonicalizedModel
        {
            CanonicalText = sb.ToString(),
            Tableau = tableau,
            Basis = basis,
            VarNames = varNames.ToArray(),
            OriginalVarCount = model.VarCount,
            SlackCount = slackCount,
            ArtificialCount = artCount,
            IsMaximization = isMax,
            BigM = BigM,
            ConstraintCount = m,

            Costs = fullCosts,
            DecisionsCount = n
        };
    }

    // --------- helpers ---------

    private static double[,] Invert(double[,] A)
    {
        int n = A.GetLength(0);
        if (n == 0) return new double[0, 0];
        if (A.GetLength(1) != n) throw new Exception("Matrix must be square.");

        var M = (double[,])A.Clone();
        var I = new double[n, n];
        for (int i = 0; i < n; i++) I[i, i] = 1.0;

        for (int col = 0; col < n; col++)
        {
            int pivot = col;
            double best = System.Math.Abs(M[pivot, col]);
            for (int i = col + 1; i < n; i++)
            {
                double v = System.Math.Abs(M[i, col]);
                if (v > best) { best = v; pivot = i; }
            }
            if (best < 1e-12) throw new Exception("Singular basis.");

            if (pivot != col)
            {
                SwapRows(M, col, pivot);
                SwapRows(I, col, pivot);
            }

            double p = M[col, col];
            for (int j = 0; j < n; j++) { M[col, j] /= p; I[col, j] /= p; }

            for (int i = 0; i < n; i++)
            {
                if (i == col) continue;
                double f = M[i, col];
                if (System.Math.Abs(f) > 1e-12)
                {
                    for (int j = 0; j < n; j++)
                    {
                        M[i, j] -= f * M[col, j];
                        I[i, j] -= f * I[col, j];
                    }
                }
            }
        }
        return I;
    }

    private static void SwapRows(double[,] M, int r1, int r2)
    {
        int n = M.GetLength(1);
        for (int j = 0; j < n; j++)
            (M[r1, j], M[r2, j]) = (M[r2, j], M[r1, j]);
    }

    private static double[] MultiplyRowVectorByMatrix(double[] row, double[,] M)
    {
        int m = M.GetLength(0), n = M.GetLength(1);
        if (m == 0) return Array.Empty<double>();
        if (row.Length != m) throw new Exception("Dimension mismatch in MultiplyRowVectorByMatrix.");
        var r = new double[n];
        for (int j = 0; j < n; j++)
        {
            double s = 0.0;
            for (int i = 0; i < m; i++) s += row[i] * M[i, j];
            r[j] = s;
        }
        return r;
    }
}
