using System.Globalization;
using System.Reflection;
using System.Text.Json;
using griffel_femex.Geometry.Grids;
using griffel_femex.Interop;
using griffel_femex.Loads.Combinations;

namespace griffel_femex.Comparison
{
    /// <summary>
    /// Walks two matched objects member by member and records where they disagree.
    ///
    /// <b>The design is exceptions-first, and that is the point.</b> The default for
    /// any member is to compare its <i>serialized form</i>, which is total: a member
    /// added by a future schema is compared the day it is added, exactly, with no
    /// table to remember to update. What the tables below hold is only the members
    /// for which serialized equality is the <i>wrong</i> answer — the keys that a
    /// native program renumbers, the integers that name other objects, and the
    /// coordinates that come back through somebody else's arithmetic. A diff built
    /// the other way round, comparing only what a table lists, silently stops
    /// covering the format the first time the format grows.
    /// </summary>
    internal sealed class MemberComparer
    {
        // ----- The exceptions -----

        /// <summary>
        /// Keys, and the two lists that are compared elsewhere. A key is skipped
        /// because §7.2 matches on uid and never on id, so the integer is by
        /// definition allowed to differ; the uid itself is skipped because it is what
        /// the two objects were matched by, so it cannot differ.
        /// </summary>
        private static readonly HashSet<string> Skipped = new HashSet<string>(StringComparer.Ordinal)
        {
            "IIdentified.Uid", "Grid.Uid", "Level.Uid", "Node.Uid", "Section.Uid",
            "SurfaceProperty.Uid", "Element.Uid", "PlateRegion.Uid", "Material.Uid",
            "LoadCase.Uid", "Load.Uid", "LoadCombination.Uid", "Support.Uid", "Hinge.Uid",

            "Grid.Id", "Level.LevelNumber", "Node.NodeNumber", "Section.Id",
            "SurfaceProperty.Id", "Element.Id", "PlateRegion.Id", "Material.Id",
            "LoadCase.Number", "Load.Id", "LoadCombination.Number", "Support.Id", "Hinge.Id",

            // Regions carry uids of their own, so they are matched and compared as
            // entities in their own right rather than inside their plate.
            "Plate.Regions",

            // A node's place is its absolute point, compared as one thing by
            // ModelDiff; these three are the coordinates it is made of, and its
            // level is compared as the reference it also is.
            "Node.X", "Node.Y", "Node.VerticalOffset",
        };

        /// <summary>
        /// Coordinates: the members §7.2 grants
        /// <see cref="FemexModel.GetCoincidenceTolerance"/> to, because they come
        /// back through the native program's own precision. Everything not named
        /// here is a number about which the format makes an exact claim.
        /// </summary>
        private static readonly HashSet<string> Geometric = new HashSet<string>(StringComparer.Ordinal)
        {
            "Level.AbsoluteElevation", "Level.RelativeElevation",
            "Grid.OriginX", "Grid.OriginY",
            "GridExtent.MinX", "GridExtent.MaxX", "GridExtent.MinY", "GridExtent.MaxY",
            "OrthogonalGridline.Offset",
            "FreeGridline.X1", "FreeGridline.Y1", "FreeGridline.X2", "FreeGridline.Y2",
            "Plate.SurfaceOffset", "PlateRegion.SurfaceOffset",
        };

