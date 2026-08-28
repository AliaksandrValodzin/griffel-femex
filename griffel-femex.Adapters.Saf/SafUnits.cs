using System;
using UnitsNet;

namespace griffel_femex.Adapters.Saf
{
    /// <summary>
    /// The numeric boundary. SAF carries typed UnitsNet quantities; FEMEX carries
    /// bare doubles plus a <see cref="Units"/> statement saying what they are in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The import leg normalises to SI and says so.</b> The SDK has already
    /// resolved every cell into a UnitsNet quantity by the time we see it, so there
    /// is nothing to guess; what there is, is a choice of what FEMEX's five unit
    /// enums should say afterwards. Metre, newton, celsius, degree and kilogram —
    /// one canonical answer, so two workbooks written in different systems produce
    /// FEMEX models that compare directly. That answer is still an *invention*:
    /// SAF stated a coarse <c>Metric | Imperial</c> flag and FEMEX carries five
    /// typed enums, so the specific statement is ours. It is reported.
    /// </para>
    /// <para>
    /// <b>The export leg reads the model's own statement and must not get it
    /// wrong.</b> <c>FEMEX_SAF_Corpus_Notes.md</c> §3.1 measured what happens if it
    /// does: the SDK logs <i>"Determined ExcelSystemOfUnits from file"</i> before it
    /// reads a single sheet and drives its conversions from that flag, so writing
    /// the wrong one rescales the whole model rather than mislabelling it.
    /// </para>
    /// <para>
    /// A note on temperature that is easy to get wrong in the other direction: SAF's
    /// thermal actions carry <see cref="Temperature"/>, an absolute quantity, but
    /// FEMEX's <c>TemperatureLoad.DeltaT</c> is a difference. A difference converts
    /// by ratio, never by offset, so celsius and kelvin differences are the same
    /// number and a fahrenheit difference is 5/9 of one. <see cref="TemperatureStep"/>
    /// exists so that nothing reaches for <c>Temperature.FromDegreesCelsius</c> on a
    /// value that is not a temperature.
    /// </para>
    /// </remarks>
    public sealed class SafUnits
    {
        private SafUnits(double length, double force, double mass, double temperatureStep,
                         bool angleInDegrees, bool imperial, bool mixed, bool stated)
        {
            LengthToMetres = length;
            ForceToNewtons = force;
            MassToKilograms = mass;
            TemperatureStepToKelvin = temperatureStep;
            AngleInDegrees = angleInDegrees;
            IsImperial = imperial;
            IsMixed = mixed;
            IsStated = stated;
        }

        /// <summary>What the import leg stamps on every model it produces.</summary>
        public static Units ImportedUnits => new Units
        {
            Length = LengthUnit.Metre,
            Force = ForceUnit.Newton,
            Temperature = TemperatureUnit.Celsius,
            Angle = AngleUnit.Degree,
            Mass = MassUnit.Kilogram,
        };

        public double LengthToMetres { get; }

        public double ForceToNewtons { get; }

        public double MassToKilograms { get; }

        /// <summary>Multiplier turning a temperature <i>difference</i> into kelvin.</summary>
        public double TemperatureStepToKelvin { get; }

        public bool AngleInDegrees { get; }

        /// <summary>
        /// P5's rule: imperial iff length is inch or foot, or force is pound-force or
        /// kip. The two are tested independently because SAF's single flag is coarser
        /// than FEMEX's five enums and a file may legitimately mix.
        /// </summary>
        public bool IsImperial { get; }

        /// <summary>
        /// One half of the model reads imperial and the other metric — <c>Metre</c>
        /// with <c>Kip</c> is a real and permitted FEMEX statement, and SAF has no
        /// flag for it. The exporter reports this at Error severity rather than
        /// picking a side quietly.
        /// </summary>
        public bool IsMixed { get; }

        /// <summary>The model said what its numbers are in. False means we assumed SI.</summary>
        public bool IsStated { get; }

