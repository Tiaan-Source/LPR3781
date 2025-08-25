using ORSolver.Models;

namespace ORSolver.Parsing;

public static class InputModelParser
{
    public static LPModel Parse(string content)
    {
        var lines = content
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        if (lines.Count < 2)
            throw new Exception("File must contain at least objective line and a sign-restrictions line.");

    
        var signsLine = lines.Last();
        lines.RemoveAt(lines.Count - 1);

    
        var objLine = lines[0];
        lines.RemoveAt(0);

        var model = new LPModel();
        var objTokens = objLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (objTokens.Length < 2)
            throw new Exception("Objective line must include type and at least one coefficient.");

        model.Type = objTokens[0].ToLower() switch
        {
            "max" => ProblemType.Maximize,
            "min" => ProblemType.Minimize,
            _ => throw new Exception("First token must be 'max' or 'min'.")
        };

        var objCoeffs = new List<double>();
        for (int i = 1; i < objTokens.Length; i++)
        {
            objCoeffs.Add(ParseSignedNumber(objTokens[i]));
        }
        model.Objective = objCoeffs.ToArray();

        foreach (var raw in lines)
        {
            var tokens = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < model.VarCount + 2)
                throw new Exception("Constraint line too short.");

            var coeffs = new double[model.VarCount];
            for (int j = 0; j < model.VarCount; j++)
                coeffs[j] = ParseSignedNumber(tokens[j]);

            var relTok = tokens[model.VarCount];
            var relation = relTok switch
            {
                "<=" => Relation.LessOrEqual,
                ">=" => Relation.GreaterOrEqual,
                "="  => Relation.Equal,
                _ => throw new Exception($"Unknown relation '{relTok}'. Use <=, >=, or =.")
            };

            if (!double.TryParse(tokens[model.VarCount + 1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var rhs))
            {
                rhs = ParseSignedNumber(tokens[model.VarCount + 1]);
            }

            model.Constraints.Add(new Constraint { Coeffs = coeffs, Relation = relation, RHS = rhs });
        }

        
        var signTokens = signsLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (signTokens.Length != model.VarCount)
            throw new Exception("Sign restrictions count must match number of variables.");

        var signs = new SignRestriction[model.VarCount];
        for (int j = 0; j < signTokens.Length; j++)
        {
            signs[j] = signTokens[j].ToLower() switch
            {
                "+" => SignRestriction.NonNegative,
                "-" => SignRestriction.NonPositive,
                "urs" => SignRestriction.Free,
                "int" => SignRestriction.Integer,
                "bin" => SignRestriction.Binary,
                _ => throw new Exception($"Unknown sign restriction '{signTokens[j]}'.")
            };
        }
        model.Signs = signs;

        return model;
    }

    private static double ParseSignedNumber(string token)
    {
     
        if (double.TryParse(token, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var val))
            return val;

        
        if (token.StartsWith('+') || token.StartsWith('-'))
        {
            var body = token.Substring(1);
            if (double.TryParse(body, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var abs))
                return token[0] == '-' ? -abs : abs;
        }
        throw new Exception($"Invalid numeric token '{token}'.");
    }
}