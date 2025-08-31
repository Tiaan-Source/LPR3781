using ORSolver.Models;
using System;
using System.Collections.Generic;
using System.Linq;

public class KnapsackSolver
{
    private class Item
    {
        public int Weight, Profit;
        public double Ratio => (double)Profit / Weight;
    }

    private class Node
    {
        public int Level, Profit, Weight;
        public double Bound;
        public List<int> TakenItems = new();
    }

    private List<Item> _items = new();
    private int _capacity;
    private int _bestProfit = 0;
    private List<int> _bestTaken = new();
    private int _nodeId = 0;
    private List<string> _nodeLog = new();

    public KnapsackResult Solve(LPModel model)
    {
        Reset();

        // Use only the FIRST constraint for capacity and weights and only calculate for max problems
        if (model.Constraints.Count == 0)
            throw new ArgumentException("No constraints found in model.");

        var isMaximization = model.Type;
        if (isMaximization != ProblemType.Maximize)
            throw new ArgumentException("This model can only solve maximization problems");

        var constraint = model.Constraints[0];
        if (constraint.Relation != Relation.LessOrEqual)
            throw new ArgumentException("First constraint must be of type '<=' for knapsack capacity.");
        
        _capacity = (int)Math.Floor(constraint.RHS);

        for (int i = 0; i < model.VarCount; i++)
        {
            int weight = (int)Math.Floor(constraint.Coeffs[i]);
            int profit = (int)Math.Floor(model.Objective[i]);

            _items.Add(new Item { Weight = weight, Profit = profit });
        }

        _items.Sort((a, b) => b.Ratio.CompareTo(a.Ratio)); // Sort descending by ratio

        PrintHeader();

        var root = new Node
        {
            Level = -1,
            Profit = 0,
            Weight = 0,
            Bound = CalculateBound(0, 0, 0)
        };

        BranchAndBound(root);

        PrintFooter();

        return new KnapsackResult
        {
            MaxProfit = _bestProfit,
            ItemsTaken = _bestTaken,
            TotalWeight = _bestTaken.Sum(i => _items[i - 1].Weight),
            ExplorationLog = _nodeLog
        };
    }

    private void BranchAndBound(Node node)
    {
        if (node.Level == _items.Count - 1)
            return;

        int nextLevel = node.Level + 1;

        // LEFT BRANCH (Include item)
        var left = new Node
        {
            Level = nextLevel,
            Weight = node.Weight + _items[nextLevel].Weight,
            Profit = node.Profit + _items[nextLevel].Profit,
            TakenItems = new List<int>(node.TakenItems)
        };
        left.TakenItems.Add(nextLevel + 1);

        if (left.Weight <= _capacity)
        {
            if (left.Profit > _bestProfit)
            {
                _bestProfit = left.Profit;
                _bestTaken = new List<int>(left.TakenItems);
            }

            left.Bound = CalculateBound(left.Level, left.Weight, left.Profit);
            LogNode(left);
            if (left.Bound > _bestProfit)
                BranchAndBound(left);
        }

        // RIGHT BRANCH (Exclude item)
        var right = new Node
        {
            Level = nextLevel,
            Weight = node.Weight,
            Profit = node.Profit,
            TakenItems = new List<int>(node.TakenItems),
            Bound = CalculateBound(nextLevel, node.Weight, node.Profit)
        };

        LogNode(right);
        if (right.Bound > _bestProfit)
            BranchAndBound(right);
    }

    private double CalculateBound(int level, int weight, int profit)
    {
        if (weight >= _capacity)
            return 0;

        double bound = profit;
        int remaining = _capacity - weight;
        int i = level + 1;

        while (i < _items.Count && _items[i].Weight <= remaining)
        {
            bound += _items[i].Profit;
            remaining -= _items[i].Weight;
            i++;
        }

        if (i < _items.Count)
            bound += _items[i].Ratio * remaining;

        return bound;
    }

    private void LogNode(Node node)
    {
        _nodeId++;
        var line = $"{_nodeId,-3} {node.Level,-5} {node.Weight,-6} {node.Profit,-6} [{string.Join(",", node.TakenItems)}]";
        _nodeLog.Add(line);
    }

    private void PrintHeader()
    {
        Console.WriteLine($"\nKnapsack Capacity: {_capacity}");
        Console.WriteLine("\nItems (sorted by profit/weight ratio):");
        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            Console.WriteLine($"Item {i + 1}: Weight = {item.Weight}, Profit = {item.Profit}, Ratio = {item.Ratio:F2}");
        }

        Console.WriteLine("\nExplored Nodes Table:");
        Console.WriteLine($"{"ID",-3} {"Level",-5} {"Weight",-6} {"Profit",-6} {"Items Taken"}");
    }

    private void PrintFooter()
    {
        foreach (var line in _nodeLog)
        {
            Console.WriteLine(line);
        }

        Console.WriteLine("\nBest Solution:");
        Console.WriteLine($"Maximum Profit = {_bestProfit}");
        Console.WriteLine("Items Taken (1-based index): " + string.Join(", ", _bestTaken));
    }

    private void Reset()
    {
        _items = new();
        _bestProfit = 0;
        _bestTaken = new();
        _nodeId = 0;
        _nodeLog = new();
    }
}

public class KnapsackResult
{
    public int MaxProfit { get; set; }
    public List<int> ItemsTaken { get; set; } = new();
    public int TotalWeight { get; set; }
    public List<string> ExplorationLog { get; set; } = new();
}
