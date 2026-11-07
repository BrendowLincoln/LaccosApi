using System.Net.Mail;
using Lacoos.SharedKernel.Errors;

namespace Lacoos.SharedKernel.Validation;

/// <summary>Acumula erros de campo para devolvê-los todos de uma vez, em português.</summary>
public sealed class Validator
{
    private const decimal MaxMoney = 999_999_999_999.99m;
    private readonly Dictionary<string, List<string>> _errors = [];

    public Validator Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var messages))
        {
            _errors[field] = messages = [];
        }

        messages.Add(message);
        return this;
    }

    public Validator Required(string? value, string field, string label) =>
        string.IsNullOrWhiteSpace(value) ? Add(field, $"{label} é obrigatório.") : this;

    public Validator Required<T>(T? value, string field, string label) where T : struct =>
        value is null ? Add(field, $"{label} é obrigatório.") : this;

    public Validator MaxLength(string? value, int max, string field, string label) =>
        value is not null && value.Length > max
            ? Add(field, $"{label} deve ter no máximo {max} caracteres.")
            : this;

    public Validator Min(int value, int min, string field, string label) =>
        value < min ? Add(field, $"{label} deve ser no mínimo {min}.") : this;

    public Validator Range(int value, int min, int max, string field, string label) =>
        value < min || value > max ? Add(field, $"{label} deve estar entre {min} e {max}.") : this;

    /// <summary>Valor em BRL: não negativo, com no máximo 2 casas decimais.</summary>
    public Validator Money(decimal? value, string field, string label)
    {
        if (value is null)
        {
            return this;
        }

        if (value < 0)
        {
            return Add(field, $"{label} não pode ser negativo.");
        }

        if (value > MaxMoney)
        {
            return Add(field, $"{label} excede o limite permitido.");
        }

        return decimal.Round(value.Value, 2) != value.Value
            ? Add(field, $"{label} deve ter no máximo 2 casas decimais.")
            : this;
    }

    public Validator Email(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return this;
        }

        var valid = MailAddress.TryCreate(value.Trim(), out var address)
            && address.Address == value.Trim()
            && address.Host.Contains('.');
        return valid ? this : Add(field, "E-mail inválido.");
    }

    public void ThrowIfInvalid()
    {
        if (_errors.Count > 0)
        {
            throw new ValidationException(_errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
        }
    }
}
