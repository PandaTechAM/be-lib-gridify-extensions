namespace GridifyExtensions.Models;

/// <summary>
///     A single filterable/orderable property mapping exposed by a mapper.
/// </summary>
/// <param name="Name">Public field name used in filter and order expressions.</param>
/// <param name="Type">CLR type name of the mapped value, when resolvable.</param>
public record MappingModel(string Name, string? Type);
