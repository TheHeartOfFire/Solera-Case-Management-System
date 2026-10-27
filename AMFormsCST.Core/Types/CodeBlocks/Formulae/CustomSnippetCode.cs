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
        CollectLeafSlots(root, [], candidates, exposeAllLeaves: false);
        if (_exposeVariableIdentifiers && candidates.Count == 0)
        {
            CollectLeafSlots(root, [], candidates, exposeAllLeaves: true);
        }

        var groupedEntries = new List<(List<int[]> Paths, string Description, string InitialValue, string? GroupKey)>();

        foreach (var (path, input) in candidates)
        {
            var strVal = input.Value?.ToString() ?? string.Empty;
            var label = string.IsNullOrWhiteSpace(input.Description)
                ? $"Input {groupedEntries.Count + 1}"
                : input.Description;
            var groupKey = input.ExposedGroupKey;
            if (string.IsNullOrWhiteSpace(groupKey) && _exposeVariableIdentifiers && !string.IsNullOrWhiteSpace(strVal))
            {
                groupKey = $"var:{strVal.Trim().ToUpperInvariant()}";
            }

            if (!string.IsNullOrWhiteSpace(groupKey))
            {
                var existingIdx = groupedEntries.FindIndex(g =>
                    string.Equals(g.GroupKey, groupKey, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(g.InitialValue, strVal, StringComparison.OrdinalIgnoreCase));

                if (existingIdx >= 0)
                {
                    groupedEntries[existingIdx].Paths.Add(path);
                    continue;
                }
            }

            groupedEntries.Add(([path], label, strVal, groupKey));
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

    private void CollectLeafSlots(CodeBase node, List<int> currentPath, List<(int[] Path, CodeInput Input)> results, bool exposeAllLeaves = false)
    {
        for (int i = 0; i < node.Inputs.Count; i++)
        {
            var input = node.Inputs[i];
            var nextPath = new List<int>(currentPath) { i };

            if (input.Value is CodeBase childBlock)
            {
                CollectLeafSlots(childBlock, nextPath, results, exposeAllLeaves);
            }
            else
            {
                var strVal = input.Value?.ToString() ?? string.Empty;
                bool shouldExpose =
                    exposeAllLeaves ||
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
        if (string.Equals(trimmed, "TODAY()", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "RANDOM()", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "PI", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
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

    public SavedCustomSnippetDto ToDto()
    {
        return new SavedCustomSnippetDto
        {
            Name = Name ?? "Custom Snippet",
            Description = Description ?? string.Empty,
            ExposeVariableIdentifiers = _exposeVariableIdentifiers,
            TemplateRoot = ToNodeDto(_templateRoot),
            ExposedInputs = Inputs.Select(inp => new SavedCodeInputDto
            {
                Index = inp.Index,
                Description = inp.Description,
                IsExposedInput = inp.IsExposedInput,
                ExposedGroupKey = inp.ExposedGroupKey,
                StringValue = inp.Value?.ToString() ?? string.Empty
            }).ToList()
        };
    }

    public static CustomSnippetCode? FromDto(
        SavedCustomSnippetDto? dto,
        IReadOnlyCollection<AMFormsCST.Core.Interfaces.CodeBlocks.ICodeBase>? knownBlocks = null)
    {
        if (dto?.TemplateRoot == null) return null;
        var root = FromNodeDto(dto.TemplateRoot, knownBlocks ?? []);
        var snippet = new CustomSnippetCode(dto.Name, dto.Description, root, dto.ExposeVariableIdentifiers);

        if (dto.ExposedInputs != null && dto.ExposedInputs.Count > 0)
        {
            for (int i = 0; i < dto.ExposedInputs.Count && i < snippet.Inputs.Count; i++)
            {
                var savedInp = dto.ExposedInputs[i];
                if (savedInp.StringValue != null)
                {
                    snippet.SetInputValue(i, savedInp.StringValue);
                }
            }
        }

        return snippet;
    }

    private static SavedCodeBlockNodeDto ToNodeDto(CodeBase node)
    {
        var nodeDto = new SavedCodeBlockNodeDto
        {
            TypeName = node.GetType().Name,
            Name = node.Name,
            Description = node.Description,
            Prefix = node.Prefix,
            Operator = (node as Functions.OperatorCode)?.Operator
        };

        foreach (var input in node.Inputs)
        {
            var inputDto = new SavedCodeInputDto
            {
                Index = input.Index,
                Description = input.Description,
                IsExposedInput = input.IsExposedInput,
                ExposedGroupKey = input.ExposedGroupKey
            };

            if (input.Value is CodeBase childCb)
            {
                inputDto.ChildBlock = ToNodeDto(childCb);
            }
            else
            {
                inputDto.StringValue = input.Value?.ToString() ?? string.Empty;
            }

            nodeDto.Inputs.Add(inputDto);
        }

        return nodeDto;
    }

    private static CodeBase FromNodeDto(
        SavedCodeBlockNodeDto dto,
        IReadOnlyCollection<AMFormsCST.Core.Interfaces.CodeBlocks.ICodeBase> prototypes)
    {
        CodeBase instance;

        if (dto.TypeName == nameof(Functions.OperatorCode) || !string.IsNullOrWhiteSpace(dto.Operator))
        {
            instance = new Functions.OperatorCode(string.IsNullOrWhiteSpace(dto.Operator) ? (dto.Prefix ?? "+") : dto.Operator!);
        }
        else
        {
            var match = prototypes.FirstOrDefault(p =>
                            string.Equals(p.GetType().Name, dto.TypeName, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(p.Prefix, dto.Prefix, StringComparison.OrdinalIgnoreCase))
                        ?? prototypes.FirstOrDefault(p =>
                            !string.IsNullOrWhiteSpace(dto.Prefix) &&
                            string.Equals(p.Prefix, dto.Prefix, StringComparison.OrdinalIgnoreCase))
                        ?? prototypes.FirstOrDefault(p =>
                            !string.IsNullOrWhiteSpace(dto.Name) &&
                            string.Equals(p.Name, dto.Name, StringComparison.OrdinalIgnoreCase));

            if (match is CodeBase matchedCb)
            {
                instance = matchedCb.Clone();
            }
            else
            {
                instance = new Functions.FormgenFunctionCode(
                    dto.Prefix ?? "BLOCK",
                    dto.Name ?? "Block",
                    dto.Inputs.Select(i => i.Description).ToArray());
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.Name)) instance.Name = dto.Name;
        if (!string.IsNullOrWhiteSpace(dto.Description)) instance.Description = dto.Description;
        if (!string.IsNullOrWhiteSpace(dto.Prefix) && instance is not Functions.OperatorCode) instance.Prefix = dto.Prefix;

        instance.Inputs.Clear();
        int idx = 0;
        foreach (var inpDto in dto.Inputs.OrderBy(i => i.Index))
        {
            CodeInput rebuiltInput;
            if (inpDto.ChildBlock != null)
            {
                var childNode = FromNodeDto(inpDto.ChildBlock, prototypes);
                rebuiltInput = new CodeInput(childNode, inpDto.Description, idx);
            }
            else
            {
                rebuiltInput = new CodeInput(inpDto.StringValue ?? string.Empty, inpDto.Description, idx);
            }

            rebuiltInput.IsExposedInput = inpDto.IsExposedInput;
            rebuiltInput.ExposedGroupKey = inpDto.ExposedGroupKey;
            instance.Inputs.Add(rebuiltInput);
            idx++;
        }

        return instance;
    }
}

public sealed class SavedCustomSnippetDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool ExposeVariableIdentifiers { get; set; }
    public SavedCodeBlockNodeDto? TemplateRoot { get; set; }
    public List<SavedCodeInputDto> ExposedInputs { get; set; } = [];
}

public sealed class SavedCodeBlockNodeDto
{
    public string? TypeName { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Prefix { get; set; }
    public string? Operator { get; set; }
    public List<SavedCodeInputDto> Inputs { get; set; } = [];
}

public sealed class SavedCodeInputDto
{
    public int Index { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsExposedInput { get; set; }
    public string? ExposedGroupKey { get; set; }
    public string? StringValue { get; set; }
    public SavedCodeBlockNodeDto? ChildBlock { get; set; }
}


