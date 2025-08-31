using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;
using ORSolver.Models;
using ORSolver.Math.Simplex;

namespace ORSolver.Math.Duality
{
    // Build dual of canonical primal: Max c^T x s.t. A x = b, x>=0
    // Dual: Min b^T y s.t. A^T y >= c, y free
    public static class DualBuilder
    {
        public static LPModel BuildDual(CanonicalizedModel cm)
        {
            var T = cm.Tableau;
            int m = T.GetLength(0) - 1;
            int n = T.GetLength(1) - 1;

            var A = Matrix<double>.Build.Dense(m, n);
            var b = Vector<double>.Build.Dense(m);
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++) A[i, j] = T[i, j];
                b[i] = T[i, n];
            }
            var c = Vector<double>.Build.Dense(n, j => cm.Costs[j]);

            // Build LPModel using your project’s classes
            var model = new LPModel
            {
                Type      = ProblemType.Minimize,
                Objective = b.ToArray(),
                Signs     = Enumerable.Repeat(SignRestriction.NonNegative, m).ToArray()
            };

            var AT = A.Transpose();
            for (int j = 0; j < n; j++)
                //model.Constraints.Add(new Constraint(AT.Row(j).ToArray(), c[j], Relation.GreaterOrEqual));
                {
                var coeffs = AT.Row(j).ToArray();

                // ↓↓↓ pick the property names that your Constraint actually exposes ↓↓↓
                var con = new Constraint
                {
                    // common names seen in this project style:
                    Coeffs   = coeffs,                  // <-- if your class uses this
                    Relation       = Relation.GreaterOrEqual, // <-- enum from your models
                    RHS  = c[j]                     // <-- sometimes named RHS or Rhs
                };

                model.Constraints.Add(con);
            }
            return model;
        }
    }
}
