using griffel_femex.BoundaryConditions;
using griffel_femex.Geometry;
using griffel_femex.Loads;
using griffel_femex.Loads.Combinations;
using griffel_femex.Materials;
using SAF.DataAccess.Models.Enums;

namespace griffel_femex.Adapters.Saf
{
    /// <summary>
    /// Every enum that crosses, both ways, in one place.
    /// </summary>
    /// <remarks>
    /// Written as tables rather than as casts because two of them are traps. SAF's
    /// member behaviour and FEMEX's <see cref="BarBehaviour"/> have the same four
    /// values in the same order — <c>1.10</c> took them from SAF deliberately — but
    /// the surface behaviour enums have four values each and share only two, so a
    /// cast there would silently mean something different. And SAF's load-case
    /// classification is two columns, <c>Action type</c> and <c>Load type</c>, where
    /// FEMEX has one <see cref="LoadNature"/>; the fold is lossy in one direction
    /// and a guess in the other, which is a fact worth being able to point at.
    /// </remarks>
    internal static class SafEnums
    {
        // ---- Materials ---------------------------------------------------------

        public static MaterialType? ToFemex(ExcelMaterialType? type)
        {
            switch (type)
            {
                case ExcelMaterialType.Concrete: return MaterialType.Concrete;
                case ExcelMaterialType.Steel: return MaterialType.Steel;
                case ExcelMaterialType.Timber: return MaterialType.Timber;
                case ExcelMaterialType.Aluminium: return MaterialType.Aluminium;
                case ExcelMaterialType.Masonry: return MaterialType.Masonry;
                case ExcelMaterialType.Other: return MaterialType.Other;
                default: return null;
            }
        }

        public static ExcelMaterialType ToSaf(MaterialType? type)
        {
            switch (type)
            {
                case MaterialType.Concrete: return ExcelMaterialType.Concrete;
                case MaterialType.Steel: return ExcelMaterialType.Steel;
                case MaterialType.Timber: return ExcelMaterialType.Timber;
                case MaterialType.Aluminium: return ExcelMaterialType.Aluminium;
                case MaterialType.Masonry: return ExcelMaterialType.Masonry;
                default: return ExcelMaterialType.Other;
            }
        }

        // ---- Members -----------------------------------------------------------

        public static BarBehaviour? ToFemex(ExcelCurveBehaviour? behaviour)
        {
            switch (behaviour)
            {
                case ExcelCurveBehaviour.Standard: return BarBehaviour.Standard;
                case ExcelCurveBehaviour.AxialForceOnly: return BarBehaviour.AxialOnly;
                case ExcelCurveBehaviour.CompressionOnly: return BarBehaviour.CompressionOnly;
                case ExcelCurveBehaviour.TensionOnly: return BarBehaviour.TensionOnly;
                default: return null;
            }
        }

        public static ExcelCurveBehaviour ToSaf(BarBehaviour? behaviour)
        {
            switch (behaviour)
            {
                case BarBehaviour.AxialOnly: return ExcelCurveBehaviour.AxialForceOnly;
                case BarBehaviour.CompressionOnly: return ExcelCurveBehaviour.CompressionOnly;
                case BarBehaviour.TensionOnly: return ExcelCurveBehaviour.TensionOnly;
                default: return ExcelCurveBehaviour.Standard;
            }
        }

        public static BarAlignment? ToFemex(ExcelCurveAlignment? alignment)
        {
            switch (alignment)
            {
                case ExcelCurveAlignment.Centre: return BarAlignment.Centre;
                case ExcelCurveAlignment.Top: return BarAlignment.Top;
                case ExcelCurveAlignment.Bottom: return BarAlignment.Bottom;
                case ExcelCurveAlignment.Left: return BarAlignment.Left;
                case ExcelCurveAlignment.Right: return BarAlignment.Right;
                case ExcelCurveAlignment.TopLeft: return BarAlignment.TopLeft;
                case ExcelCurveAlignment.TopRight: return BarAlignment.TopRight;
                case ExcelCurveAlignment.BottomLeft: return BarAlignment.BottomLeft;
                case ExcelCurveAlignment.BottomRight: return BarAlignment.BottomRight;
                default: return null;
            }
        }

