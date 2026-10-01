namespace Aibysitter.Rules;

/// <summary>A markdown section. Level 0 is content before the first heading.</summary>
public sealed record Section(string Heading, int Level, int StartLine, int EndLine);
