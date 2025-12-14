using CssClassUtility.Models;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace CssClassUtility.Operations;

public static class CssDeduplicator
{
    public static string Deduplicate(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("File not found", path);

        string content = File.ReadAllText(path);
        var classes = CssParser.GetClassesFromContent(content, path); 
        
        // Sort by position to handle Bottom-Up operations
        var sortedClasses = classes.OrderBy(c => c.StartIndex).ToList();

        // Group by Selector AND Context (e.g. @media)
        // This ensures distinct selectors (e.g. .btn vs .btn.primary) are NOT merged.
        // Only true duplicates (identical selector and context) will be merged.
        var groups = sortedClasses
            .GroupBy(c => new { c.Selector, c.Context })
            .Where(g => g.Count() > 1)
            .ToList();

        if (groups.Count == 0) return "No duplicate classes found (considering context).";

        int removedCount = 0;
        int mergedCount = 0;
        
        // We will collect operations: (Start, End, Replacement)
        // Replacement null = Remove.
        var ops = new List<(int Start, int End, string? Replacement)>();

        foreach (var group in groups)
        {
            // First instance is Master
            var master = group.First();
            var victims = group.Skip(1).ToList();

            // Merge Properties
            var masterProps = ContentToProperties(master.Content);
            
            // Loop victims
            foreach (var victim in victims)
            {
                var victimProps = ContentToProperties(victim.Content);
                foreach (var kvp in victimProps)
                {
                    // Later definitions override earlier ones
                    masterProps[kvp.Key] = kvp.Value;
                }
                
                // Mark victim for removal
                ops.Add((victim.StartIndex, victim.BlockEnd, null));
            }

            // Prepare Master Replacement
            string newContent = PropertiesToContent(masterProps);
            string newBlock = $"{master.Selector} {{\n{newContent}\n}}";
            ops.Add((master.StartIndex, master.BlockEnd, newBlock));
            
            mergedCount++;
            removedCount += victims.Count;
        }

        // Apply Operations (Reconstruction Strategy)
        // Sort ops ASCENDING
        var sortedOps = ops.OrderBy(o => o.Start).ToList();
        
        var sb = new StringBuilder();
        int lastPos = 0;

        foreach (var op in sortedOps)
        {
            // Safety Check for Overlap
            if (op.Start < lastPos) 
            {
                // Overlap detected. Since we process master and victims, ensure we don't corrupt.
                // If this op is inside a previously processed block, skip it.
                continue;
            }

            // Append text before the operation block
            if (op.Start > lastPos)
            {
                sb.Append(content.Substring(lastPos, op.Start - lastPos));
            }

            // Apply operation
            if (op.Replacement != null)
            {
                // If strictly replacing content, we might want to trim empty lines?
                // For now, straight replacement.
                sb.Append(op.Replacement);
            }
            // else: Remove (Skip content)

            // Update lastPos
            lastPos = op.End + 1;
        }

        // Append remaining content
        if (lastPos < content.Length)
        {
            sb.Append(content.Substring(lastPos));
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return $"Consolidated {mergedCount} groups. Removed {removedCount} duplicate definitions.";
    }

    private static Dictionary<string, string> ContentToProperties(string content)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // Simple Split by ; matching CssParser logic
        var parts = content.Split(';');
        foreach(var part in parts)
        {
            if(string.IsNullOrWhiteSpace(part)) continue;
            var kv = part.Split(':', 2);
            if(kv.Length == 2)
            {
                string key = kv[0].Trim();
                string val = kv[1].Trim();
                dict[key] = val;
            }
        }
        return dict;
    }

    private static string PropertiesToContent(Dictionary<string, string> props)
    {
        var sb = new StringBuilder();
        foreach (var kvp in props)
        {
            sb.AppendLine($"    {kvp.Key}: {kvp.Value};");
        }
        return sb.ToString().TrimEnd();
    }
}
