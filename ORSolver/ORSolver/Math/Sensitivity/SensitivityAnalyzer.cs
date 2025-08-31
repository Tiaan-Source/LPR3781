using System;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;

namespace ORSolver.Math.Sensitivity
{
    using ORSolver.Math.Simplex;

    // Post-optimality on final basis (Max form).
    public sealed class SensitivityAnalyzer
    {
        private readonly Matrix<double> _A;
        private readonly Vector<double> _b;
        private readonly Vector<double> _c;
        private readonly int[] _basis;
        private readonly Matrix<double> _Binv;
        private readonly Vector<double> _y;
        private readonly int _m, _n;

        public SensitivityAnalyzer(CanonicalizedModel cm, int[] basis)
        {
            var T = cm.Tableau;
            _m = T.GetLength(0) - 1;
            _n = T.GetLength(1) - 1;

            _A = Matrix<double>.Build.Dense(_m, _n);
            _b = Vector<double>.Build.Dense(_m);
            for (int i = 0; i < _m; i++)
            {
                for (int j = 0; j < _n; j++) _A[i, j] = T[i, j];
                _b[i] = T[i, _n];
            }
            _c = Vector<double>.Build.Dense(_n, j => cm.Costs[j]);
            _basis = (int[])basis.Clone();

            var B = Matrix<double>.Build.Dense(_m, _m);
            for (int i = 0; i < _m; i++) B.SetColumn(i, _A.Column(_basis[i]));
            _Binv = B.Inverse();

            var cB = Vector<double>.Build.Dense(_m, i => _c[_basis[i]]);
            _y     = _Binv.TransposeThisAndMultiply(cB);
        }

        public double[] ShadowPrices() => _y.ToArray();

        // Non-basic c_j range: keep r_j = c_j - a_j^T y <= 0 (Max). Decrease is unbounded.
        public (double decrease, double increase) ObjCoeffRangeNonBasic(int j)
        {
            if (Array.IndexOf(_basis, j) >= 0) throw new ArgumentException("j is basic.");
            double rj = _c[j] - _A.Column(j).DotProduct(_y);
            return (double.NegativeInfinity, -rj);
        }

        // Basic c_bi range: keep all non-basic r_j(Δ) <= 0 → bounds on Δ.
        public (double decrease, double increase) ObjCoeffRangeBasic(int basisPos)
        {
            var ei = Vector<double>.Build.Dense(_m, k => k == basisPos ? 1.0 : 0.0);
            var w  = _Binv.TransposeThisAndMultiply(ei);

            double lower = double.NegativeInfinity, upper = double.PositiveInfinity;
            for (int j = 0; j < _n; j++)
            {
                if (Array.IndexOf(_basis, j) >= 0) continue;
                double r0 = _c[j] - _A.Column(j).DotProduct(_y);
                double a  = _A.Column(j).DotProduct(w);
                if (System.Math.Abs(a) <= 1e-12) continue;
                if (a > 0) upper = System.Math.Min(upper, r0 / a);
                else       lower = System.Math.Max(lower, r0 / a);
            }
            return (lower, upper);
        }

        // RHS b_i range: keep x_B = B^{-1}(b + Δ e_i) ≥ 0.
        public (double decrease, double increase) RHSRange(int i)
        {
            var ei = Vector<double>.Build.Dense(_m, k => k == i ? 1.0 : 0.0);
            var v  = _Binv * ei;
            var xB = _Binv * _b;

            double dec = double.PositiveInfinity, inc = double.PositiveInfinity;
            for (int r = 0; r < _m; r++)
            {
                if (System.Math.Abs(v[r]) <= 1e-12) continue;
                if (v[r] > 0) dec = System.Math.Min(dec,  xB[r] / v[r]);
                else          inc = System.Math.Min(inc, -xB[r] / v[r]);
            }
            return (-dec, inc);
        }
    }
}
