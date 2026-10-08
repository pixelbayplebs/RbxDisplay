using Xunit;

namespace RbxDisplay;

public sealed class ColorRulesTests
{
    [Fact(DisplayName = "100% saturation is identity")]
    public void FullSaturationMatchesIdentity()
    {
        Assert.True(Rules.SameColor(Rules.Saturated(Rules.Identity(), 1), Rules.Identity()));
    }

    [Fact(DisplayName = "grayscale channels agree")]
    public void ZeroSaturationAgreesAcrossChannels()
    {
        float[] gray = Rules.Saturated(Rules.Identity(), 0);
        for (int channel = 0; channel < 3; channel++)
        {
            Assert.True(System.Math.Abs(gray[channel * 5] - gray[channel * 5 + 1]) < 0.000001);
            Assert.True(System.Math.Abs(gray[channel * 5] - gray[channel * 5 + 2]) < 0.000001);
        }
    }

    [Fact(DisplayName = "boost preserves gray")]
    public void BoostPreservesGray()
    {
        float[] boost = Rules.Saturated(Rules.Identity(), 1.35);
        for (int output = 0; output < 3; output++)
            Assert.True(System.Math.Abs(boost[output] + boost[5 + output] + boost[10 + output] - 1) < 0.000001);
    }

    [Fact(DisplayName = "alpha and homogeneous coordinate preserved")]
    public void AlphaAndHomogeneousCoordinateStayOne()
    {
        float[] boost = Rules.Saturated(Rules.Identity(), 1.35);
        Assert.Equal(1, boost[18]);
        Assert.Equal(1, boost[24]);
    }

    [Fact(DisplayName = "preserve existing color transform")]
    public void ExistingTransformSurvivesIdentitySaturation()
    {
        float[] existing = Rules.Identity();
        existing[20] = 0.15f;
        Assert.True(Rules.SameColor(Rules.Saturated(existing, 1), existing));
    }

    [Fact(DisplayName = "nonfinite matrices never compare equal")]
    public void NonFiniteMatrixIsNeverEqual()
    {
        float[] invalid = Rules.Identity();
        invalid[0] = float.PositiveInfinity;
        Assert.False(Rules.SameColor(invalid, invalid));
    }
}
