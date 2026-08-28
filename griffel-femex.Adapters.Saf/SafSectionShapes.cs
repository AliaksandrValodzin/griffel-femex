using System;
using System.Collections.Generic;
using System.Linq;
using griffel_femex.Geometry.Sections;
using griffel_femex.Interop;
using SAF.DataAccess.Models.Enums;
using SAF.DataAccess.Models.Libraries;
using Length = UnitsNet.Length;

namespace griffel_femex.Adapters.Saf
{
    /// <summary>
    /// Cross-section shapes, both ways, and the one part of this adapter where the
    /// evidence runs out mid-table.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SAF states a parametric section as a <c>Shape</c> from a 45-value library and
    /// an ordered <c>Parameters</c> list. The SDK exposes the list and not its
    /// meaning: the per-shape order is in the specification, which is not in the
    /// package. So the order was measured off the published corpus where the corpus
    /// settles it, and inferred where it does not — and the difference is reported
    /// rather than smoothed over, because a depth read as a width is precisely the
    /// silently-changed answer this product exists to catch.
    /// </para>
    /// <para>
    /// <b>Settled by the corpus.</b> <c>CS7</c> is a symmetric steel I-section:
    /// <c>[500, 200, 200, 25, 25, 15]</c>. Only one reading of six numbers gives a
    /// 500-deep I with 200-wide, 25-thick flanges and a 15-thick web, so the
    /// I-section order is depth, both flange widths, both flange thicknesses, web.
    /// Three <c>Rectangle</c> rows and two <c>Circle</c> rows fix those, and
    /// <c>Pipe</c>'s two numbers, <c>[150, 8]</c>, admit only one reading.
    /// </para>
    /// <para>
    /// <b>Inferred.</b> T, L, angle, channel and tube follow the same convention —
    /// depth, width, then thicknesses in the same order the I-section uses. Every
    /// section built that way carries the <see cref="SafLoss.InferredShapeParameters"/>
    /// message. Closing it is an afternoon with the specification's parameter tables
    /// and is recorded as such.
    /// </para>
    /// <para>
    /// <b>Everything else is generic.</b> Oval, haunched I, double and triple
    /// rectangle, T-tee, and the thirty-odd shapes the corpus does not exercise, plus
    /// every asymmetric I — carried as <see cref="GenericSection"/> with whatever
    /// stiffness the workbook stated. That is <i>Approximated</i>, not a failure: a
    /// generic section with the right A and I analyses correctly and draws wrongly.
    /// </para>
    /// </remarks>
    internal static class SafSectionShapes
    {
        /// <summary>
        /// P5's form-code table, confirmed against the corpus: the reference file's
        /// three manufactured IPE180 rows carry <c>Form code = 1</c>, which is
        /// <c>ISection</c>. Codes 1-8 are FEMEX's eight discriminators exactly, so
        /// eight of nine section kinds have a known code and only <c>generic</c>
        /// needs the provisional 0.
        /// </summary>
        public static int FormCodeFor(Section section)
        {
            switch (section)
            {
                case ISection _: return 1;
                case Box _: return 2;
                case Pipe _: return 3;
                case Angle _: return 4;
                case Channel _: return 5;
                case TSection _: return 6;
                case Rectangle _: return 7;
                case Circle _: return 8;
                default: return 0;
            }
        }

        public static ExcelProfileLibraryId? ShapeFor(Section section)
        {
            switch (section)
            {
                case ISection _: return ExcelProfileLibraryId.ISection;
                case Box _: return ExcelProfileLibraryId.Tube;
                case Pipe _: return ExcelProfileLibraryId.Pipe;
                case Angle _: return ExcelProfileLibraryId.LSection;
                case Channel _: return ExcelProfileLibraryId.Channel;
                case TSection _: return ExcelProfileLibraryId.TSection;
                case Rectangle _: return ExcelProfileLibraryId.Rectangle;
                case Circle _: return ExcelProfileLibraryId.Circle;
                default: return null;
            }
        }

