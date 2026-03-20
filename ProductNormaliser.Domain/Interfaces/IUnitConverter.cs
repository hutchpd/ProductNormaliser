namespace ProductNormaliser.Core.Interfaces;

public interface IUnitConverter
{
    object? Convert(string targetUnit, string rawValue, string valueType);
}