        public static ExcelCurveAlignment ToSaf(BarAlignment? alignment)
        {
            switch (alignment)
            {
                case BarAlignment.Top: return ExcelCurveAlignment.Top;
                case BarAlignment.Bottom: return ExcelCurveAlignment.Bottom;
                case BarAlignment.Left: return ExcelCurveAlignment.Left;
                case BarAlignment.Right: return ExcelCurveAlignment.Right;
                case BarAlignment.TopLeft: return ExcelCurveAlignment.TopLeft;
                case BarAlignment.TopRight: return ExcelCurveAlignment.TopRight;
                case BarAlignment.BottomLeft: return ExcelCurveAlignment.BottomLeft;
                case BarAlignment.BottomRight: return ExcelCurveAlignment.BottomRight;
                default: return ExcelCurveAlignment.Centre;
            }
        }

        // ---- Surfaces ----------------------------------------------------------

        /// <summary>
        /// Lossy in both directions and deliberately not a cast. SAF's
        /// <c>Orthotropic</c> has no FEMEX value — directionality was ruled to belong
        /// on the surface property, and that half was never built — and FEMEX's
        /// <c>Plate</c>, bending without membrane action, has no SAF value.
        /// </summary>
        public static PlateBehaviour ToFemex(ExcelMember2DBehaviour? behaviour, out bool exact)
        {
            exact = true;
            switch (behaviour)
            {
                case ExcelMember2DBehaviour.Membrane: return PlateBehaviour.Membrane;
                case ExcelMember2DBehaviour.PressOnly: return PlateBehaviour.CompressionOnly;
                case ExcelMember2DBehaviour.Isotropic: return PlateBehaviour.Shell;
                case ExcelMember2DBehaviour.Orthotropic:
                    exact = false;
                    return PlateBehaviour.Shell;
                default: return PlateBehaviour.Shell;
            }
        }

        public static ExcelMember2DBehaviour ToSaf(PlateBehaviour behaviour, out bool exact)
        {
            exact = true;
            switch (behaviour)
            {
                case PlateBehaviour.Membrane: return ExcelMember2DBehaviour.Membrane;
                case PlateBehaviour.CompressionOnly: return ExcelMember2DBehaviour.PressOnly;
                case PlateBehaviour.Plate:
                    exact = false;
                    return ExcelMember2DBehaviour.Isotropic;
                default: return ExcelMember2DBehaviour.Isotropic;
            }
        }

        public static SurfaceAlignment ToFemex(ExcelMember2DAlignment? alignment)
        {
            switch (alignment)
            {
                case ExcelMember2DAlignment.Top: return SurfaceAlignment.Top;
                case ExcelMember2DAlignment.Bottom: return SurfaceAlignment.Bottom;
                default: return SurfaceAlignment.Centre;
            }
        }

        public static ExcelMember2DAlignment ToSaf(SurfaceAlignment alignment)
        {
            switch (alignment)
            {
                case SurfaceAlignment.Top: return ExcelMember2DAlignment.Top;
                case SurfaceAlignment.Bottom: return ExcelMember2DAlignment.Bottom;
                default: return ExcelMember2DAlignment.Centre;
            }
        }

        public static SurfaceLoadSpanning ToFemex(ExcelSurfaceActionDistributionTo? distribution)
        {
            switch (distribution)
            {
                case ExcelSurfaceActionDistributionTo.OneWayX: return SurfaceLoadSpanning.OneWayX;
                case ExcelSurfaceActionDistributionTo.OneWayY: return SurfaceLoadSpanning.OneWayY;
                default: return SurfaceLoadSpanning.TwoWay;
            }
        }

        public static ExcelSurfaceActionDistributionTo ToSaf(SurfaceLoadSpanning spanning)
        {
            switch (spanning)
            {
                case SurfaceLoadSpanning.OneWayX: return ExcelSurfaceActionDistributionTo.OneWayX;
                case SurfaceLoadSpanning.OneWayY: return ExcelSurfaceActionDistributionTo.OneWayY;
                default: return ExcelSurfaceActionDistributionTo.TwoWay;
            }
        }

