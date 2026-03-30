using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ProductNormaliser.Web.Infrastructure;

internal static class ScopedFormValidation
{
    public static bool TryValidateActiveForm(PageModel pageModel, object model, string prefix)
    {
        var unrelatedKeys = pageModel.ModelState.Keys
            .Where(key => !IsActiveFormKey(key, prefix))
            .ToArray();

        foreach (var key in unrelatedKeys)
        {
            pageModel.ModelState.Remove(key);
        }

        if (!pageModel.ModelState.Keys.Any(key => IsActiveFormKey(key, prefix) && !string.IsNullOrWhiteSpace(key)))
        {
            var validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), validationResults, validateAllProperties: true);

            foreach (var validationResult in validationResults)
            {
                var memberNames = validationResult.MemberNames.Any()
                    ? validationResult.MemberNames
                    : [string.Empty];

                foreach (var memberName in memberNames)
                {
                    var key = string.IsNullOrWhiteSpace(memberName)
                        ? string.Empty
                        : $"{prefix}.{memberName}";

                    pageModel.ModelState.AddModelError(key, validationResult.ErrorMessage ?? "Validation failed.");
                }
            }
        }

        return !pageModel.ModelState
            .Where(entry => IsActiveFormKey(entry.Key, prefix))
            .Any(entry => entry.Value is { ValidationState: ModelValidationState.Invalid });
    }

    private static bool IsActiveFormKey(string key, string prefix)
    {
        return string.IsNullOrWhiteSpace(key)
            || string.Equals(key, prefix, StringComparison.Ordinal)
            || key.StartsWith(prefix + ".", StringComparison.Ordinal)
            || key.StartsWith(prefix + "[", StringComparison.Ordinal);
    }
}