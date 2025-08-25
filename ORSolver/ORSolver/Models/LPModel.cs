namespace ORSolver.Models;

public enum ProblemType { Maximize, Minimize }

public enum Relation { LessOrEqual, GreaterOrEqual, Equal }

public enum SignRestriction
{
    NonNegative, // '+'
    NonPositive, // '-'
    Free,        // 'urs'
    Integer,     // 'int' (accepted, not enforced here)
    Binary       // 'bin' (accepted, not enforced here)
}

public sealed class Constraint
{
    public double[] Coeffs { get; set; } = Array.Empty<double>();
    public Relation Relation { get; set; }
    public double RHS { get; set; }
}

public sealed class LPModel
{
    public ProblemType Type { get; set; }
    public double[] Objective { get; set; } = Array.Empty<double>(); // c
    public List<Constraint> Constraints { get; } = new();
    public SignRestriction[] Signs { get; set; } = Array.Empty<SignRestriction>();

    public int VarCount => Objective.Length;
    public int ConCount => Constraints.Count;

    public string ToPrettyString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("===== Parsed Model =====");
        sb.AppendLine($"Type: {Type}");
        sb.Append("Objective: ");
        for (int j = 0; j < Objective.Length; j++)
        {
            var c = Objective[j];
            sb.Append($"{(c>=0?"+":"")}{c} x{j+1} ");
        }
        sb.AppendLine();

        sb.AppendLine("Constraints:");
        for (int i = 0; i < Constraints.Count; i++)
        {
            var con = Constraints[i];
            for (int j = 0; j < Objective.Length; j++)
            {
                var a = con.Coeffs[j];
                sb.Append($"{(a>=0?"+":"")}{a} x{j+1} ");
            }
            sb.Append(con.Relation switch
            {
                Relation.LessOrEqual => "<=",
                Relation.GreaterOrEqual => ">=",
                Relation.Equal => "=",
                _ => "?"
            });
            sb.AppendLine($" {con.RHS}");
        }

        sb.Append("Sign restrictions: ");
        for (int j = 0; j < Signs.Length; j++)
        {
            var s = Signs[j] switch
            {
                SignRestriction.NonNegative => "+",
                SignRestriction.NonPositive => "-",
                SignRestriction.Free => "urs",
                SignRestriction.Integer => "int",
                SignRestriction.Binary => "bin",
                _ => "?"
            };
            sb.Append(s);
            if (j < Signs.Length - 1) sb.Append(' ');
        }
        sb.AppendLine();
        return sb.ToString();
    }
}