        // ---- Loads -------------------------------------------------------------

        /// <summary>
        /// SAF classifies a case twice — <c>Action type</c> (permanent, variable,
        /// accidental) and <c>Load type</c> (self weight, wind, snow, seismic and
        /// nine more) — where FEMEX has one <see cref="LoadNature"/>. The specific
        /// load type wins where it says something the action type does not, because
        /// the action type is the coarser statement of the two.
        /// </summary>
        public static LoadNature ToFemex(ExcelActionType? action, ExcelLoadCaseType? loadType)
        {
            switch (loadType)
            {
                case ExcelLoadCaseType.Wind: return LoadNature.Wind;
                case ExcelLoadCaseType.Snow: return LoadNature.Snow;
                case ExcelLoadCaseType.Seismic: return LoadNature.Seismic;
                case ExcelLoadCaseType.Temperature: return LoadNature.Temperature;
                case ExcelLoadCaseType.SelfWeight: return LoadNature.Dead;
            }

            switch (action)
            {
                case ExcelActionType.Permanent: return LoadNature.Dead;
                case ExcelActionType.Accidental: return LoadNature.Accidental;
                default: return LoadNature.Live;
            }
        }

        public static ExcelActionType ToSafAction(LoadNature nature)
        {
            switch (nature)
            {
                case LoadNature.Dead: return ExcelActionType.Permanent;
                case LoadNature.Accidental: return ExcelActionType.Accidental;
                default: return ExcelActionType.Variable;
            }
        }

        public static ExcelLoadCaseType ToSafLoadType(LoadNature nature, bool carriesSelfWeight)
        {
            if (carriesSelfWeight)
                return ExcelLoadCaseType.SelfWeight;

            switch (nature)
            {
                case LoadNature.Wind: return ExcelLoadCaseType.Wind;
                case LoadNature.Snow: return ExcelLoadCaseType.Snow;
                case LoadNature.Seismic: return ExcelLoadCaseType.Seismic;
                case LoadNature.Temperature: return ExcelLoadCaseType.Temperature;
                default: return ExcelLoadCaseType.Static;
            }
        }

        public static LoadGroupType ToFemex(ExcelLoadGroupType? type, out bool exact)
        {
            exact = true;
            switch (type)
            {
                case ExcelLoadGroupType.Permanent: return LoadGroupType.Permanent;
                case ExcelLoadGroupType.Variable: return LoadGroupType.Variable;
                case ExcelLoadGroupType.Accidental: return LoadGroupType.Accidental;
                case ExcelLoadGroupType.Seismic: return LoadGroupType.Seismic;
                case ExcelLoadGroupType.Tensioning: return LoadGroupType.Tensioning;
                case ExcelLoadGroupType.Moving:
                case ExcelLoadGroupType.Fire:
                    // SAF has seven group types; FEMEX has five. Moving and Fire are
                    // variable actions in every code that names them, so Variable is
                    // the honest fallback — and it is reported, not assumed.
                    exact = false;
                    return LoadGroupType.Variable;
                default: return LoadGroupType.Variable;
            }
        }

        public static ExcelLoadGroupType ToSaf(LoadGroupType type)
        {
            switch (type)
            {
                case LoadGroupType.Permanent: return ExcelLoadGroupType.Permanent;
                case LoadGroupType.Accidental: return ExcelLoadGroupType.Accidental;
                case LoadGroupType.Seismic: return ExcelLoadGroupType.Seismic;
                case LoadGroupType.Tensioning: return ExcelLoadGroupType.Tensioning;
                default: return ExcelLoadGroupType.Variable;
            }
        }

        public static LoadGroupRelation ToFemex(ExcelRelation? relation)
        {
            switch (relation)
            {
                case ExcelRelation.Exclusive: return LoadGroupRelation.Exclusive;
                case ExcelRelation.Together: return LoadGroupRelation.Together;
                default: return LoadGroupRelation.Standard;
            }
        }

        public static ExcelRelation ToSaf(LoadGroupRelation relation)
        {
            switch (relation)
            {
                case LoadGroupRelation.Exclusive: return ExcelRelation.Exclusive;
                case LoadGroupRelation.Together: return ExcelRelation.Together;
                default: return ExcelRelation.Standard;
            }
        }

