using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Sosi
{
    /// <summary>
    /// The features of a SOSI file, ordered by <see cref="SosiFeature.Id"/> (like <c>features.all()</c> in sosi.js).
    /// The same features in file order are available through <see cref="InFileOrder"/>
    /// (and <see cref="SosiData.FileOrderedFeatures"/>).
    /// </summary>
    public sealed class SosiFeatureCollection : IReadOnlyList<SosiFeature>
    {
        private readonly Dictionary<int, SosiFeature> _byId;
        private readonly List<SosiFeature> _ordered;

        internal SosiFeatureCollection(IEnumerable<SosiFeature> featuresInFileOrder)
        {
            _byId = new Dictionary<int, SosiFeature>();
            var fileOrderIds = new List<int>();
            foreach (var feature in featuresInFileOrder)
            {
                if (!_byId.ContainsKey(feature.Id))
                {
                    fileOrderIds.Add(feature.Id);
                }
                _byId[feature.Id] = feature; // the last one wins, like in sosi.js
                feature.Owner = this;
            }
            _ordered = _byId.Values.OrderBy(f => f.Id).ToList();
            InFileOrder = new ReadOnlyCollection<SosiFeature>(fileOrderIds.Select(id => _byId[id]).ToList());
        }

        /// <summary>Number of features (sosi.js: <c>features.length()</c>).</summary>
        public int Count => _ordered.Count;

        /// <summary>The feature at the given position when ordered by id.</summary>
        public SosiFeature this[int index] => _ordered[index];

        /// <summary>The features in the order they appear in the file (sosi.js: <c>features.all(true)</c>).</summary>
        public IReadOnlyList<SosiFeature> InFileOrder { get; }

        /// <summary>Returns the feature with the given id, or null (sosi.js: <c>features.getById(id)</c>). O(1).</summary>
        public SosiFeature GetById(int id)
        {
            SosiFeature feature;
            return _byId.TryGetValue(id, out feature) ? feature : null;
        }

        /// <summary>Tries to get the feature with the given id. O(1).</summary>
        public bool TryGetById(int id, out SosiFeature feature) => _byId.TryGetValue(id, out feature);

        /// <summary>True if a feature with the given id exists. O(1).</summary>
        public bool ContainsId(int id) => _byId.ContainsKey(id);

        public IEnumerator<SosiFeature> GetEnumerator() => _ordered.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
