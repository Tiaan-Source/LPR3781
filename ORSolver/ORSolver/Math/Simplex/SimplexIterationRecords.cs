namespace ORSolver.Math.Simplex
{
    // Price-Out: reports y, reduced costs r, entering column, and z
    public sealed record PriceOutIteration(
        int Iteration,
        int EnteringCol,
        double[] DualY,
        double[] ReducedCosts,
        double Z);

    // Product-Form: reports direction d, leaving row, step size, xB before/after
    public sealed record ProductFormIteration(
        int Iteration,
        int LeavingBasisPos,
        double[] DirectionD,
        double[] XbBefore,
        double StepSize,
        double[] XbAfter);
}