        /// <summary>
        /// <c>According National Standard</c> is the one that does not cross: it
        /// defers the combination's whole definition to a code clause FEMEX has no
        /// property for, so it arrives <see cref="LimitState.Unspecified"/> and is
        /// reported rather than guessed into Ultimate.
        /// </summary>
        public static LimitState ToFemex(ExcelLoadCaseCombinationCategory? category, out bool exact)
        {
            exact = true;
            switch (category)
            {
                case ExcelLoadCaseCombinationCategory.UltimateLimitState: return LimitState.Ultimate;
                case ExcelLoadCaseCombinationCategory.ServiceabilityLimitState: return LimitState.Serviceability;
                case ExcelLoadCaseCombinationCategory.AccidentalLimitState: return LimitState.Accidental;
                case ExcelLoadCaseCombinationCategory.AccordingNationalStandard:
                    exact = false;
                    return LimitState.Unspecified;
                default: return LimitState.Unspecified;
            }
        }

        public static ExcelLoadCaseCombinationCategory ToSaf(LimitState state)
        {
            switch (state)
            {
                case LimitState.Ultimate: return ExcelLoadCaseCombinationCategory.UltimateLimitState;
                case LimitState.Serviceability: return ExcelLoadCaseCombinationCategory.ServiceabilityLimitState;
                case LimitState.Accidental: return ExcelLoadCaseCombinationCategory.AccidentalLimitState;
                default: return ExcelLoadCaseCombinationCategory.NotDefined;
            }
        }

        public static LoadCombinationType ToFemex(ExcelLoadCaseCombinationType? type)
        {
            switch (type)
            {
                case ExcelLoadCaseCombinationType.Envelope: return LoadCombinationType.Envelope;
                default: return LoadCombinationType.LinearAdd;
            }
        }

        /// <summary>
        /// <c>AbsoluteAdd</c> and <c>Srss</c> have no SAF value at all. They come
        /// back as <c>Linear</c>, which is a different combination, so the caller
        /// must report the difference rather than let the enum swallow it.
        /// </summary>
        public static ExcelLoadCaseCombinationType ToSaf(LoadCombinationType type, out bool exact)
        {
            exact = type == LoadCombinationType.LinearAdd || type == LoadCombinationType.Envelope;
            return type == LoadCombinationType.Envelope
                ? ExcelLoadCaseCombinationType.Envelope
                : ExcelLoadCaseCombinationType.Linear;
        }

        public static LoadDirection ToFemex(ExcelActionDirection? direction)
        {
            switch (direction)
            {
                case ExcelActionDirection.X: return LoadDirection.X;
                case ExcelActionDirection.Y: return LoadDirection.Y;
                case ExcelActionDirection.Vector: return LoadDirection.Vector;
                default: return LoadDirection.Z;
            }
        }

        public static ExcelActionDirection ToSaf(LoadDirection direction)
        {
            switch (direction)
            {
                case LoadDirection.X: return ExcelActionDirection.X;
                case LoadDirection.Y: return ExcelActionDirection.Y;
                case LoadDirection.Vector: return ExcelActionDirection.Vector;
                default: return ExcelActionDirection.Z;
            }
        }

        public static LoadCoordinateSystem ToFemex(ExcelCoordinateSystem? system)
        {
            return system == ExcelCoordinateSystem.Local
                ? LoadCoordinateSystem.Local
                : LoadCoordinateSystem.Global;
        }

        public static ExcelCoordinateSystem ToSaf(LoadCoordinateSystem system)
        {
            return system == LoadCoordinateSystem.Local
                ? ExcelCoordinateSystem.Local
                : ExcelCoordinateSystem.Global;
        }

        // ---- Restraints --------------------------------------------------------