        /// <summary>The dimension list to write, in the order <see cref="ToFemex"/> reads.</summary>
        public static Length[] ParametersFor(Section section, SafUnits units)
        {
            switch (section)
            {
                case Rectangle rectangle:
                    return new[] { units.Length(rectangle.Depth), units.Length(rectangle.Width) };
                case Circle circle:
                    return new[] { units.Length(circle.Diameter) };
                case Pipe pipe:
                    return new[] { units.Length(pipe.Diameter), units.Length(pipe.WallThickness) };
                case ISection i:
                    return new[]
                    {
                        units.Length(i.TotalDepth), units.Length(i.FlangeWidth), units.Length(i.FlangeWidth),
                        units.Length(i.FlangeThickness), units.Length(i.FlangeThickness),
                        units.Length(i.WebThickness),
                    };
                case TSection t:
                    return new[]
                    {
                        units.Length(t.TotalDepth), units.Length(t.FlangeWidth),
                        units.Length(t.FlangeThickness), units.Length(t.WebThickness),
                    };
                case Channel c:
                    return new[]
                    {
                        units.Length(c.TotalDepth), units.Length(c.FlangeWidth),
                        units.Length(c.WebThickness), units.Length(c.FlangeThickness),
                        Length.Zero,
                    };
                case Angle a:
                    return new[]
                    {
                        units.Length(a.LegLengthZ), units.Length(a.LegLengthY),
                        units.Length(a.Thickness), units.Length(a.Thickness),
                    };
                case Box b:
                    return new[]
                    {
                        units.Length(b.Depth), units.Length(b.Width),
                        units.Length(b.WallThickness), units.Length(b.WallThickness),
                        Length.Zero,
                    };
                default:
                    return new Length[0];
            }
        }

        public static Section ToFemex(ExcelStructuralCrossSection source, SafMessageLog log, int id)
        {
            var reference = new ObjectRef(FemexEntity.Section, id, SafIdentity.UidOf(source));
            Section section = Build(source, log, reference);

            section.Properties = Properties(source);

            // Only a manufactured section names a catalogue profile. A General
            // section also carries a Profile string, but it is the name of the
            // polygon definition rather than a library designation — reading it as a
            // catalogue entry would claim the section came out of a steel table.
            if (source.CrossSectionType == ExcelCrossSectionType.Manufactured &&
                !string.IsNullOrWhiteSpace(source.Profile))
            {
                // Profile crosses; the library it came from does not. SAF's
                // Description ID is a shape classification — European I beam, cold
                // formed channel — and not a library name, so putting it in
                // SectionCatalogue.Source would answer FEMEX's "which table is this
                // designation from" question with something that is not an answer.
                section.Catalogue = new SectionCatalogue { Profile = source.Profile };

                if (source.DescriptionId > 0)
                    log.Concept(SafLoss.DroppedSectionDescription);
            }

            return section;
        }

