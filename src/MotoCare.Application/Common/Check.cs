using MotoCare.Domain;

namespace MotoCare.Application.Common;

/// <summary>
/// Small validation helpers used by the services.
/// Each method throws a BusinessRuleException, which the API returns as HTTP 400 with the message.
/// </summary>
public static class Check
{
    /// <summary>Fails with <paramref name="message"/> when the condition is false.</summary>
    public static void That(bool condition, string message)
    {
        if (!condition) throw new BusinessRuleException(message);
    }

    /// <summary>Text must not be empty.</summary>
    public static void Required(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new BusinessRuleException($"{label} is required.");
    }

    /// <summary>An id (dropdown selection) must be chosen.</summary>
    public static void Required(int? id, string label)
    {
        if (id is null or <= 0) throw new BusinessRuleException($"Select the {label.ToLowerInvariant()}.");
    }

    /// <summary>Number must be greater than zero.</summary>
    public static void Positive(decimal value, string label)
    {
        if (value <= 0) throw new BusinessRuleException($"{label} must be greater than zero.");
    }

    /// <summary>Number must be zero or more.</summary>
    public static void NotNegative(decimal value, string label)
    {
        if (value < 0) throw new BusinessRuleException($"{label} cannot be negative.");
    }

    /// <summary>A record loaded from the database must exist.</summary>
    public static T Found<T>(T? value, string what) where T : class
        => value ?? throw new KeyNotFoundException($"{what} not found.");
}