        /// <summary>
        /// SAF states eight constraint types; FEMEX states fixed-or-free, an optional
        /// stiffness, and a sense. Seven of the eight land. The eighth,
        /// <c>Non linear</c>, is a resistance curve rather than a sense — it appears
        /// in the very first published workbook, with a negative stiffness — and it
        /// comes back free, with a message, rather than resisting in a way the source
        /// did not say.
        /// </summary>
        public static bool TryToFemex(ExcelConstraintType? type, out bool isFixed,
                                      out bool usesStiffness, out RestraintSense? sense)
        {
            isFixed = false;
            usesStiffness = false;
            sense = null;

            switch (type)
            {
                case ExcelConstraintType.Free:
                    return true;
                case ExcelConstraintType.Rigid:
                    isFixed = true;
                    return true;
                case ExcelConstraintType.Flexible:
                    usesStiffness = true;
                    return true;
                case ExcelConstraintType.CompressionOnly:
                    isFixed = true;
                    sense = RestraintSense.CompressionOnly;
                    return true;
                case ExcelConstraintType.TensionOnly:
                    isFixed = true;
                    sense = RestraintSense.TensionOnly;
                    return true;
                case ExcelConstraintType.FlexibleCompressionOnly:
                    usesStiffness = true;
                    sense = RestraintSense.CompressionOnly;
                    return true;
                case ExcelConstraintType.FlexibleTensionOnly:
                    usesStiffness = true;
                    sense = RestraintSense.TensionOnly;
                    return true;
                case ExcelConstraintType.NonLinear:
                    return false;
                default:
                    return true;
            }
        }

        public static ExcelConstraintType ToSaf(Restraint restraint)
        {
            bool flexible = restraint.Stiffness.HasValue;
            switch (restraint.Sense)
            {
                case RestraintSense.CompressionOnly:
                    return flexible
                        ? ExcelConstraintType.FlexibleCompressionOnly
                        : ExcelConstraintType.CompressionOnly;
                case RestraintSense.TensionOnly:
                    return flexible
                        ? ExcelConstraintType.FlexibleTensionOnly
                        : ExcelConstraintType.TensionOnly;
            }

            if (flexible)
                return ExcelConstraintType.Flexible;

            return restraint.Fixed ? ExcelConstraintType.Rigid : ExcelConstraintType.Free;
        }

        /// <summary>
        /// The same restraint, for the sheets whose validator accepts only five of
        /// the eight constraint types.
        /// </summary>
        /// <remarks>
        /// SAF's line-support and edge-support sheets do not allow the two combined
        /// values — flexible <i>and</i> one-directional — that the point-support sheet
        /// does. Something has to give, and it is the stiffness rather than the sense:
        /// a restraint that resists in one direction only and resists rigidly is wrong
        /// by a stiffness, where one that is flexible in both directions is wrong
        /// about whether the structure lifts off, which is the difference the model
        /// was built to show.
        /// </remarks>
        public static ExcelConstraintType ToSafNarrow(Restraint restraint, out bool exact)
        {
            ExcelConstraintType full = ToSaf(restraint);
            switch (full)
            {
                case ExcelConstraintType.FlexibleCompressionOnly:
                    exact = false;
                    return ExcelConstraintType.CompressionOnly;
                case ExcelConstraintType.FlexibleTensionOnly:
                    exact = false;
                    return ExcelConstraintType.TensionOnly;
                default:
                    exact = true;
                    return full;
            }
        }

        public static ExcelConstraintType ToSaf(Release release)
        {
            if (!release.Released)
                return ExcelConstraintType.Rigid;

            return release.ResidualStiffness.HasValue
                ? ExcelConstraintType.Flexible
                : ExcelConstraintType.Free;
        }

        public static void ToFemex(ExcelConstraintType? type, double stiffness, Release release)
        {
            switch (type)
            {
                case ExcelConstraintType.Rigid:
                case null:
                    release.Released = false;
                    break;
                case ExcelConstraintType.Free:
                    release.Released = true;
                    break;
                case ExcelConstraintType.Flexible:
                case ExcelConstraintType.FlexibleCompressionOnly:
                case ExcelConstraintType.FlexibleTensionOnly:
                    release.Released = true;
                    release.ResidualStiffness = stiffness;
                    break;
                default:
                    // Compression-only, tension-only and non-linear hinges are a
                    // resistance rule FEMEX's Release cannot state. Left continuous,
                    // which is the conservative reading, and reported by the caller.
                    release.Released = false;
                    break;
            }
        }
    }
}
