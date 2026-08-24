namespace Fovium.Navigation;

internal sealed class NaturalPathComparer : IComparer<string>
{
    public static NaturalPathComparer Instance { get; } = new();

    public int Compare(string? left, string? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        var leftName = Path.GetFileName(left);
        var rightName = Path.GetFileName(right);
        var comparison = CompareNatural(leftName, rightName, ignoreCase: true);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = string.Compare(leftName, rightName, StringComparison.Ordinal);
        return comparison != 0
            ? comparison
            : string.Compare(left, right, StringComparison.Ordinal);
    }

    private static int CompareNatural(string left, string right, bool ignoreCase)
    {
        var leftIndex = 0;
        var rightIndex = 0;
        while (leftIndex < left.Length && rightIndex < right.Length)
        {
            var leftIsDigit = IsAsciiDigit(left[leftIndex]);
            var rightIsDigit = IsAsciiDigit(right[rightIndex]);
            if (leftIsDigit && rightIsDigit)
            {
                var numeric = CompareNumericRuns(left, ref leftIndex, right, ref rightIndex);
                if (numeric != 0)
                {
                    return numeric;
                }

                continue;
            }

            var leftCharacter = ignoreCase ? char.ToUpperInvariant(left[leftIndex]) : left[leftIndex];
            var rightCharacter = ignoreCase ? char.ToUpperInvariant(right[rightIndex]) : right[rightIndex];
            if (leftCharacter != rightCharacter)
            {
                return leftCharacter.CompareTo(rightCharacter);
            }

            leftIndex++;
            rightIndex++;
        }

        return (left.Length - leftIndex).CompareTo(right.Length - rightIndex);
    }

    private static int CompareNumericRuns(
        string left,
        ref int leftIndex,
        string right,
        ref int rightIndex)
    {
        var leftStart = leftIndex;
        var rightStart = rightIndex;
        while (leftIndex < left.Length && IsAsciiDigit(left[leftIndex]))
        {
            leftIndex++;
        }

        while (rightIndex < right.Length && IsAsciiDigit(right[rightIndex]))
        {
            rightIndex++;
        }

        var leftSignificant = leftStart;
        var rightSignificant = rightStart;
        while (leftSignificant < leftIndex && left[leftSignificant] == '0')
        {
            leftSignificant++;
        }

        while (rightSignificant < rightIndex && right[rightSignificant] == '0')
        {
            rightSignificant++;
        }

        var leftLength = leftIndex - leftSignificant;
        var rightLength = rightIndex - rightSignificant;
        if (leftLength != rightLength)
        {
            return leftLength.CompareTo(rightLength);
        }

        for (var offset = 0; offset < leftLength; offset++)
        {
            var comparison = left[leftSignificant + offset].CompareTo(right[rightSignificant + offset]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';
}
