using System;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;
using ORSolver.Output;
using ORSolver.Models;

namespace ORSolver.Math.Simplex
{
    /// Revised Primal Simplex that logs Product-Form + Price-Out each iter,
    /// and writes via ResultsExporter.
    public sealed class RevisedPrimalSimplexSolver
    {
        public SimplexSolveLog Solve(CanonicalizedModel cm, ResultExporter exporter, string iterPath)
        {
            var T0 = cm.Tableau;
            int m = T0.GetLength(0) - 1;
            int n = T0.GetLength(1) - 1;

            var A = Matrix<double>.Build.Dense(m, n);
            var b = Vector<double>.Build.Dense(m);
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++) A[i, j] = T0[i, j];
                b[i] = T0[i, n];
            }
            var c = Vector<double>.Build.Dense(n, j => cm.Costs[j]);
            var basis = (int[])cm.Basis.Clone();

            var log = new SimplexSolveLog(cm.VarNames, m, n + 1, cm.Costs, cm.IsMaximization);

            Matrix<double> BuildB()
            {
                var Bm = Matrix<double>.Build.Dense(m, m);
                for (int i = 0; i < m; i++) Bm.SetColumn(i, A.Column(basis[i]));
                return Bm;
            }

            exporter.AppendHeader(iterPath, "Revised Primal Simplex (Product-Form + Price-Out)");

            var B = BuildB();
            var Binv = B.Inverse();

            int iter = 0, iterSafety = 0, maxIter = 10000;
            while (true)
            {
                // y = (B^{-T}) c_B, r = c - A^T y, z = y^T b
                var cB = Vector<double>.Build.Dense(m, i => c[basis[i]]);
                var y  = Binv.TransposeThisAndMultiply(cB);
                var r  = c - A.TransposeThisAndMultiply(y);
                double z = y.DotProduct(b);

                // enter: first r_j > 1e-9 and non-basic (Bland)
                int entering = -1;
                for (int j = 0; j < n; j++)
                {
                    if (Array.IndexOf(basis, j) >= 0) continue;
                    if (r[j] > 1e-9) { entering = j; break; }
                }

                // PRICE-OUT log
                exporter.AppendPriceOut(iterPath, new PriceOutIteration(
                    Iteration: iter,
                    EnteringCol: entering,
                    DualY: y.ToArray(),
                    ReducedCosts: r.ToArray(),
                    Z: z));

                log.EnteringHistory.Add(entering);
                log.BasisHistory.Add((int[])basis.Clone());

                if (entering == -1)
                {
                    // optimal: check artificials for infeasibility
                    var xB = Binv * b;
                    var x  = Vector<double>.Build.Dense(n);
                    for (int i = 0; i < m; i++) x[basis[i]] = xB[i];

                    int startArt = cm.DecisionsCount + cm.SlackCount;
                    for (int j = 0; j < cm.ArtificialCount; j++)
                    {
                        if (x[startArt + j] > 1e-8)
                        {
                            exporter.AppendFooterInfeasible(iterPath);
                            throw new SimplexException("Infeasible: artificial variable remains positive.", log);
                        }
                    }
                    exporter.AppendFooterOptimal(iterPath, z);
                    return log; // OPTIMAL
                }

                // Direction & ratio
                var d       = Binv * A.Column(entering);
                var xBcurr  = Binv * b;

                double theta = double.PositiveInfinity;
                int leavingPos = -1;
                for (int i = 0; i < m; i++)
                {
                    if (d[i] > 1e-9)
                    {
                        double ratio = xBcurr[i] / d[i];
                        if (ratio < theta - 1e-12 || (System.Math.Abs(ratio - theta) <= 1e-12 && basis[i] < (leavingPos >=0 ? basis[leavingPos] : int.MaxValue)))
                        {
                            theta = ratio; leavingPos = i;
                        }
                    }
                }
                if (leavingPos == -1)
                {
                    exporter.AppendFooterUnbounded(iterPath);
                    throw new SimplexException("Unbounded problem.", log);
                }

                // PRODUCT-FORM log
                var xBnext = xBcurr - d.Multiply(theta);
                exporter.AppendProductForm(iterPath, new ProductFormIteration(
                    Iteration: iter,
                    LeavingBasisPos: leavingPos,
                    DirectionD: d.ToArray(),
                    XbBefore: xBcurr.ToArray(),
                    StepSize: theta,
                    XbAfter: xBnext.ToArray()));

                log.LeavingHistory.Add(leavingPos);

                // pivot (recompute Binv for clarity)
                basis[leavingPos] = entering;
                B = BuildB();
                Binv = B.Inverse();

                iter++;
                if (++iterSafety > maxIter)
                    throw new SimplexException("Iteration limit reached.", log);
            }
        }
    }
}