        /// <summary>
        /// Every integer in the format that names another object. Missing an entry
        /// here is visible rather than silent: the reference is then compared as a
        /// bare number and a renumbered model reports a difference that is not one.
        /// </summary>
        private static readonly Dictionary<string, Reference> References =
            new Dictionary<string, Reference>(StringComparer.Ordinal)
            {
                ["FemexModel.DefaultGridIds"] = new Reference(RefTarget.Grid, ordered: false),
                ["Level.GridIds"] = new Reference(RefTarget.Grid, ordered: false),
                ["Node.LevelNumber"] = new Reference(RefTarget.Level),

                ["Bar.StartNodeId"] = new Reference(RefTarget.Node),
                ["Bar.EndNodeId"] = new Reference(RefTarget.Node),
                ["Bar.SectionId"] = new Reference(RefTarget.Section),
                ["Bar.MaterialId"] = new Reference(RefTarget.Material),

                ["Plate.NodeIds"] = new Reference(RefTarget.Node),
                ["Plate.SurfacePropertyId"] = new Reference(RefTarget.SurfaceProperty),
                ["Plate.MaterialId"] = new Reference(RefTarget.Material),
                ["PlateRegion.NodeIds"] = new Reference(RefTarget.Node),
                ["PlateRegion.SurfacePropertyId"] = new Reference(RefTarget.SurfaceProperty),
                ["PlateRegion.MaterialId"] = new Reference(RefTarget.Material),

                ["Load.LoadCaseNumber"] = new Reference(RefTarget.LoadCase),
                ["PointLoad.NodeNumber"] = new Reference(RefTarget.Node),
                ["LinearLoad.StartNode"] = new Reference(RefTarget.Node),
                ["LinearLoad.EndNode"] = new Reference(RefTarget.Node),
                ["LinearLoad.BarId"] = new Reference(RefTarget.Bar),
                ["AreaLoad.PlateId"] = new Reference(RefTarget.Plate),
                ["AreaLoad.RegionId"] = new Reference(RefTarget.Region, scope: "PlateId"),
                ["AreaLoad.NodeSequence"] = new Reference(RefTarget.Node),
                ["TemperatureLoad.ElementIds"] = new Reference(RefTarget.Element, ordered: false),
                ["LoadCombinationTerm.LoadCaseNumber"] = new Reference(RefTarget.LoadCase),

                ["Support.NodeIds"] = new Reference(RefTarget.Node, ordered: false),
                ["Support.PlateId"] = new Reference(RefTarget.Plate),
                ["Support.RegionId"] = new Reference(RefTarget.Region, scope: "PlateId"),
                ["Hinge.NodeIds"] = new Reference(RefTarget.Node, ordered: false),
                ["Hinge.ElementId"] = new Reference(RefTarget.Element),
                ["Hinge.RegionId"] = new Reference(RefTarget.Region, scope: "ElementId"),
                ["Hinge.EdgeStartNodeId"] = new Reference(RefTarget.Node),
                ["Hinge.EdgeEndNodeId"] = new Reference(RefTarget.Node),
            };

        /// <summary>
        /// The two lists of keyed sub-objects that carry no uid of their own. Both
        /// have a natural key the format already guarantees, so both compare as sets
        /// under it — which is §7.2's rule applied one level down.
        /// </summary>
        private static readonly Dictionary<string, Func<object, MemberComparer, bool, string>> KeyedLists =
            new Dictionary<string, Func<object, MemberComparer, bool, string>>(StringComparer.Ordinal)
            {
                // A gridline's identity is already its non-nullable, validated,
                // unique label — which is why EnumerateIdentified leaves it out.
                ["Grid.Lines"] = (item, _, _) => ((Gridline)item).Label,

                // A term has no key at all, and the case it factors is the only
                // thing that could be one. Two terms naming one case are a defect
                // the validator reports; here they collide and the second is
                // reported as a difference, which is the honest outcome.
                ["LoadCombination.Terms"] = (item, comparer, isLeft) =>
                    comparer.Index(isLeft).Token(RefTarget.LoadCase,
                                                 ((LoadCombinationTerm)item).LoadCaseNumber, null),
            };

        private readonly ModelDiffOptions _options;
        private readonly EntityIndex _leftIndex;
        private readonly EntityIndex _rightIndex;
        private readonly double _geometricTolerance;
        private readonly List<ModelDifference> _differences;

        internal MemberComparer(ModelDiffOptions options, EntityIndex leftIndex, EntityIndex rightIndex,
                                double geometricTolerance, List<ModelDifference> differences)
        {
            _options = options;
            _leftIndex = leftIndex;
            _rightIndex = rightIndex;
            _geometricTolerance = geometricTolerance;
            _differences = differences;
        }

        private EntityIndex Index(bool isLeft) => isLeft ? _leftIndex : _rightIndex;

