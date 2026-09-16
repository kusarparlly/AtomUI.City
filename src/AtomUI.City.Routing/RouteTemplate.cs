using System.Globalization;
using System.Text.RegularExpressions;

namespace AtomUI.City.Routing;

/// <summary>
/// Represents route template.
/// </summary>
public sealed class RouteTemplate
{
    private RouteTemplate(string pattern, IReadOnlyList<RouteTemplateSegment> segments)
    {
        Pattern = pattern;
        Segments = Array.AsReadOnly(segments.ToArray());
    }

    /// <summary>
    /// Gets pattern.
    /// </summary>
    public string Pattern { get; }

    /// <summary>
    /// Gets segments.
    /// </summary>
    public IReadOnlyList<RouteTemplateSegment> Segments { get; }

    /// <summary>
    /// Executes the parse operation.
    /// </summary>
    public static RouteTemplate Parse(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        if (pattern.Length > 0 && string.IsNullOrWhiteSpace(pattern))
        {
            throw new ArgumentException("The value cannot be composed entirely of whitespace.", nameof(pattern));
        }

        var normalizedPattern = NormalizePattern(pattern);
        var segments = normalizedPattern.Length == 0
            ? []
            : ParseSegments(normalizedPattern);

        return new RouteTemplate(normalizedPattern, segments);
    }

    /// <summary>
    /// Executes the try match operation.
    /// </summary>
    public bool TryMatch(string path, out IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(path);

        var routeValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pathSegments = NormalizePattern(path)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();
        var pathIndex = 0;

        foreach (var segment in Segments)
        {
            if (segment.Kind == RouteTemplateSegmentKind.CatchAll)
            {
                var catchAllValue = string.Join('/', pathSegments.Skip(pathIndex));
                if (catchAllValue.Length == 0 && segment.DefaultValue is not null)
                {
                    catchAllValue = segment.DefaultValue;
                }

                if (!SatisfiesConstraints(catchAllValue, segment.Constraints))
                {
                    values = RouteParameters.Empty();
                    return false;
                }

                routeValues[segment.Name!] = catchAllValue;
                pathIndex = pathSegments.Length;
                continue;
            }

            if (pathIndex >= pathSegments.Length)
            {
                if (segment.Kind == RouteTemplateSegmentKind.Parameter && segment.DefaultValue is not null)
                {
                    routeValues[segment.Name!] = segment.DefaultValue;
                    continue;
                }

                if (segment.Kind == RouteTemplateSegmentKind.Parameter && segment.IsOptional)
                {
                    continue;
                }

                values = RouteParameters.Empty();

                return false;
            }

            var pathSegment = pathSegments[pathIndex];

            if (segment.Kind == RouteTemplateSegmentKind.Literal)
            {
                if (!string.Equals(segment.Literal, pathSegment, StringComparison.OrdinalIgnoreCase))
                {
                    values = RouteParameters.Empty();

                    return false;
                }

                pathIndex++;
                continue;
            }

            if (!SatisfiesConstraints(pathSegment, segment.Constraints))
            {
                values = RouteParameters.Empty();

                return false;
            }

            routeValues[segment.Name!] = pathSegment;
            pathIndex++;
        }

        if (pathIndex != pathSegments.Length)
        {
            values = RouteParameters.Empty();

            return false;
        }

        values = RouteParameters.Copy(routeValues);

        return true;
    }

    /// <summary>
    /// Executes the try bind parameters operation.
    /// </summary>
    public bool TryBindParameters(
        IReadOnlyDictionary<string, string> parameters,
        out IReadOnlyDictionary<string, string> boundParameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var values = new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase);

        foreach (var segment in Segments.Where(segment => segment.Kind != RouteTemplateSegmentKind.Literal))
        {
            if (!values.TryGetValue(segment.Name!, out var value))
            {
                if (segment.DefaultValue is not null)
                {
                    values[segment.Name!] = segment.DefaultValue;
                    continue;
                }

                if (segment.IsOptional || segment.Kind == RouteTemplateSegmentKind.CatchAll)
                {
                    continue;
                }

                boundParameters = RouteParameters.Empty();
                return false;
            }

            if (!SatisfiesConstraints(value, segment.Constraints))
            {
                boundParameters = RouteParameters.Empty();
                return false;
            }
        }

