namespace PressureAdvance.Core;

internal static class Validation
{
    public static double Finite(double value, string name)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(name, value, "Value must be finite.");
        }

        return value;
    }

    public static double Positive(double value, string name)
    {
        Finite(value, name);
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Value must be greater than zero.");
        }

        return value;
    }

    public static double NonNegative(double value, string name)
    {
        Finite(value, name);
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Value must not be negative.");
        }

        return value;
    }
}