        /// <summary>
        /// Compares every readable public member of two objects of the same runtime
        /// type, appending a <see cref="ModelDifference"/> for each disagreement.
        /// </summary>
        internal void CompareMembers(ObjectRef? subject, object left, object right, string? path)
        {
            PropertyInfo[] properties = left.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            Array.Sort(properties, (a, b) => string.CompareOrdinal(a.Name, b.Name));

            foreach (PropertyInfo property in properties)
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    continue;

                MethodInfo? getter = property.GetGetMethod();
                if (getter is null || getter.IsStatic)
                    continue;

                string key = $"{property.DeclaringType?.Name}.{property.Name}";
                if (Skipped.Contains(key))
                    continue;

                string member = path is null ? property.Name : $"{path}.{property.Name}";
                object? leftValue = property.GetValue(left);
                object? rightValue = property.GetValue(right);

                if (property.Name == nameof(IExtensible.UnknownMembers) &&
                    property.PropertyType == typeof(Dictionary<string, JsonElement>))
                {
                    if (_options.CompareUnknownMembers)
                        CompareUnknown(subject, member,
                                       leftValue as Dictionary<string, JsonElement>,
                                       rightValue as Dictionary<string, JsonElement>);
                    continue;
                }

                if (References.TryGetValue(key, out Reference reference))
                {
                    CompareReference(subject, member, reference, left, right, leftValue, rightValue);
                    continue;
                }

                if (KeyedLists.TryGetValue(key, out var keyOf))
                {
                    CompareKeyedList(subject, member, keyOf, leftValue, rightValue);
                    continue;
                }

                if (IsFloating(property.PropertyType))
                {
                    CompareNumbers(subject, member, leftValue, rightValue, Geometric.Contains(key));
                    continue;
                }

                if (IsNested(property.PropertyType))
                {
                    CompareNested(subject, member, leftValue, rightValue);
                    continue;
                }

                CompareSerialized(subject, member, property.PropertyType, leftValue, rightValue);
            }
        }

        // ----- The comparisons -----

        private void CompareReference(ObjectRef? subject, string member, Reference reference,
                                      object leftOwner, object rightOwner,
                                      object? leftValue, object? rightValue)
        {
            List<string> left = Tokens(reference, leftOwner, leftValue, isLeft: true);
            List<string> right = Tokens(reference, rightOwner, rightValue, isLeft: false);

            if (!reference.Ordered)
            {
                left.Sort(StringComparer.Ordinal);
                right.Sort(StringComparer.Ordinal);
            }

            if (SameSequence(left, right))
                return;

            string leftText = string.Join(", ", left);
            string rightText = string.Join(", ", right);
            Report(DifferenceKind.MemberDiffers, subject, member, leftText, rightText,
                   $"{Describe(subject)} {member} points at {Render(leftText)} on the left and " +
                   $"{Render(rightText)} on the right.");
        }

        private List<string> Tokens(Reference reference, object owner, object? value, bool isLeft)
        {
            EntityIndex index = Index(isLeft);
            int? scope = reference.Scope is null ? null : ReadInt(owner, reference.Scope);
            var tokens = new List<string>();

            if (value is null)
            {
                tokens.Add("none");
                return tokens;
            }

            if (value is int single)
            {
                tokens.Add(index.Token(reference.Target, single, scope));
                return tokens;
            }

            if (value is IEnumerable<int> many)
            {
                foreach (int id in many)
                    tokens.Add(index.Token(reference.Target, id, scope));

                return tokens;
            }

            throw new NotSupportedException(
                $"A reference member of type {value.GetType()} is not an id or a list of ids. " +
                "Either the format grew a reference shape the diff has not been taught, or the " +
                "entry naming it in MemberComparer.References is wrong.");
        }

        private static int? ReadInt(object owner, string propertyName)
        {
            PropertyInfo? property = owner.GetType().GetProperty(propertyName,
                                                                 BindingFlags.Public | BindingFlags.Instance);
            object? value = property?.GetValue(owner);
            return value as int?;
        }

        private void CompareKeyedList(ObjectRef? subject, string member,
                                      Func<object, MemberComparer, bool, string> keyOf,
                                      object? leftValue, object? rightValue)
        {
            Dictionary<string, object> left = KeyBy(leftValue, keyOf, isLeft: true);
            Dictionary<string, object> right = KeyBy(rightValue, keyOf, isLeft: false);