        /// <summary>Reads a model's unit statement, falling back to SI when it has none.</summary>
        public static SafUnits Of(Units? units)
        {
            if (units is null)
                return new SafUnits(1.0, 1.0, 1.0, 1.0, true, false, false, false);

            bool stated = units.Length.HasValue && units.Force.HasValue;

            LengthUnit length = units.Length ?? LengthUnit.Metre;
            ForceUnit force = units.Force ?? ForceUnit.Newton;
            MassUnit mass = units.Mass ?? MassUnit.Kilogram;
            TemperatureUnit temperature = units.Temperature ?? TemperatureUnit.Celsius;
            AngleUnit angle = units.Angle ?? AngleUnit.Degree;

            bool imperialLength = length == LengthUnit.Inch || length == LengthUnit.Foot;
            bool imperialForce = force == ForceUnit.PoundForce || force == ForceUnit.Kip;

            return new SafUnits(
                LengthFactor(length),
                ForceFactor(force),
                MassFactor(mass),
                TemperatureStepFactor(temperature),
                angle != AngleUnit.Radian,
                imperialLength || imperialForce,
                imperialLength != imperialForce,
                stated);
        }

        private static double LengthFactor(LengthUnit unit)
        {
            switch (unit)
            {
                case LengthUnit.Millimetre: return 1e-3;
                case LengthUnit.Centimetre: return 1e-2;
                case LengthUnit.Inch: return 0.0254;
                case LengthUnit.Foot: return 0.3048;
                default: return 1.0;
            }
        }

        private static double ForceFactor(ForceUnit unit)
        {
            switch (unit)
            {
                case ForceUnit.Kilonewton: return 1e3;
                case ForceUnit.Meganewton: return 1e6;
                case ForceUnit.PoundForce: return 4.4482216152605;
                case ForceUnit.Kip: return 4448.2216152605;
                default: return 1.0;
            }
        }

        private static double MassFactor(MassUnit unit)
        {
            switch (unit)
            {
                case MassUnit.Tonne: return 1e3;
                case MassUnit.Pound: return 0.45359237;
                case MassUnit.Slug: return 14.5939029372064;
                default: return 1.0;
            }
        }

        private static double TemperatureStepFactor(TemperatureUnit unit)
        {
            // A step of one degree fahrenheit is 5/9 of a kelvin. A step of one
            // degree celsius is exactly one kelvin — which is why this is a ratio
            // table and not Temperature.From*/.Kelvins, whose offset would turn a
            // 30-degree rise into a 303-degree one.
            return unit == TemperatureUnit.Fahrenheit ? 5.0 / 9.0 : 1.0;
        }

        // ---- FEMEX → SAF -------------------------------------------------------

        public Length Length(double value) => UnitsNet.Length.FromMeters(value * LengthToMetres);

        public Force Force(double value) => UnitsNet.Force.FromNewtons(value * ForceToNewtons);

        public Torque Moment(double value) =>
            Torque.FromNewtonMeters(value * ForceToNewtons * LengthToMetres);

        public ForcePerLength ForcePerLength(double value) =>
            UnitsNet.ForcePerLength.FromNewtonsPerMeter(value * ForceToNewtons / LengthToMetres);

        public TorquePerLength MomentPerLength(double value) =>
            TorquePerLength.FromNewtonMetersPerMeter(value * ForceToNewtons);

        public Pressure Pressure(double value) =>
            UnitsNet.Pressure.FromPascals(value * ForceToNewtons / (LengthToMetres * LengthToMetres));

        public Density Density(double value) =>
            UnitsNet.Density.FromKilogramsPerCubicMeter(
                value * MassToKilograms / (LengthToMetres * LengthToMetres * LengthToMetres));

        public Area Area(double value) =>
            UnitsNet.Area.FromSquareMeters(value * LengthToMetres * LengthToMetres);

        public AreaMomentOfInertia SecondMoment(double value) =>
            AreaMomentOfInertia.FromMetersToTheFourth(value * Power(LengthToMetres, 4));

        public Volume SectionModulus(double value) =>
            UnitsNet.Volume.FromCubicMeters(value * Power(LengthToMetres, 3));

        public WarpingMomentOfInertia WarpingConstant(double value) =>
            WarpingMomentOfInertia.FromMetersToTheSixth(value * Power(LengthToMetres, 6));

        public RotationalStiffness RotationalStiffness(double value) =>
            UnitsNet.RotationalStiffness.FromNewtonMetersPerRadian(value * ForceToNewtons * LengthToMetres);

