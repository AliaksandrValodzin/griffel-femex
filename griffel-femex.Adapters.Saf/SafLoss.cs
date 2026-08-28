namespace griffel_femex.Adapters.Saf
{
    /// <summary>
    /// Every loss this adapter declares, one member per entry of
    /// <c>FEMEX_SAF_Fit.md</c> §8.2, plus the entries the corpus added afterwards.
    /// </summary>
    /// <remarks>
    /// <para>
    /// §8.2 is not a summary of the mapping — it is the mapping's acceptance
    /// criterion. <c>SAF_Adapter.md</c> B3 says so: <i>"a Phase B test asserts the
    /// importer emits at least the enumerated per-concept messages"</i>. Naming each
    /// one makes that assertion possible without matching on prose, and makes the
    /// catalogue a thing a reader can enumerate rather than a thing they have to
    /// find by reading two implementations.
    /// </para>
    /// <para>
    /// The distinction §4.4 of <c>FEMEX_Adapters.md</c> draws — per concept versus
    /// per object — is carried by <see cref="SafMessages"/>, not here. A member of
    /// this enum is a <i>kind</i> of loss; whether it is reported once or forty-two
    /// times is a property of what it is about, and is stated beside its text.
    /// </para>
    /// </remarks>
    public enum SafLoss
    {
        // ---- Invented ----------------------------------------------------------

        /// <summary>Import: a level synthesised because SAF has free coordinates and FEMEX nodes need a landlord.</summary>
        SynthesisedLevel,

        /// <summary>Import: every number in the model restated in metre-newton units.</summary>
        RestatedInSiUnits,

        /// <summary>Import: a load case attached to a load group the exporter invented.</summary>
        AttachedToInventedLoadGroup,

        /// <summary>Export: <c>LoadCombination.IncludeInDesignEnvelope</c>, which SAF has no column for.</summary>
        DroppedDesignEnvelopeFlag,

        /// <summary>Import: FEMEX's five typed unit enums, stamped from SAF's one coarse flag.</summary>
        StampedUnitSystem,

        /// <summary>Import: uids minted by <c>AssignMissingUids</c> because the SAF rows had no Id.</summary>
        MintedUids,

        /// <summary>Import: a placeholder section for a member naming no cross-section.</summary>
        PlaceholderSection,

        /// <summary>Import: gravity, which SAF never states and FEMEX always does.</summary>
        InventedGravity,

        /// <summary>Export: <c>Model.System of units</c>, which FEMEX states more finely than SAF can hold.</summary>
        InventedSystemOfUnits,

        /// <summary>Export: <c>Model.National code</c>, deliberately outside the FEMEX format.</summary>
        InventedNationalCode,

        /// <summary>Export: <c>Model.LCS of cross-section</c>, derivable from FEMEX's fixed convention and stated nowhere.</summary>
        InventedCrossSectionLcs,

        /// <summary>Export: <c>Model.SAF Version</c> — a statement about SAF, which FEMEX never makes.</summary>
        InventedSafVersion,

        /// <summary>Export: names synthesised for the four sheets whose FEMEX counterparts carry none.</summary>
        SynthesisedNames,

        /// <summary>Export: a <c>StructuralLoadGroup</c> per load nature, because SAF's reference is mandatory.</summary>
        InventedLoadGroup,

        /// <summary>Export: zero analysis eccentricity and a centre system line, both mandatory and both unstated in FEMEX.</summary>
        InventedMemberEccentricity,

        /// <summary>Export: <c>Form code = 0</c> for a shape FEMEX modelled as generic.</summary>
        InventedFormCode,

        /// <summary>Export: a material for a section no member uses, which SAF requires anyway.</summary>
        InventedSectionMaterial,

        /// <summary>Export: the Pasternak subsoil terms SAF requires and FEMEX has no property for.</summary>
        InventedPasternakSubsoil,

        /// <summary>Export: the local-frame reference vector SAF requires on every member and surface.</summary>
        InventedLocalFrame,

        // ---- Dropped, export leg ----------------------------------------------

        /// <summary>Export: <c>Grid</c> and <c>Gridline</c>. SAF has no concept.</summary>
        DroppedGrids,

        /// <summary>Export: <c>FemexMesh</c>. SAF carries results, not meshes.</summary>
        DroppedMesh,

        /// <summary>Export: region <c>Priority</c> — the one place FEMEX leads SAF outright.</summary>
        DroppedRegionPriority,

        /// <summary>Export: <c>PlateBehaviour.Plate</c>, bending only, which SAF has no value for.</summary>
        DroppedPlateBehaviour,

        /// <summary>Export: <c>AbsoluteAdd</c> and <c>Srss</c> combinations, which SAF has no type for.</summary>
        DroppedCombinationType,

        /// <summary>Export: <c>Level.IsGround</c> and <c>Level.RelativeElevation</c>.</summary>
        DroppedLevelProperties,

        /// <summary>Export: FEMEX's shared surface-property object, which SAF has no sheet for.</summary>
        DissolvedSurfaceProperty,

        /// <summary>Export: the four section properties SAF has no column for.</summary>
        DroppedSectionProperties,

        /// <summary>Export: two members give one section different materials, and SAF puts the material on the section.</summary>
        SharedSectionMaterial,

        /// <summary>Export: a linear load on an edge that is no plate's contour edge.</summary>
        UnplaceableLinearLoad,

        /// <summary>Export: a one-directional flexible restraint on a sheet that has no such value.</summary>
        NarrowedLineRestraint,

        /// <summary>Export: an area load bounded by a free polygon rather than by a surface.</summary>
        UnplaceableAreaLoad,

        /// <summary>Export: a surface load stated by a direction vector, which SAF states only by axis.</summary>
        FlattenedLoadDirection,

        /// <summary>Export: a linear support on an edge that is no plate's contour edge.</summary>
        UnplaceableEdgeSupport,

        /// <summary>Export: an in-plane thermal gradient on a surface, which SAF's surface thermal action has no column for.</summary>
        DroppedSurfaceThermalGradient,

        // ---- Dropped, import leg, one message per concept ----------------------

        /// <summary>Import: <c>StructuralCurveMember.Type</c> — seventeen values of annotation.</summary>
        DroppedMemberType,

        /// <summary>Import: <c>Layer</c> and <c>Color</c>, on every object that carries them.</summary>
        DroppedLayerAndColour,

        /// <summary>Import: <c>StructuralCurveAction.Eccentricity ey/ez</c>.</summary>
        DroppedLoadEccentricity,

        /// <summary>Import: <c>StructuralCrossSection.Description ID</c>, a shape classification FEMEX has no field for.</summary>
        DroppedSectionDescription,

        /// <summary>Import: <c>StructuralMaterial.Subtype</c>.</summary>
        DroppedMaterialSubtype,

        /// <summary>Import: <c>StructuralLoadCase.Duration</c>.</summary>
        DroppedLoadCaseDuration,

        /// <summary>Import: a load group's <c>Load type</c> — Domestic, Roofs, Snow, Wind.</summary>
        DroppedLoadGroupCategory,

        /// <summary>Import: <c>Coordinate system = Local</c> or <c>Direction = Vector</c> on a point load.</summary>
        DroppedPointLoadFrame,

        /// <summary>Import: a combination SAF calls non-linear, which FEMEX has no type for.</summary>
        DroppedNonLinearCombination,

        /// <summary>Import: <c>StructuralLoadCombination.National standard</c>.</summary>
        DroppedNationalStandard,

        /// <summary>Import: ten of the eleven <c>Project</c> columns.</summary>
        DroppedProjectColumns,

        /// <summary>Import: the <c>CompositeShapeDef</c> polygon behind a <c>General</c> cross-section.</summary>
        DroppedCompositeShape,

        /// <summary>Import: Pasternak <c>C2x/C2y</c> on a subsoil connection. Winkler C1 survives; C2 does not.</summary>
        DroppedPasternakSubsoil,

        /// <summary>Import: a restraint stated <c>Non linear</c>, which FEMEX's three senses cannot hold.</summary>
        DroppedNonLinearRestraint,

        /// <summary>Import: a partial-length edge hinge, whose <c>Start point</c>/<c>End point</c> FEMEX has nowhere to put.</summary>
        DroppedPartialEdgeHinge,

        /// <summary>Import: a curved surface member, which FEMEX's planarity rule rejects.</summary>
        DroppedCurvedSurface,

        /// <summary>Import: the SAF names of the four sheets whose FEMEX counterparts carry no name.</summary>
        DroppedObjectNames,

        // ---- Approximated ------------------------------------------------------

        /// <summary>Import: a non-line member segment, chorded into a chain of bars.</summary>
        ChordedCurve,

        /// <summary>Import: a varying member with more than a single linear transition.</summary>
        FlattenedVaryingMember,

        /// <summary>Import: a parametric or compound shape outside FEMEX's eight, carried as generic + properties.</summary>
        GenericSection,

        /// <summary>Import: a shape whose SAF dimension list is richer than FEMEX's parametric shape.</summary>
        SimplifiedSectionShape,

        /// <summary>Import: a shape whose SAF parameter <i>order</i> was inferred rather than measured.</summary>
        InferredShapeParameters,

        /// <summary>Import: a rib, carried as a plain bar with its composite action lost.</summary>
        RibAsBar,

        /// <summary>Import: a thickness type other than constant, carried as a nominal thickness.</summary>
        NominalThickness,

        /// <summary>Export: a self-weight factor that is neither 0 nor 1, pushed into combination multipliers.</summary>
        SelfWeightFactor,

        /// <summary>Import: <c>Load factor</c> × <c>Multiplier</c> collapsed into one <c>Factor</c>.</summary>
        CollapsedCombinationFactor,

        /// <summary>Import: <c>Category = According National Standard</c>, which <c>LimitState</c> has no member for.</summary>
        UnrepresentableCombinationCategory,

        /// <summary>Import: a <c>Repeat (n)</c> series expanded into n separate loads.</summary>
        ExpandedRepeatSeries,

        /// <summary>Import: a point force and a point moment at the same station merged into one <c>PointLoad</c>.</summary>
        MergedForceAndMoment,

        /// <summary>Import: linear thermal variation, four fibre temperatures collapsed into two gradients.</summary>
        CollapsedThermalVariation,

        /// <summary>Import: an LCS stated by vector or by point, resolved to one roll angle.</summary>
        ResolvedMemberLcs,

        /// <summary>Import: a surface contour with a curved edge, chorded into straight ones.</summary>
        ChordedSurfaceEdge,

        /// <summary>Import: a surface behaviour SAF states and FEMEX has no value for.</summary>
        ApproximatedSurfaceBehaviour,

        /// <summary>Import: a load group type SAF has and FEMEX does not — moving, or fire.</summary>
        ApproximatedLoadGroupType,

        /// <summary>Import: an absolute position along a chorded arc, where the chord length is not the arc length.</summary>
        ChordedPosition,

        // ---- Unmapped, per concept --------------------------------------------

        /// <summary>Import: <c>StructuralProxyElement</c> and its vertex and face sheets.</summary>
        UnmappedProxyElement,

        /// <summary>Import: <c>RelConnectsRigidLink</c>.</summary>
        UnmappedRigidLink,

        /// <summary>Import: <c>RelConnectsRigidCross</c>.</summary>
        UnmappedRigidCross,

        /// <summary>Import: <c>RelConnectsRigidMember</c>.</summary>
        UnmappedRigidMember,

        /// <summary>Import: <c>StructuralCurveEdge</c>, and everything that points at one.</summary>
        UnmappedInternalEdge,

        /// <summary>Import: <c>StructuralPointActionFree</c>.</summary>
        UnmappedFreePointAction,

        /// <summary>Import: <c>StructuralCurveActionFree</c>.</summary>
        UnmappedFreeCurveAction,

        /// <summary>Import: <c>StructuralSurfaceActionFree</c>.</summary>
        UnmappedFreeSurfaceAction,

        /// <summary>Import: <c>StructuralPointSupportDeformation</c> — imposed support displacement.</summary>
        UnmappedSupportDeformation,

        /// <summary>Import: both <c>ResultInternalForce</c> sheets. Deliberate on both sides, and said aloud.</summary>
        UnmappedResults,

        /// <summary>Import: <c>Model.Ignored objects</c> and <c>Ignored groups</c> — update semantics FEMEX has no equivalent for.</summary>
        UnmappedIgnoredObjects,

        /// <summary>Import: <c>StructuralCurveMemberRib</c>'s effective width, shear connection and composite action.</summary>
        UnmappedCompositeAction,
    }
}