            var keys = new List<string>(left.Keys);
            foreach (string key in right.Keys)
            {
                if (!left.ContainsKey(key))
                    keys.Add(key);
            }

            keys.Sort(StringComparer.Ordinal);

            foreach (string key in keys)
            {
                bool inLeft = left.TryGetValue(key, out object? leftItem);
                bool inRight = right.TryGetValue(key, out object? rightItem);
                string path = $"{member}[{key}]";

                if (inLeft && !inRight)
                {
                    Report(DifferenceKind.MemberDiffers, subject, path, key, null,
                           $"{Describe(subject)} has {path} on the left and nothing matching it on the right.");
                    continue;
                }

                if (!inLeft && inRight)
                {
                    Report(DifferenceKind.MemberDiffers, subject, path, null, key,
                           $"{Describe(subject)} has {path} on the right and nothing matching it on the left.");
                    continue;
                }

                if (leftItem!.GetType() != rightItem!.GetType())
                {
                    Report(DifferenceKind.TypeDiffers, subject, path,
                           leftItem.GetType().Name, rightItem.GetType().Name,
                           $"{Describe(subject)} {path} is a {leftItem.GetType().Name} on the left and a " +
                           $"{rightItem.GetType().Name} on the right.");
                    continue;
                }

                CompareMembers(subject, leftItem, rightItem, path);
            }
        }

        private Dictionary<string, object> KeyBy(object? value,
                                                 Func<object, MemberComparer, bool, string> keyOf,
                                                 bool isLeft)
        {
            var keyed = new Dictionary<string, object>(StringComparer.Ordinal);
            if (value is not System.Collections.IEnumerable items)
                return keyed;

            int duplicates = 0;
            foreach (object? item in items)
            {
                if (item is null)
                    continue;

                string key = keyOf(item, this, isLeft);
                if (keyed.ContainsKey(key))
                    key = $"{key}#{++duplicates}";

                keyed[key] = item;
            }

            return keyed;
        }

        private void CompareNested(ObjectRef? subject, string member, object? leftValue, object? rightValue)
        {
            if (leftValue is null && rightValue is null)
                return;

            if (leftValue is null || rightValue is null)
            {
                Report(DifferenceKind.MemberDiffers, subject, member,
                       leftValue is null ? null : "present", rightValue is null ? null : "present",
                       $"{Describe(subject)} {member} is stated on the " +
                       $"{(leftValue is null ? "right" : "left")} only.");
                return;
            }

            if (leftValue.GetType() != rightValue.GetType())
            {
                Report(DifferenceKind.TypeDiffers, subject, member,
                       leftValue.GetType().Name, rightValue.GetType().Name,
                       $"{Describe(subject)} {member} is a {leftValue.GetType().Name} on the left and a " +
                       $"{rightValue.GetType().Name} on the right.");
                return;
            }

            CompareMembers(subject, leftValue, rightValue, member);
        }

        private void CompareNumbers(ObjectRef? subject, string member, object? leftValue,
                                    object? rightValue, bool geometric)
        {
            double? left = ToDouble(leftValue);
            double? right = ToDouble(rightValue);

            if (!left.HasValue && !right.HasValue)
                return;

            if (left.HasValue && right.HasValue && NumbersEqual(left.Value, right.Value, geometric))
                return;

            string leftText = Text(left);
            string rightText = Text(right);
            Report(DifferenceKind.MemberDiffers, subject, member, leftText, rightText,
                   $"{Describe(subject)} {member} is {leftText} on the left and {rightText} on the right.");
        }

        private void CompareSerialized(ObjectRef? subject, string member, Type declaredType,
                                       object? leftValue, object? rightValue)
        {
            string left = Serialize(declaredType, leftValue);
            string right = Serialize(declaredType, rightValue);

            if (string.Equals(left, right, StringComparison.Ordinal))
                return;

            Report(DifferenceKind.MemberDiffers, subject, member, left, right,
                   $"{Describe(subject)} {member} is {Render(left)} on the left and {Render(right)} " +
                   "on the right.");
        }