        public RotationalStiffnessPerLength RotationalStiffnessPerLength(double value) =>
            UnitsNet.RotationalStiffnessPerLength.FromNewtonMetersPerRadianPerMeter(value * ForceToNewtons);

        public SpecificWeight SubgradeModulus(double value) =>
            UnitsNet.SpecificWeight.FromNewtonsPerCubicMeter(
                value * ForceToNewtons / Power(LengthToMetres, 3));

        /// <summary>
        /// A temperature <i>difference</i>, expressed the way the SAF cell expresses
        /// it. The cell is a number of degrees celsius and the SDK wraps it in
        /// <see cref="Temperature"/> — an absolute type holding a relative number —
        /// so <c>FromDegreesCelsius</c>/<c>.DegreesCelsius</c> is the pair that
        /// round-trips the cell. <c>FromKelvins</c> would add 273.15 to every
        /// gradient in the file.
        /// </summary>
        public Temperature TemperatureStep(double value) =>
            Temperature.FromDegreesCelsius(value * TemperatureStepToKelvin);

        public Angle Angle(double value) =>
            AngleInDegrees ? UnitsNet.Angle.FromDegrees(value) : UnitsNet.Angle.FromRadians(value);

        public CoefficientOfThermalExpansion ThermalExpansion(double value) =>
            CoefficientOfThermalExpansion.FromInverseKelvin(value / TemperatureStepToKelvin);

        // ---- SAF → FEMEX -------------------------------------------------------
        //
        // The import leg always lands on SI, so these are plain readers of the SI
        // property rather than the inverse of the table above. Keeping them here
        // rather than inline is what stops a caller reaching for .Millimeters
        // because a workbook happened to be written in millimetres.

        public static double Metres(Length value) => value.Meters;

        public static double Metres(Length? value) => value?.Meters ?? 0.0;

        public static double Newtons(Force? value) => value?.Newtons ?? 0.0;

        public static double NewtonMetres(Torque? value) => value?.NewtonMeters ?? 0.0;

        public static double NewtonsPerMetre(ForcePerLength? value) => value?.NewtonsPerMeter ?? 0.0;

        public static double NewtonMetresPerMetre(TorquePerLength? value) =>
            value?.NewtonMetersPerMeter ?? 0.0;

        public static double Pascals(Pressure? value) => value?.Pascals ?? 0.0;

        public static double KilogramsPerCubicMetre(Density? value) =>
            value?.KilogramsPerCubicMeter ?? 0.0;

        public static double SquareMetres(Area? value) => value?.SquareMeters ?? 0.0;

        public static double MetresToTheFourth(AreaMomentOfInertia? value) =>
            value?.MetersToTheFourth ?? 0.0;

        public static double CubicMetres(Volume? value) => value?.CubicMeters ?? 0.0;

        public static double MetresToTheSixth(WarpingMomentOfInertia? value) =>
            value?.MetersToTheSixth ?? 0.0;

        public static double NewtonMetresPerRadian(RotationalStiffness? value) =>
            value?.NewtonMetersPerRadian ?? 0.0;

        public static double NewtonMetresPerRadianPerMetre(RotationalStiffnessPerLength? value) =>
            value?.NewtonMetersPerRadianPerMeter ?? 0.0;

        public static double NewtonsPerCubicMetre(SpecificWeight? value) =>
            value?.NewtonsPerCubicMeter ?? 0.0;

        /// <summary>
        /// The number the SAF cell holds, in degrees celsius. Correct for both the
        /// absolute fibre temperatures and the differences, because in both cases
        /// the cell is a °C number that the SDK happened to wrap in an absolute type.
        /// </summary>
        public static double DegreesCelsius(Temperature? value) => value?.DegreesCelsius ?? 0.0;

        public static double Degrees(Angle value) => value.Degrees;

        public static double InverseKelvin(CoefficientOfThermalExpansion? value) =>
            value?.InverseKelvin ?? 0.0;

        private static double Power(double value, int exponent)
        {
            double result = 1.0;
            for (int i = 0; i < exponent; i++)
                result *= value;

            return result;
        }
    }
}
