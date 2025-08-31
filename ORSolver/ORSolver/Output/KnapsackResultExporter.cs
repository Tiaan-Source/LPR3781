using System;
using System.IO;
using System.Text;
using ORSolver.Models;

public class KnapsackResultExporter
{
    public void Export(string path, LPModel model, KnapsackResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var sw = new StreamWriter(path, false, Encoding.UTF8);

        sw.WriteLine("=====================================================");
        sw.WriteLine(" solve.exe — Knapsack Branch & Bound Results");
        sw.WriteLine("=====================================================");
        sw.WriteLine();

        // Basic model info
        sw.WriteLine($"Knapsack Capacity: {result.TotalWeight}");
        sw.WriteLine($"Maximum Profit   : {result.MaxProfit}");
        sw.WriteLine($"Items Taken      : {string.Join(", ", result.ItemsTaken)}");
        sw.WriteLine();

        sw.WriteLine("Items Taken Details:");
        sw.WriteLine("Item | Weight | Profit | Ratio");
        sw.WriteLine("--------------------------------");

        var weights = model.Constraints[0].Coeffs;
        var profits = model.Objective;

        foreach (var itemIndex in result.ItemsTaken)
        {
            int idx = itemIndex - 1; 
            if (idx >= 0 && idx < weights.Length && idx < profits.Length)
            {
                double weight = weights[idx];
                double profit = profits[idx];
                double ratio = profit / weight;
                sw.WriteLine($"{itemIndex,4} | {weight,6:0.##} | {profit,6:0.##} | {ratio,5:0.00}");
            }
        }

        sw.WriteLine();
        sw.WriteLine("===== Exploration Log =====");
        sw.WriteLine("ID  Level Weight Profit Items Taken");
        sw.WriteLine("------------------------------------");
        foreach (var line in result.ExplorationLog)
            sw.WriteLine(line);
    }
}
