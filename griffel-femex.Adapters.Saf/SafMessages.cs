using System;
using System.Collections.Generic;
using griffel_femex.Interop;

namespace griffel_femex.Adapters.Saf
{
    /// <summary>
    /// The declared-loss catalogue of <c>FEMEX_SAF_Fit.md</c> §8.2, as data.
    /// </summary>
    /// <remarks>
    /// The catalogue is the specification, so it is also the checklist — which only
    /// works if there is one copy of it. This is that copy: the mapping code names a
    /// <see cref="SafLoss"/> and never writes the prose, and the coverage test reads
    /// the same table rather than matching on strings it had to guess.
    /// </remarks>
    public static class SafMessages
    {
        /// <summary>One catalogue entry: what the loss is, which leg it happens on, and how it is anchored.</summary>
        public sealed class Entry
        {
            internal Entry(SafLoss loss, LossCategory category, TransferDirection direction,
                           FemexEntity? entity, bool perObject, string text)
            {
                Loss = loss;
                Category = category;
                Direction = direction;
                Entity = entity;
                PerObject = perObject;
                Text = text;
            }

            public SafLoss Loss { get; }

            public LossCategory Category { get; }

            /// <summary>The leg this loss happens on. Several happen on both.</summary>
            public TransferDirection Direction { get; }

            /// <summary>
            /// What a message about this loss anchors to. Null means the loss is
            /// about the model itself — <c>TransferMessage.ModelLoss</c>, which
            /// Deviation 1 of the contract reserves for exactly three facts and
            /// their kin: an assumed unit system, an invented gravity, a stale schema.
            /// </summary>
            public FemexEntity? Entity { get; }

            /// <summary>
            /// True where §4.4 wants one message per object; false where it wants
            /// one per concept — <i>"142 members carried stiffness modifiers"</i>.
            /// Annotation columns are per concept because forty-two identical
            /// messages about <c>Layer</c> are noise, not a report.
            /// </summary>
            public bool PerObject { get; }

            public string Text { get; }
        }

        private static readonly Dictionary<SafLoss, Entry> Table = Build();

        /// <summary>Every declared loss, keyed by its name.</summary>
        public static IReadOnlyDictionary<SafLoss, Entry> Catalogue => Table;

        public static Entry For(SafLoss loss)
        {
            if (!Table.TryGetValue(loss, out Entry? entry))
                throw new ArgumentOutOfRangeException(nameof(loss), loss, "No catalogue entry.");

            return entry;
        }

