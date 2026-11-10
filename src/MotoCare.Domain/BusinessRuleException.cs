namespace MotoCare.Domain;

/// <summary>A rule broken by the request; returned to the client as HTTP 400.</summary>
public sealed class BusinessRuleException(string message) : Exception(message);
