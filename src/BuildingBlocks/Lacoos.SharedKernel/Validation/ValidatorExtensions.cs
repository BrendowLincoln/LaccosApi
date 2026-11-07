namespace Lacoos.SharedKernel.Validation;

public static class ValidatorExtensions
{
    /// <summary>Aceita só http e https, para links salvos não executarem script no frontend.</summary>
    public static Validator Url(this Validator validator, string? value, string field, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return validator;
        }

        var valid = Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        return valid ? validator : validator.Add(field, $"{label} deve ser uma URL http ou https.");
    }
}