        private static Dictionary<SafLoss, Entry> Build()
        {
            var table = new Dictionary<SafLoss, Entry>();

            void Add(SafLoss loss, LossCategory category, TransferDirection direction,
                     FemexEntity? entity, bool perObject, string text)
            {
                table[loss] = new Entry(loss, category, direction, entity, perObject, text);
            }

            const TransferDirection In = TransferDirection.Import;
            const TransferDirection Out = TransferDirection.Export;

            // ---- Invented ------------------------------------------------------

            Add(SafLoss.SynthesisedLevel, LossCategory.Invented, In, FemexEntity.Level, true,
                "SAF places a node by three free coordinates; FEMEX places one on a Level and an " +
                "offset, and the level reference is required. This level was synthesised from the " +
                "elevations the workbook uses. Nothing in SAF references a storey, so a model with " +
                "no storey meaning at all acquires levels the source never had.");

            Add(SafLoss.StampedUnitSystem, LossCategory.Invented, In, null, false,
                "SAF states one coarse flag, Metric or Imperial; FEMEX states five typed units. The " +
                "model was normalised to metre, newton, celsius, degree and kilogram, which is a " +
                "finer statement than the workbook made.");

            Add(SafLoss.RestatedInSiUnits, LossCategory.Approximated, In, null, false,
                "Every number of this kind was restated in metre-newton units. The SDK resolves each " +
                "cell into a typed quantity before this adapter sees it, so the workbook's own units " +
                "are gone by then and one canonical answer is the only one available. Nothing " +
                "changed physically and every stated number did: a model exported in kilonewtons " +
                "comes back in newtons.");

            Add(SafLoss.AttachedToInventedLoadGroup, LossCategory.Invented, In, FemexEntity.LoadCase, false,
                "SAF requires every load case to reference a load group. Cases in this model " +
                "referenced none, so the export synthesised groups for them and the case now names " +
                "one it did not name before.");

            Add(SafLoss.DroppedDesignEnvelopeFlag, LossCategory.Dropped, Out,
                FemexEntity.LoadCombination, false,
                "FEMEX marks a combination as included in the design envelope or excluded from it. " +
                "SAF has no such column, so an excluded combination arrives at the far end included " +
                "— which changes what a design check reports, not what an analysis computes.");

            Add(SafLoss.MintedUids, LossCategory.Invented, In, null, false,
                "Rows in this workbook carried no Id, so their uids were minted rather than read. A " +
                "uid that was minted is not provenance and must not be presented as one.");

            Add(SafLoss.PlaceholderSection, LossCategory.Invented, In, FemexEntity.Section, true,
                "The member names no cross-section that this workbook defines, and a FEMEX bar must " +
                "have one. A placeholder with zero properties was created — zero rather than nominal, " +
                "so nothing downstream can return a confident wrong answer against it.");

            Add(SafLoss.InventedGravity, LossCategory.Invented, In, null, false,
                "SAF states which global axis is vertical and never states gravity itself. FEMEX " +
                "states a direction and a magnitude on the model root, so the imported model " +
                "carries the standard downward 9.80665 m/s2 — which the workbook did not say.");

            Add(SafLoss.InventedSystemOfUnits, LossCategory.Invented, Out, null, false,
                "SAF requires Model.System of units and reads it before any sheet, driving every " +
                "conversion in the file from it. FEMEX states five typed units instead, which permit " +
                "combinations SAF has no flag for, so this value was decided rather than read.");

            Add(SafLoss.InventedNationalCode, LossCategory.Invented, Out, null, false,
                "SAF requires Model.National code. FEMEX deliberately holds no national annex, so " +
                "EC-Standard-EN was written — the value both published reference workbooks use.");

            Add(SafLoss.InventedCrossSectionLcs, LossCategory.Invented, Out, null, false,
                "SAF requires Model.LCS of cross-section. FEMEX has one fixed convention and states " +
                "it nowhere, so ZYX was asserted — the value both published reference workbooks use.");

            Add(SafLoss.InventedSafVersion, LossCategory.Invented, Out, null, false,
                "SAF requires a specification version. FEMEX's schemaVersion is a statement about " +
                "FEMEX, not about SAF, so the version written is the SDK's own: " +
                SafGateway.WrittenSpecVersion + ".");

            Add(SafLoss.SynthesisedNames, LossCategory.Invented, Out, null, false,
                "SAF keys every sheet by Name and treats a duplicate within a sheet as fatal. Bar, " +
                "Node, Support and Hinge carry no name in FEMEX at all, so names were synthesised " +
                "for four of the largest sheets in the file. A round trip through FEMEX therefore " +
                "renames most of the model — legal, visible, and worth knowing once.");

            Add(SafLoss.InventedLoadGroup, LossCategory.Invented, Out, FemexEntity.LoadGroup, true,
                "SAF requires every load case to reference a load group. This group was synthesised " +
                "from the case's nature. Its Relation is the invented part: the two published " +
                "reference workbooks disagree about which relation wind and snow groups take, which " +
                "is the proof that choosing one is guessing.");

            Add(SafLoss.InventedMemberEccentricity, LossCategory.Invented, Out, null, false,
                "SAF requires System line and the four analysis eccentricities on every member. " +
                "Where FEMEX states none, Centre and zero were written. Reported once for the model " +
                "rather than once per member: the value is zero on every row of every file in the " +
                "published corpus, so the invention is almost always right and saying it forty-two " +
                "times is noise.");

            Add(SafLoss.InventedFormCode, LossCategory.Invented, Out, FemexEntity.Section, true,
                "SAF's Form code discriminates the shape. FEMEX's eight parametric shapes map onto " +
                "codes 1-8 exactly, but this section is generic, so 0 was written — the provisional " +
                "code, which tells the receiver the shape is unknown rather than asserting a wrong one.");

            Add(SafLoss.InventedSectionMaterial, LossCategory.Invented, Out, FemexEntity.Section, true,
                "SAF puts the material on the cross-section and requires it. No member in this " +
                "model uses this section, so there is no member to take a material from; the " +
                "model's first material was written so that the workbook opens.");

            Add(SafLoss.InventedPasternakSubsoil, LossCategory.Invented, Out, FemexEntity.Support, false,
                "SAF requires the Pasternak shear terms C2x and C2y on a subsoil connection. FEMEX " +
                "has no property for either, so zero was written — a Winkler bed with no shear " +
                "layer, which is a statement the model did not make.");

            Add(SafLoss.InventedLocalFrame, LossCategory.Invented, Out, null, false,
                "SAF requires a local-frame reference on every member and every surface — a type " +
                "and a vector, four mandatory columns, and its own validator refuses the workbook " +
                "without them. FEMEX states a roll angle about an axis whose default frame is a " +
                "rule rather than a vector, so the reference written is SAF's own default for that " +
                "form — global Y for a member, global X for a surface — with FEMEX's angle applied " +
                "on top. Where FEMEX's default rule and SAF's differ for a particular member " +
                "orientation, so will the section's orientation about its axis.");

            // ---- Dropped, export leg ------------------------------------------

            Add(SafLoss.DroppedGrids, LossCategory.Dropped, Out, FemexEntity.Grid, false,
                "SAF has no grid or gridline concept. The model's grids do not cross.");

            Add(SafLoss.DroppedMesh, LossCategory.Dropped, Out, FemexEntity.Mesh, false,
                "SAF carries analysis results but not the mesh they were computed on. The model's " +
                "mesh does not cross.");

            Add(SafLoss.DroppedRegionPriority, LossCategory.Dropped, Out, FemexEntity.Plate, false,
                "FEMEX resolves overlapping regions by Priority, with a total and deterministic tie " +
                "rule. SAF's region and opening sheets carry no precedence field of any kind, so " +
                "overlapping regions arrive as undefined behaviour at the far end.");

            Add(SafLoss.DroppedPlateBehaviour, LossCategory.Dropped, Out, FemexEntity.Plate, true,
                "The plate is bending-only. SAF's behaviour column has Isotropic, Orthotropic, " +
                "Membrane and Press only, and no value for bending without membrane action; it was " +
                "written as Isotropic.");

            Add(SafLoss.DroppedCombinationType, LossCategory.Dropped, Out, FemexEntity.LoadCombination, true,
                "SAF's combination types are Linear, Envelope and Non linear. An absolute-sum or " +
                "SRSS combination has no SAF value and was written as Linear, which is a different " +
                "combination.");

            Add(SafLoss.DroppedLevelProperties, LossCategory.Dropped, Out, FemexEntity.Level, false,
                "SAF's StructuralStorey is a name and a height. IsGround, RelativeElevation and the " +
                "grid references do not cross.");

            Add(SafLoss.DissolvedSurfaceProperty, LossCategory.Dropped, Out, FemexEntity.SurfaceProperty, false,
                "FEMEX names a surface property once and lets many plates share it. SAF states the " +
                "thickness on each surface and has no sheet for the property itself, so the shared " +
                "object dissolves into per-surface numbers. The thicknesses are exact; the sharing " +
                "is not, so a round trip returns one property per distinct thickness, with new " +
                "identity and no memory of which plates were deliberately alike.");

            Add(SafLoss.DroppedSectionProperties, LossCategory.Dropped, Out, FemexEntity.Section, false,
                "SAF's seven optional stiffness columns are a subset of FEMEX's eleven. The two " +
                "shear areas and the two elastic section moduli have no SAF column and do not cross. " +
                "This is the one direction in which FEMEX's section record is the richer of the two.");

            Add(SafLoss.SharedSectionMaterial, LossCategory.Dropped, Out, FemexEntity.Section, false,
                "SAF puts the material on the cross-section; FEMEX puts it on the member. Two " +
                "members in this model give one section different materials, which SAF cannot " +
                "state: the section was written with the first material found, so the other " +
                "members changed material in the crossing.");

            Add(SafLoss.NarrowedLineRestraint, LossCategory.Approximated, Out, FemexEntity.Support, true,
                "SAF's line and edge support sheets accept five of its eight constraint types, and " +
                "not the two that are flexible in one direction only. The sense was kept and the " +
                "stiffness was not: a support that lifts off and resists rigidly is wrong by a " +
                "stiffness, where one that is flexible both ways is wrong about whether it lifts off.");

            Add(SafLoss.UnplaceableAreaLoad, LossCategory.Dropped, Out, FemexEntity.Load, true,
                "The area load is bounded by its own polygon of node numbers rather than applied to a " +
                "surface. SAF's surface action must name a surface, a region or a load panel, so " +
                "there is nothing for this load to be applied to and it was not written.");

            Add(SafLoss.FlattenedLoadDirection, LossCategory.Approximated, Out, FemexEntity.Load, true,
                "The load acts along a stated vector. SAF states a load direction as one axis, and " +
                "its vector columns are not something the SDK can write, so the load was written " +
                "along the axis its vector leans on hardest and the other two components did not " +
                "cross. The magnitude is unchanged, so the load is the right size in the wrong " +
                "direction — which is a difference a check report should show and a receiving " +
                "program will not.");

            Add(SafLoss.UnplaceableLinearLoad, LossCategory.Dropped, Out, FemexEntity.Load, true,
                "A FEMEX linear load names either a bar or two nodes. This one names two nodes that "  +
                "are not a contour edge of any plate in the model, so SAF has nothing for the run to "  +
                "lie on and the load was not written rather than being attached somewhere plausible.");

            Add(SafLoss.UnplaceableEdgeSupport, LossCategory.Dropped, Out, FemexEntity.Support, true,
                "A FEMEX linear support names an edge by its two nodes. SAF names it by a surface " +
                "and an index into that surface's contour, and this pair is not a contour edge of " +
                "any plate in the model — so the support has nothing to attach to and was not " +
                "written, rather than being attached somewhere plausible.");

            Add(SafLoss.UnplaceableSurfaceSupport, LossCategory.Dropped, Out, FemexEntity.Support, true,
                "A FEMEX area support names either a plate or a free polygon of nodes. This one names " +
                "no plate, and SAF's surface connection makes the surface it acts on mandatory — so " +
                "the support has nothing to attach to and was not written. The alternative, a row " +
                "naming no surface, is one SAF's own validator refuses, which would cost the whole " +
                "workbook rather than one support.");

            Add(SafLoss.DroppedSurfaceThermalGradient, LossCategory.Dropped, Out, FemexEntity.Load, true,
                "The temperature load states an in-plane gradient on a surface. SAF's surface " +
                "thermal action carries a top and a bottom fibre temperature and nothing across the " +
                "plane, so the in-plane half did not cross.");

            // ---- Dropped, import leg ------------------------------------------

            Add(SafLoss.DroppedMemberType, LossCategory.Dropped, In, FemexEntity.Bar, false,
                "StructuralCurveMember.Type — Column, Rafter, Purlin and fourteen more — is " +
                "annotation with no analysis meaning, and FEMEX has no property for it.");

            Add(SafLoss.DroppedLayerAndColour, LossCategory.Dropped, In, null, false,
                "Layer and Color are carried on members, surfaces, ribs and proxy elements. Both are " +
                "presentation, and FEMEX holds neither.");

            Add(SafLoss.DroppedLoadEccentricity, LossCategory.Dropped, In, FemexEntity.Load, false,
                "StructuralCurveAction states an eccentricity ey/ez from the member axis. A FEMEX " +
                "linear load is applied on the axis.");

            Add(SafLoss.DroppedSectionDescription, LossCategory.Dropped, In, FemexEntity.Section, false,
                "A manufactured cross-section states a Description ID — a shape classification from a " +
                "list of over a hundred: European I beam, cold formed channel, American wide flange. " +
                "FEMEX carries the profile designation and the manufacture, and has no field for the " +
                "classification, so it does not cross.");

            Add(SafLoss.DroppedMaterialSubtype, LossCategory.Dropped, In, FemexEntity.Material, false,
                "StructuralMaterial.Subtype has no FEMEX property. Type and Quality both cross.");

            Add(SafLoss.DroppedLoadCaseDuration, LossCategory.Dropped, In, FemexEntity.LoadCase, false,
                "StructuralLoadCase.Duration — Long, Medium, Short, Instantaneous — has no FEMEX " +
                "property. It matters to timber design and to nothing in the FEMEX format.");

            Add(SafLoss.DroppedLoadGroupCategory, LossCategory.Dropped, In, FemexEntity.LoadGroup, false,
                "A SAF load group states both a type and a category — Domestic, Roofs, Snow, Wind " +
                "and nine more. FEMEX's load group carries the type and the relation and no " +
                "category; the case's own nature carries what survives of it.");

            Add(SafLoss.DroppedPointLoadFrame, LossCategory.Dropped, In, FemexEntity.Load, false,
                "A SAF point force or moment can be stated in the member's local frame, or as a " +
                "free vector. FEMEX's point load does not derive from its distributed load and " +
                "carries neither a coordinate system nor a direction vector, so the components were " +
                "taken as global.");

            Add(SafLoss.DroppedNonLinearCombination, LossCategory.Dropped, In,
                FemexEntity.LoadCombination, true,
                "SAF calls this combination non-linear, meaning its cases are not superposable. " +
                "FEMEX's combination types are linear add, envelope, absolute add and SRSS, none " +
                "of which says that, so it arrives as a linear add — which is arithmetic the source " +
                "said does not apply.");

            Add(SafLoss.DroppedNationalStandard, LossCategory.Dropped, In, FemexEntity.LoadCombination, false,
                "StructuralLoadCombination.National standard names the code clause the combination " +
                "came from. FEMEX carries a limit state and no annex reference.");

            Add(SafLoss.DroppedProjectColumns, LossCategory.Dropped, In, null, false,
                "SAF's Project sheet has eleven columns. FEMEX's FileMetadata takes the project name " +
                "and no more; number, location, type, kind, status, owner, dates and descriptions do " +
                "not cross.");

            Add(SafLoss.DroppedCompositeShape, LossCategory.Dropped, In, FemexEntity.Section, false,
                "A General cross-section is defined by a CompositeShapeDef polygon — up to 99 " +
                "contours with per-contour materials. The stated stiffness survives on a generic " +
                "section; the geometry does not.");

            Add(SafLoss.DroppedPasternakSubsoil, LossCategory.Dropped, In, FemexEntity.Support, true,
                "A SAF surface connection states Winkler C1x/C1y/C1z and Pasternak C2x/C2y. The " +
                "Winkler terms land on the area support's stiffnesses; the Pasternak shear layer has " +
                "no FEMEX representation and is not a detail of the same model — it is a different one.");

            Add(SafLoss.DroppedNonLinearRestraint, LossCategory.Dropped, In, FemexEntity.Support, true,
                "The restraint is stated Non linear, which is a resistance curve rather than a sense. " +
                "FEMEX's three senses are both ways, compression only and tension only, so the " +
                "restraint was left free rather than made to resist in a way the source did not say.");

            Add(SafLoss.DroppedPartialEdgeHinge, LossCategory.Dropped, In, FemexEntity.Hinge, true,
                "The edge hinge runs between a start and an end point along the edge. A FEMEX linear " +
                "hinge names the edge by its two nodes and runs the whole of it.");

            Add(SafLoss.DroppedCurvedSurface, LossCategory.Dropped, In, FemexEntity.Plate, true,
                "The surface member is curved. FEMEX plates are planar by rule, so the surface and " +
                "everything defined on it were not imported.");

            Add(SafLoss.DroppedObjectNames, LossCategory.Dropped, In, null, false,
                "SAF keys every sheet by Name. FEMEX's Bar, Node, Support and Hinge carry no name " +
                "property at all, so the names on four of the largest sheets in the workbook did " +
                "not cross. This is the other half of the renaming a round trip performs, and the " +
                "reason it performs it.");

            // ---- Approximated --------------------------------------------------

            Add(SafLoss.ChordedCurve, LossCategory.Approximated, In, FemexEntity.Bar, true,
                "The member is not a straight line. FEMEX geometry is chords, so the member arrives " +
                "as a chain of straight bars, each carrying the member's uid as its parent so the " +
                "chain can be recognised and re-emitted as one curve.");

            Add(SafLoss.FlattenedVaryingMember, LossCategory.Approximated, In, FemexEntity.Bar, true,
                "The member varies over more than one span, or over a span that is not a single " +
                "linear transition. FEMEX carries a start section and an end section, so the " +
                "variation was reduced to the first and last sections it names. A member haunched at " +
                "both ends arrives with the wrong moment distribution.");

            Add(SafLoss.GenericSection, LossCategory.Approximated, In, FemexEntity.Section, true,
                "The shape is outside FEMEX's eight parametric discriminators. It arrives as a " +
                "generic section carrying whatever stiffness the workbook stated, and no shape.");

            Add(SafLoss.SimplifiedSectionShape, LossCategory.Approximated, In, FemexEntity.Section, true,
                "The SAF shape states more dimensions than FEMEX's parametric shape has properties " +
                "for — a second flange thickness, a root radius, or two unequal flange widths. The " +
                "shape and its principal dimensions crossed; the rest did not, so the section's " +
                "area and second moments are close rather than equal.");

            Add(SafLoss.InferredShapeParameters, LossCategory.Approximated, In, FemexEntity.Section, false,
                "SAF states a parametric shape's dimensions as an ordered list whose meaning is " +
                "per-shape. The order for the rectangle, the circle, the pipe and the I-section is " +
                "settled by the published corpus: a symmetric I of 500 x 200 with 25 mm flanges " +
                "reads [500, 200, 200, 25, 25, 15], which fixes depth first, both flange widths, " +
                "both flange thicknesses, then the web. The T, L, angle, channel and tube orders " +
                "follow that same reading and are not independently confirmed — every section using " +
                "one is therefore approximate until the specification's parameter tables are read.");

            Add(SafLoss.RibAsBar, LossCategory.Approximated, In, FemexEntity.Bar, true,
                "A rib is a member acting compositely with the surface it lies on. FEMEX has no rib, " +
                "so it arrives as a plain bar: the stiffness is the bar's alone, and the effective " +
                "width, the shear connection and the composite action are gone.");

            Add(SafLoss.NominalThickness, LossCategory.Approximated, In, FemexEntity.SurfaceProperty, true,
                "The surface thickness varies. FEMEX's surface property has one implementation, " +
                "constant thickness, so the first stated thickness was taken as nominal.");

            Add(SafLoss.SelfWeightFactor, LossCategory.Approximated, Out, FemexEntity.LoadCase, true,
                "The case carries a self-weight factor that is neither 0 nor 1. SAF has no " +
                "self-weight load object and no factor on the case — self weight is generated by the " +
                "receiver and scaled through each combination's Multiplier — so the factor was " +
                "pushed into the multiplier of every combination naming this case. That is " +
                "equivalent only where the case appears in combinations at all.");

            Add(SafLoss.CollapsedCombinationFactor, LossCategory.Approximated, In, FemexEntity.LoadCombination, true,
                "SAF states a Load factor and a Multiplier per term; FEMEX states one Factor. The " +
                "two were multiplied. The product is right and the split is gone, so a round trip " +
                "returns the product as the factor and 1 as the multiplier.");

            Add(SafLoss.UnrepresentableCombinationCategory, LossCategory.Approximated, In,
                FemexEntity.LoadCombination, true,
                "The combination's category is According National Standard, which defers the whole " +
                "definition to a named code clause. FEMEX's limit state has no member for that, and " +
                "no property for the standard's name, so the combination arrives with its category " +
                "unspecified rather than misdescribed.");

            Add(SafLoss.ExpandedRepeatSeries, LossCategory.Approximated, In, FemexEntity.Load, true,
                "One SAF row stands for a repeated series of loads at a fixed spacing. FEMEX has no " +
                "repeat concept, so the series was expanded into separate loads. The values are " +
                "exact; the grouping is gone, and each expanded load carries the original's uid as " +
                "its parent so the series can be recognised again.");

            Add(SafLoss.MergedForceAndMoment, LossCategory.Approximated, In, FemexEntity.Load, true,
                "SAF splits force from moment into two sheets with two Ids; FEMEX carries Fx..Mz on " +
                "one object. A force and a moment at the same station in the same case were merged, " +
                "so one of the two SAF uids could not survive.");

            Add(SafLoss.CollapsedThermalVariation, LossCategory.Approximated, In, FemexEntity.Load, true,
                "SAF states a linear thermal variation as up to four fibre temperatures. FEMEX " +
                "states a mean change and two signed gradients, so the four were reduced to three " +
                "numbers.");

            Add(SafLoss.ChordedSurfaceEdge, LossCategory.Approximated, In, FemexEntity.Plate, true,
                "One of the surface's edges is an arc. FEMEX plate contours are straight-edged " +
                "polygons, so the arc arrives as the chords through the points SAF states on it. " +
                "The enclosed area is smaller than the workbook's own stated area by the segments " +
                "the chords cut off.");

            Add(SafLoss.ApproximatedSurfaceBehaviour, LossCategory.Approximated, In, FemexEntity.Plate, true,
                "The surface is orthotropic. FEMEX has four plate behaviours and no orthotropic " +
                "one — directionality was ruled to belong on the surface property, and that half " +
                "was never built — so it arrives as a shell, which is stiffer across the weak " +
                "direction than the source said. SAF carries no orthotropy parameters either, so " +
                "there is nothing held back for later.");

            Add(SafLoss.ApproximatedLoadGroupType, LossCategory.Approximated, In, FemexEntity.LoadGroup, true,
                "SAF has seven load-group types and FEMEX has five. Moving and fire are variable " +
                "actions in every code that names them, so the group arrives variable — the right " +
                "family, and not the same statement.");

            Add(SafLoss.ResolvedMemberLcs, LossCategory.Approximated, In, FemexEntity.Bar, false,
                "The member's local frame is stated by a vector or by a point. FEMEX states a roll " +
                "angle about the member axis from its own default frame, so the direction was " +
                "resolved to one angle — exact only where the stated vector lies in the plane " +
                "FEMEX's default rule produces.");

            Add(SafLoss.ChordedPosition, LossCategory.Approximated, In, FemexEntity.Load, true,
                "The position along the member is stated absolutely, and the member is curved. " +
                "FEMEX stores positions relative to a straight bar, and the chord length is not the " +
                "arc length, so the converted position is close rather than equal.");

            // ---- Unmapped, per concept ----------------------------------------

            Add(SafLoss.UnmappedProxyElement, LossCategory.Unmapped, In, null, false,
                "StructuralProxyElement and its vertex and face sheets carry geometry that is not a " +
                "structural member — architectural context, mostly. FEMEX has no concept for it.");

            Add(SafLoss.UnmappedRigidLink, LossCategory.Unmapped, In, null, false,
                "RelConnectsRigidLink couples two nodes through six constrained degrees of freedom. " +
                "FEMEX has no constraint object, so the coupling does not cross and the two nodes " +
                "arrive independent.");

            Add(SafLoss.UnmappedRigidCross, LossCategory.Unmapped, In, null, false,
                "RelConnectsRigidCross couples crossing members. FEMEX has no constraint object.");

            Add(SafLoss.UnmappedRigidMember, LossCategory.Unmapped, In, null, false,
                "RelConnectsRigidMember couples a node to members, edges or surfaces. FEMEX has no " +
                "constraint object.");

            Add(SafLoss.UnmappedInternalEdge, LossCategory.Unmapped, In, null, false,
                "StructuralCurveEdge is an internal edge inside a surface member. FEMEX has no " +
                "internal edge, so the edge is lost — and so is everything that points at it: edge " +
                "supports, edge hinges and line loads on internal edges all name a target that did " +
                "not arrive.");

            Add(SafLoss.UnmappedFreePointAction, LossCategory.Unmapped, In, null, false,
                "StructuralPointActionFree places a force at a raw coordinate rather than on an " +
                "object. FEMEX addresses a point load by node, and minting a node to hold one would " +
                "change the topology of the model being reported on.");

            Add(SafLoss.UnmappedFreeCurveAction, LossCategory.Unmapped, In, null, false,
                "StructuralCurveActionFree places a line load along a free polyline. FEMEX addresses " +
                "a linear load by nodes or by a bar, and neither exists here.");

            Add(SafLoss.UnmappedFreeSurfaceAction, LossCategory.Unmapped, In, null, false,
                "StructuralSurfaceActionFree bounds a pressure by a free polygon of raw coordinates. " +
                "FEMEX's free area load is bounded by node numbers, so importing one would consume " +
                "model nodes that the source did not have.");

            Add(SafLoss.UnmappedSupportDeformation, LossCategory.Unmapped, In, null, false,
                "StructuralPointSupportDeformation is an imposed settlement or rotation at a " +
                "support, in a named load case. FEMEX has no load type for a prescribed displacement.");

            Add(SafLoss.UnmappedResults, LossCategory.Unmapped, In, null, false,
                "ResultInternalForce1D and ResultInternalForce2DEdge carry analysis results. FEMEX " +
                "is deliberately a model format and holds none, so the results sheets were read and " +
                "not carried — said aloud rather than passed over in silence.");

            Add(SafLoss.UnmappedIgnoredObjects, LossCategory.Unmapped, In, null, false,
                "Model.Ignored objects and Ignored groups tell a receiving program which sheets to " +
                "leave alone when merging this file into an existing model. FEMEX has no update " +
                "semantics and therefore nowhere to put the instruction.");

            Add(SafLoss.UnmappedCompositeAction, LossCategory.Unmapped, In, null, false,
                "A rib's effective width, its width basis and its shear connection type describe " +
                "composite action between a member and a surface. FEMEX models the two as unrelated " +
                "objects.");

            return table;
        }
    }
}
