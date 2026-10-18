namespace AMFormsCST.Core.Types.CodeBlocks.Formulae;

public class CustomSnippetCode : CodeBase
{
    private readonly CodeBase _templateRoot;
    private readonly bool _exposeVariableIdentifiers;
    private readonly List<(List<int[]> Paths, string Description, string? GroupKey)> _exposedSlots = [];

    public CustomSnippetCode() : this("Custom Snippet", "User-defined custom snippet", new Functions.TextCode())
    {
    }

    public CustomSnippetCode(
        string name,
        string description,
        CodeBase templateRoot,
        bool exposeVariableIdentifiers = false)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Custom Snippet" : name;
        Description = description ?? string.Empty;
        _exposeVariableIdentifiers = exposeVariableIdentifiers;
        _templateRoot = (templateRoot.GetStructure() ?? templateRoot).Clone();

        DiscoverExposedLeafSlots(_templateRoot);
    }

    private void DiscoverExposedLeafSlots(CodeBase root)
    {
        var candidates = new List<(int[] Path, CodeInput Input)>();
        CollectLeafSlots(root, [], candidates);

        var groupedEntries = new List<(List<int[]> Paths, string Description, string InitialValue, string? GroupKey)>();

        foreach (var (path, input) in candidates)
        {
            var strVal = input.Value?.ToString() ?? string.Empty;
            var label = string.IsNullOrWhiteSpace(input.Description)
                ? $"Input {groupedEntries.Count + 1}"
                : input.Description;
            var groupKey = input.ExposedGroupKey;

            if (!string.IsNullOrWhiteSpace(groupKey))
            {
                var existingIdx = groupedEntries.FindIndex(g =>
                    string.Equals(g.GroupKey, groupKey, StringComparison.Ordinal) &&
                    string.Equals(g.InitialValue, strVal, StringComparison.Ordinal));

                if (existingIdx >= 0)
                {
                    groupedEntries[existingIdx].Paths.Add(path);
                    input.SetValue(string.Empty);
                    continue;
                }
            }

            groupedEntries.Add(([path], label, strVal, groupKey));
            input.SetValue(string.Empty);
        }

        foreach (var entry in groupedEntries)
        {
            int idx = Inputs.Count;
            var effectiveGroupKey = !string.IsNullOrWhiteSpace(entry.GroupKey)
                ? entry.GroupKey
                : $"{idx}:{entry.Description}";
            _exposedSlots.Add((entry.Paths, entry.Description, effectiveGroupKey));

            var addedInput = new CodeInput(entry.InitialValue, entry.Description, idx)
            {
                IsExposedInput = true,
                ExposedGroupKey = effectiveGroupKey
            };
            Inputs.Add(addedInput);
        }
    }

    private void CollectLeafSlots(CodeBase node, List<int> currentPath, List<(int[] Path, CodeInput Input)> results)
    {
        for (int i = 0; i < node.Inputs.Count; i++)
        {
            var input = node.Inputs[i];
            var nextPath = new List<int>(currentPath) { i };

            if (input.Value is CodeBase childBlock)
            {
                CollectLeafSlots(childBlock, nextPath, results);
            }
            else
            {
                var strVal = input.Value?.ToString() ?? string.Empty;
                bool shouldExpose =
                    input.IsExposedInput ||
                    string.IsNullOrEmpty(strVal) ||
                    (_exposeVariableIdentifiers && IsVariableIdentifierValue(strVal));

                if (shouldExpose)
                {
                    results.Add((nextPath.ToArray(), input));
                }
            }
        }
    }

    private static bool IsVariableIdentifierValue(string strVal)
    {
        var trimmed = strVal.Trim();
        if (string.IsNullOrEmpty(trimmed)) return false;
        return !IsSingleQuoted(trimmed) && !IsNumericLiteral(trimmed) && !IsBooleanLiteral(trimmed);
    }

    public override CodeBase GetStructure()
    {
        var structure = _templateRoot.Clone();
        structure.WasDeconstructed = true;
        structure.ReconstructName = Name;
        structure.ReconstructDescription = Description;

        for (int i = 0; i < _exposedSlots.Count && i < Inputs.Count; i++)
        {
            var (paths, _, groupKey) = _exposedSlots[i];
            var sourceInput = Inputs[i];
            foreach (var path in paths)
            {
                ApplyInputAtPath(structure, path, sourceInput, groupKey ?? $"{i}:{sourceInput.Description}");
            }
        }
        return structure;
    }

    private static void ApplyInputAtPath(CodeBase root, int[] path, CodeInput sourceInput, string groupKey)
    {
        CodeBase current = root;
        for (int depth = 0; depth < path.Length - 1; depth++)
        {
            int idx = path[depth];
            if (idx < 0 || idx >= current.Inputs.Count) return;
            if (current.Inputs[idx].Value is CodeBase child)
            {
                current = child;
            }
            else
            {
                return;
            }
        }

        int finalIdx = path[^1];
        if (finalIdx >= 0 && finalIdx < current.Inputs.Count)
        {
            current.SetInputFrom(finalIdx, sourceInput, groupKey);
        }
    }

    public override string GetCode() => GetStructure().GetCode();

    public override CodeBase Clone()
    {
        var clone = new CustomSnippetCode(
            Name ?? "Custom Snippet",
            Description ?? string.Empty,
            GetStructure(),
            _exposeVariableIdentifiers);
        for (int i = 0; i < Inputs.Count && i < clone.Inputs.Count; i++)
        {
            if (Inputs[i].Value is CodeBase cb)
                clone.SetInputValue(i, cb.Clone());
            else
                clone.SetInputValue(i, Inputs[i].Value?.ToString() ?? string.Empty);
        }
        return clone;
    }
}