        boundParameters = RouteParameters.Copy(values);
        return true;
    }

    internal int SpecificityScore()
    {
        return Segments.Sum(segment => segment.Kind switch
        {
            RouteTemplateSegmentKind.Literal => 40,
            RouteTemplateSegmentKind.Parameter when segment.Constraints.Count > 0 => 30,
            RouteTemplateSegmentKind.Parameter when segment.IsOptional || segment.DefaultValue is not null => 10,
            RouteTemplateSegmentKind.Parameter => 20,
            RouteTemplateSegmentKind.CatchAll => 0,
            _ => 0,
        });
    }

    private static IReadOnlyList<RouteTemplateSegment> ParseSegments(string normalizedPattern)
    {
        var rawSegments = normalizedPattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var segments = new List<RouteTemplateSegment>(rawSegments.Length);
        var parameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < rawSegments.Length; index++)
        {
            var parsedSegment = ParseSegment(rawSegments[index]);

            if (parsedSegment.Kind == RouteTemplateSegmentKind.CatchAll && index != rawSegments.Length - 1)
            {
                throw new RouteGraphException(
                    RouteGraphError.InvalidRouteTemplate,
                    "Route catch-all parameter must be the last segment.");
            }

            if (parsedSegment.Kind != RouteTemplateSegmentKind.Literal &&
                !parameterNames.Add(parsedSegment.Name!))
            {
                throw new RouteGraphException(
                    RouteGraphError.InvalidRouteTemplate,
                    $"Route parameter '{parsedSegment.Name}' is declared more than once.");
            }

            segments.Add(parsedSegment);
        }

        return segments;
    }

    private static RouteTemplateSegment ParseSegment(string segment)
    {
        var startsWithParameter = segment.StartsWith('{');
        var endsWithParameter = segment.EndsWith('}');

        if (startsWithParameter != endsWithParameter)
        {
            throw new RouteGraphException(RouteGraphError.InvalidRouteTemplate, $"Route segment '{segment}' has unbalanced braces.");
        }

        if (!startsWithParameter)
        {
            if (segment.Contains('{') || segment.Contains('}'))
            {
                throw new RouteGraphException(RouteGraphError.InvalidRouteTemplate, $"Route segment '{segment}' has invalid braces.");
            }

            return RouteTemplateSegment.LiteralSegment(segment);
        }

        var body = segment[1..^1];
        var kind = RouteTemplateSegmentKind.Parameter;

        if (body.StartsWith('*'))
        {
            kind = RouteTemplateSegmentKind.CatchAll;
            body = body[1..];
        }

        string? defaultValue = null;
        var defaultSeparatorIndex = FindTopLevelCharacter(body, '=');

        if (defaultSeparatorIndex >= 0)
        {
            defaultValue = body[(defaultSeparatorIndex + 1)..];
            body = body[..defaultSeparatorIndex];
        }

        var parts = SplitParameterParts(body);

        if (parts.Length == 0 || parts.Any(string.IsNullOrWhiteSpace))
        {
            throw new RouteGraphException(RouteGraphError.InvalidRouteTemplate, "Route parameter name cannot be empty.");
        }

        var name = parts[0];
        var isOptional = name.EndsWith("?", StringComparison.Ordinal);

        if (isOptional)
        {
            name = name[..^1];
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new RouteGraphException(RouteGraphError.InvalidRouteTemplate, "Route parameter name cannot be empty.");
        }

        var constraints = parts.Skip(1).ToArray();
        foreach (var constraint in constraints)
        {
            if (!IsKnownConstraint(constraint))
            {
                throw new RouteGraphException(
                    RouteGraphError.InvalidRouteTemplate,
                    $"Route constraint '{constraint}' is not supported.");
            }
        }

        if (defaultValue is not null && !SatisfiesConstraints(defaultValue, constraints))
        {
            throw new RouteGraphException(
                RouteGraphError.InvalidRouteTemplate,
                $"Default value '{defaultValue}' does not satisfy the constraints for route parameter '{name}'.");
        }

        return RouteTemplateSegment.ParameterSegment(
            kind,
            name,
            isOptional,
            defaultValue,
            constraints);
    }

    private static bool SatisfiesConstraints(string value, IReadOnlyList<string> constraints)
    {
        foreach (var constraint in constraints)
        {
            if (!SatisfiesConstraint(value, constraint))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SatisfiesConstraint(string value, string constraint)
    {
        if (TryReadConstraintArgument(constraint, "min", out var min))
        {
            return TryParseDecimal(value, out var numeric) && TryParseDecimal(min, out var bound) && numeric >= bound;
        }

        if (TryReadConstraintArgument(constraint, "max", out var max))
        {
            return TryParseDecimal(value, out var numeric) && TryParseDecimal(max, out var bound) && numeric <= bound;
        }

        if (TryReadConstraintArguments(constraint, "range", 2, out var range))
        {
            return TryParseDecimal(value, out var numeric) &&
                TryParseDecimal(range[0], out var minimum) &&
                TryParseDecimal(range[1], out var maximum) &&
                numeric >= minimum && numeric <= maximum;
        }

        if (TryReadIntConstraint(constraint, "length", out var length))
        {
            return value.Length == length;
        }

        if (TryReadIntConstraint(constraint, "minlength", out var minimumLength))
        {
            return value.Length >= minimumLength;
        }

        if (TryReadIntConstraint(constraint, "maxlength", out var maximumLength))
        {
            return value.Length <= maximumLength;
        }

        if (TryReadConstraintArgument(constraint, "regex", out var pattern))
        {
            try
            {
                return Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        return constraint switch
        {
            "bool" => bool.TryParse(value, out _),
            "datetime" => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "decimal" => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
            "double" => double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _),
            "float" => float.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _),
            "guid" => Guid.TryParse(value, out _),
            "int" => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            "long" => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            "alpha" => value.Length > 0 && value.All(char.IsLetter),
            _ => false,
        };
    }

    private static bool IsKnownConstraint(string constraint)
    {
        if (TryReadConstraintArgument(constraint, "min", out var min))
        {
            return TryParseDecimal(min, out _);
        }

        if (TryReadConstraintArgument(constraint, "max", out var max))
        {
            return TryParseDecimal(max, out _);
        }

        if (TryReadConstraintArguments(constraint, "range", 2, out var range))
        {
            return TryParseDecimal(range[0], out var minimum) &&
                TryParseDecimal(range[1], out var maximum) &&
                minimum <= maximum;
        }

        if (TryReadIntConstraint(constraint, "length", out _) ||
            TryReadIntConstraint(constraint, "minlength", out _) ||
            TryReadIntConstraint(constraint, "maxlength", out _))
        {
            return true;
        }

        if (TryReadConstraintArgument(constraint, "regex", out var pattern))
        {
            try
            {
                _ = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        return constraint is
            "bool" or
            "datetime" or
            "decimal" or
            "double" or
            "float" or
            "guid" or
            "int" or
            "long" or
            "alpha";
    }

    private static string[] SplitParameterParts(string body)
    {
        var parts = new List<string>();
        var start = 0;
        var depth = 0;
        var escaped = false;
        var inCharacterClass = false;

        for (var index = 0; index < body.Length; index++)
        {
            var current = body[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (current == '\\')
            {
                escaped = true;
                continue;
            }

            if (current == '[')
            {
                inCharacterClass = true;
                continue;
            }

            if (current == ']' && inCharacterClass)
            {
                inCharacterClass = false;
                continue;
            }

            if (!inCharacterClass)
            {
                depth += current switch
                {
                    '(' => 1,
                    ')' => -1,
                    _ => 0,
                };
            }

            if (depth < 0)
            {
                throw new RouteGraphException(RouteGraphError.InvalidRouteTemplate, "Route constraint has unbalanced parentheses.");
            }

            if (current == ':' && depth == 0 && !inCharacterClass)
            {
                parts.Add(body[start..index]);
                start = index + 1;
            }
        }

        if (depth != 0 || inCharacterClass)
        {
            throw new RouteGraphException(RouteGraphError.InvalidRouteTemplate, "Route constraint has unbalanced parentheses.");
        }

        parts.Add(body[start..]);
        return parts.ToArray();
    }

    private static int FindTopLevelCharacter(string value, char character)
    {
        var depth = 0;
        var escaped = false;
        var inCharacterClass = false;
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (current == '\\')
            {
                escaped = true;
                continue;
            }

            if (current == '[')
            {
                inCharacterClass = true;
                continue;
            }

            if (current == ']' && inCharacterClass)
            {
                inCharacterClass = false;
                continue;
            }

            if (!inCharacterClass)
            {
                depth += current switch
                {
                    '(' => 1,
                    ')' => -1,
                    _ => 0,
                };
            }

            if (current == character && depth == 0 && !inCharacterClass)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryReadIntConstraint(string constraint, string name, out int value)
    {
        value = 0;
        return TryReadConstraintArgument(constraint, name, out var argument) &&
            int.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
            value >= 0;
    }

    private static bool TryReadConstraintArgument(string constraint, string name, out string argument)
    {
        if (TryReadConstraintArguments(constraint, name, 1, out var arguments))
        {
            argument = arguments[0];
            return true;
        }

        argument = string.Empty;
        return false;
    }

    private static bool TryReadConstraintArguments(
        string constraint,
        string name,
        int count,
        out string[] arguments)
    {
        var prefix = name + "(";
        if (!constraint.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !constraint.EndsWith(')'))
        {
            arguments = [];
            return false;
        }

        var body = constraint[prefix.Length..^1];
        arguments = count == 1
            ? [body.Trim()]
            : body.Split(',', StringSplitOptions.TrimEntries);
        return arguments.Length == count && arguments.All(argument => argument.Length > 0);
    }

    private static bool TryParseDecimal(string value, out decimal result) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);

    private static string NormalizePattern(string pattern)
    {
        return pattern.Trim().Trim('/');
    }

}