        private static Section Build(ExcelStructuralCrossSection source, SafMessageLog log,
                                     ObjectRef reference)
        {
            double[] p = (source.Parameters ?? new Length[0]).Select(SafUnits.Metres).ToArray();
            string handle = source.Name ?? string.Empty;

            switch (source.Shape)
            {
                case ExcelProfileLibraryId.Rectangle when p.Length >= 2:
                    return new Rectangle { Depth = p[0], Width = p[1] };

                case ExcelProfileLibraryId.Circle when p.Length >= 1:
                    return new Circle { Diameter = p[0] };

                case ExcelProfileLibraryId.Pipe when p.Length >= 2:
                    return new Pipe { Diameter = p[0], WallThickness = p[1] };

                case ExcelProfileLibraryId.ISection when p.Length >= 6:
                    if (Near(p[1], p[2]) && Near(p[3], p[4]))
                    {
                        return new ISection
                        {
                            TotalDepth = p[0],
                            FlangeWidth = p[1],
                            FlangeThickness = p[3],
                            WebThickness = p[5],
                        };
                    }

                    // Asymmetric flanges. FEMEX's I-section is symmetric by
                    // construction, so taking one flange and discarding the other
                    // would move the elastic centroid without saying so.
                    log.Object(SafLoss.GenericSection, reference, handle,
                               "The I-section has unequal flanges.");
                    return new GenericSection();

                case ExcelProfileLibraryId.TSection when p.Length >= 4:
                    log.Concept(SafLoss.InferredShapeParameters);
                    return new TSection
                    {
                        TotalDepth = p[0],
                        FlangeWidth = p[1],
                        FlangeThickness = p[2],
                        WebThickness = p[3],
                    };

                case ExcelProfileLibraryId.Channel when p.Length >= 4:
                case ExcelProfileLibraryId.CSection when p.Length >= 4:
                case ExcelProfileLibraryId.USection when p.Length >= 4:
                    log.Concept(SafLoss.InferredShapeParameters);
                    if (p.Length > 4)
                        log.Object(SafLoss.SimplifiedSectionShape, reference, handle, "A root radius was stated.");

                    return new Channel
                    {
                        TotalDepth = p[0],
                        FlangeWidth = p[1],
                        WebThickness = p[2],
                        FlangeThickness = p[3],
                    };

                case ExcelProfileLibraryId.LSection when p.Length >= 3:
                case ExcelProfileLibraryId.Angle when p.Length >= 3:
                    log.Concept(SafLoss.InferredShapeParameters);
                    if (p.Length > 3 && !Near(p[2], p[3]))
                    {
                        log.Object(SafLoss.SimplifiedSectionShape, reference, handle,
                                   "The two legs state different thicknesses; the first was taken.");
                    }

                    return new Angle { LegLengthZ = p[0], LegLengthY = p[1], Thickness = p[2] };

                case ExcelProfileLibraryId.Tube when p.Length >= 3:
                case ExcelProfileLibraryId.Box when p.Length >= 3:
                    log.Concept(SafLoss.InferredShapeParameters);
                    if (p.Length > 3 && !Near(p[2], p[3]))
                    {
                        log.Object(SafLoss.SimplifiedSectionShape, reference, handle,
                                   "The web and flange walls differ; the web thickness was taken.");
                    }

                    return new Box { Depth = p[0], Width = p[1], WallThickness = p[2] };

                case null when source.CrossSectionType == ExcelCrossSectionType.Manufactured:
                    // A catalogue profile states no dimensions at all — the receiving
                    // program looks the name up. Generic is not an approximation here,
                    // it is what the workbook said.
                    return new GenericSection();

                default:
                    log.Object(SafLoss.GenericSection, reference, handle,
                               source.Shape.HasValue
                                   ? $"The SAF shape is {source.Shape.Value}."
                                   : "The section states no parametric shape.");
                    return new GenericSection();
            }
        }

        private static SectionProperties? Properties(ExcelStructuralCrossSection source)
        {
            // The one place the export leg loses nothing: SAF's seven optional
            // stiffness columns are a subset of SectionProperties' eleven. J is SAF's
            // It, and ShearAreaY/Z, Wely and Welz have no SAF column at all.
            if (source.CrossSectionalPropertiesA is null && source.CrossSectionalPropertiesIy is null &&
                source.CrossSectionalPropertiesIz is null && source.CrossSectionalPropertiesIt is null &&
                source.CrossSectionalPropertiesIw is null && source.CrossSectionalPropertiesWply is null &&
                source.CrossSectionalPropertiesWplz is null)
            {
                return null;
            }

            return new SectionProperties
            {
                Area = Optional(source.CrossSectionalPropertiesA, SafUnits.SquareMetres),
                Iy = Optional(source.CrossSectionalPropertiesIy, SafUnits.MetresToTheFourth),
                Iz = Optional(source.CrossSectionalPropertiesIz, SafUnits.MetresToTheFourth),
                J = Optional(source.CrossSectionalPropertiesIt, SafUnits.MetresToTheFourth),
                Iw = Optional(source.CrossSectionalPropertiesIw, SafUnits.MetresToTheSixth),
                Wply = Optional(source.CrossSectionalPropertiesWply, SafUnits.CubicMetres),
                Wplz = Optional(source.CrossSectionalPropertiesWplz, SafUnits.CubicMetres),
            };
        }

        private static double? Optional<T>(T? value, Func<T?, double> read) where T : struct
        {
            return value.HasValue ? read(value) : (double?)null;
        }

        private static bool Near(double a, double b)
        {
            double scale = Math.Max(Math.Abs(a), Math.Abs(b));
            return Math.Abs(a - b) <= Math.Max(1e-12, scale * 1e-9);
        }
    }
}
