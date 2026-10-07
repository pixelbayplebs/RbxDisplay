using System;

namespace Stretcher;

internal static class Rules
{
    public static bool SameMode(DisplayMode? a, DisplayMode? b)
    {
        return a != null && b != null && a.Width == b.Width && a.Height == b.Height &&
            a.Refresh == b.Refresh && a.Bits == b.Bits && a.Orientation == b.Orientation && a.Flags == b.Flags;
    }

    public static float[] Identity()
    {
        float[] matrix = new float[25];
        for (int i = 0; i < 5; i++)
            matrix[i * 5 + i] = 1;
        return matrix;
    }

    public static bool SameColor(float[]? a, float[]? b)
    {
        if (a == null || b == null || a.Length != 25 || b.Length != 25)
            return false;
        for (int i = 0; i < 25; i++)
        {
            if (float.IsNaN(a[i]) || float.IsNaN(b[i]) || float.IsInfinity(a[i]) || float.IsInfinity(b[i]) || Math.Abs(a[i] - b[i]) > 0.0005f)
                return false;
        }

        return true;
    }

    public static float[] Saturated(float[] original, double saturation)
    {
        ArgumentNullException.ThrowIfNull(original);
        float[] transform = Identity();
        double[] weights = [0.2126, 0.7152, 0.0722];
        for (int input = 0; input < 3; input++)
        {
            for (int output = 0; output < 3; output++)
                transform[input * 5 + output] = (float)(weights[input] * (1 - saturation) + (input == output ? saturation : 0));
        }

        float[] result = new float[25];
        // Windows uses row vectors. Preserve any existing color matrix, then apply saturation.
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                for (int k = 0; k < 5; k++)
                    result[i * 5 + j] += original[i * 5 + k] * transform[k * 5 + j];
            }
        }

        return result;
    }

    public static DisplayMode Desired(DisplayMode current, int width, int height, int hz)
    {
        ArgumentNullException.ThrowIfNull(current);
        return new DisplayMode
        {
            Width = width,
            Height = height,
            Refresh = hz == 0 ? current.Refresh : hz,
            Bits = current.Bits,
            Orientation = current.Orientation,
            Flags = current.Flags
        };
    }

    public static bool CanRestoreMode(string? expectedIdentity, string? currentIdentity, DisplayMode? current, DisplayMode? owned)
    {
        return !string.IsNullOrWhiteSpace(expectedIdentity) && expectedIdentity == currentIdentity && SameMode(current, owned);
    }
}