        private void CompareUnknown(ObjectRef? subject, string member,
                                    Dictionary<string, JsonElement>? left,
                                    Dictionary<string, JsonElement>? right)
        {
            var names = new List<string>();
            if (left is not null)
                names.AddRange(left.Keys);
            if (right is not null)
            {
                foreach (string name in right.Keys)
                {
                    if (left is null || !left.ContainsKey(name))
                        names.Add(name);
                }
            }

            names.Sort(StringComparer.Ordinal);

            foreach (string name in names)
            {
                string? leftText = left is not null && left.TryGetValue(name, out JsonElement l)
                    ? l.GetRawText() : null;
                string? rightText = right is not null && right.TryGetValue(name, out JsonElement r)
                    ? r.GetRawText() : null;

                if (string.Equals(leftText, rightText, StringComparison.Ordinal))
                    continue;

                Report(DifferenceKind.MemberDiffers, subject, $"{member}[{name}]", leftText, rightText,
                       $"{Describe(subject)} carries the unrecognised member \"{name}\" as " +
                       $"{Render(leftText)} on the left and {Render(rightText)} on the right.");
            }
        }

        // ----- Shared -----

        internal bool NumbersEqual(double left, double right, bool geometric)
        {
            if (left.Equals(right))
                return true;

            if (geometric)
                return Math.Abs(left - right) <= _geometricTolerance;

            if (_options.RelativeTolerance <= 0.0)
                return false;

            double scale = Math.Max(Math.Abs(left), Math.Abs(right));
            return Math.Abs(left - right) <= _options.RelativeTolerance * scale;
        }

        internal void Report(DifferenceKind kind, ObjectRef? subject, string? member,
                             string? left, string? right, string text)
        {
            _differences.Add(new ModelDifference(kind, subject, member, left, right, text));
        }

        internal static string Describe(ObjectRef? subject)
        {
            return subject.HasValue ? subject.Value.ToString() : "The model";
        }

        private static bool SameSequence(List<string> left, List<string> right)
        {
            if (left.Count != right.Count)
                return false;

            for (int i = 0; i < left.Count; i++)
            {
                if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private static bool IsFloating(Type type)
        {
            return type == typeof(double) || type == typeof(double?)
                || type == typeof(float) || type == typeof(float?);
        }

        /// <summary>
        /// A class of this library's own, which is worth walking into so a
        /// difference names <c>Ux.Stiffness</c> rather than a blob of JSON. Lists and
        /// dictionaries are excluded: those are either keyed lists, handled above, or
        /// something the serialized comparison handles exactly.
        /// </summary>
        private static bool IsNested(Type type)
        {
            if (type.IsValueType || type == typeof(string))
                return false;

            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
                return false;

            return type.Assembly == typeof(FemexModel).Assembly;
        }

        private static double? ToDouble(object? value)
        {
            return value switch
            {
                double d => d,
                float f => f,
                _ => null,
            };
        }

        private static string Text(double? value)
        {
            return value.HasValue
                ? value.Value.ToString("R", CultureInfo.InvariantCulture)
                : "unstated";
        }

        private static string Serialize(Type declaredType, object? value)
        {
            return value is null ? "null" : JsonSerializer.Serialize(value, declaredType, FemexModel.JsonOptions);
        }

        private static string Render(string? value)
        {
            return value is null ? "unstated" : value;
        }

        /// <summary>One integer member that names another object.</summary>
        private readonly struct Reference
        {
            internal Reference(RefTarget target, bool ordered = true, string? scope = null)
            {
                Target = target;
                Ordered = ordered;
                Scope = scope;
            }

            internal RefTarget Target { get; }

            /// <summary>
            /// Whether the order of a list of references is part of what it says. A
            /// plate's node list is a polygon and its winding is meaning; a support's
            /// node list is a set and its order is an accident of the writer.
            /// </summary>
            internal bool Ordered { get; }

            /// <summary>
            /// The sibling member naming the object this reference is scoped inside —
            /// a region id means nothing without its plate.
            /// </summary>
            internal string? Scope { get; }
        }
    }
}
