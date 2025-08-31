using System;
using System.Linq;
using ORSolver.Math.Sensitivity;
using ORSolver.Math.Simplex;

namespace ORSolver.UI
{
    // Only show this AFTER a linear model solved to optimality (guard in Program.cs)
    public sealed class SensitivityMenu
    {
        private readonly CanonicalizedModel _canonical;
        private readonly int[] _finalBasis;
        private readonly int _dp;

        public SensitivityMenu(CanonicalizedModel canonical, int[] finalBasis, int roundDp = 3)
        {
            _canonical  = canonical;
            _finalBasis = (int[])finalBasis.Clone();
            _dp         = roundDp;
        }

        public void Show()
        {
            var sa = new SensitivityAnalyzer(_canonical, _finalBasis);
            while (true)
            {
                Console.WriteLine("\n=== Sensitivity Analysis ===");
                Console.WriteLine("1) Shadow prices");
                Console.WriteLine("2) Obj coeff range (non-basic j)");
                Console.WriteLine("3) Obj coeff range (basic pos i)");
                Console.WriteLine("4) RHS range (constraint i)");
                Console.WriteLine("0) Back");
                Console.Write("Choose: ");
                var key = Console.ReadLine();
                if (key == "0") return;

                try
                {
                    switch (key)
                    {
                        case "1":
                            var y = sa.ShadowPrices();
                            Console.WriteLine("y: " + string.Join(", ", y.Select(v => System.Math.Round(v, _dp).ToString($"F{_dp}"))));
                            break;
                        case "2":
                            Console.Write("Enter j (non-basic col): ");
                            int j = int.Parse(Console.ReadLine()!);
                            var nb = sa.ObjCoeffRangeNonBasic(j);
                            Console.WriteLine($"Δc_j in [{(double.IsNegativeInfinity(nb.decrease) ? "-inf" : nb.decrease.ToString())}, {nb.increase}]");
                            break;
                        case "3":
                            Console.Write("Enter i (basic position 0..m-1): ");
                            int i = int.Parse(Console.ReadLine()!);
                            var br = sa.ObjCoeffRangeBasic(i);
                            Console.WriteLine($"Δc_bi in [{br.decrease}, {br.increase}]");
                            break;
                        case "4":
                            Console.Write("Enter i (constraint index 0..m-1): ");
                            int k = int.Parse(Console.ReadLine()!);
                            var rr = sa.RHSRange(k);
                            Console.WriteLine($"Δb_i in [{rr.decrease}, {rr.increase}]");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                }
            }
        }
    }
}
