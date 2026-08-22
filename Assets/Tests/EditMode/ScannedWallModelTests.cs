using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DrillingAssistant.Tests
{
    public class ScannedWallModelTests
    {
        private GameObject _owner;
        private ScannedWallModel _model;

        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("ModelUnderTest");
            _model = _owner.AddComponent<ScannedWallModel>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_owner);
        }

        [Test]
        public void EachMarkerGetsItsOwnId()
        {
            Assert.AreEqual(1, _model.AddMarker(Vector3.zero).Id);
            Assert.AreEqual(2, _model.AddMarker(Vector3.right).Id);
        }

        [Test]
        public void RestoreKeepsMarkerIdsAndAllocatesAboveThem()
        {
            _model.AddMarker(Vector3.zero);
            var saved = new List<UserMarker>
            {
                new UserMarker { Id = 5, LocalPosition = Vector3.up }
            };

            _model.RestoreState(new List<ScannedStructure>(), saved, new List<PlannedRoute>());

            Assert.AreEqual(5, _model.Markers[0].Id, "a restored marker keeps its id");
            Assert.AreEqual(6, _model.AddMarker(Vector3.zero).Id, "the next id clears every restored one");
        }

        [Test]
        public void RestoreKeepsRouteIdsAndAllocatesAboveThem()
        {
            var saved = new List<PlannedRoute>
            {
                new PlannedRoute { Id = 3, LocalStart = Vector3.zero, LocalEnd = Vector3.right }
            };

            _model.RestoreState(new List<ScannedStructure>(), new List<UserMarker>(), saved);

            Assert.AreEqual(3, _model.Routes[0].Id);
            Assert.AreEqual(4, _model.AddRoute(Vector3.zero, Vector3.up).Id);
        }

        [Test]
        public void OnlyARealRemovalIsReported()
        {
            int reported = 0;
            _model.MarkerRemoved += _ => reported++;
            var marker = _model.AddMarker(Vector3.zero);

            _model.RemoveMarker(marker.Id + 99);
            Assert.AreEqual(0, reported, "removing an unknown id changes nothing");

            _model.RemoveMarker(marker.Id);
            Assert.AreEqual(1, reported);
            Assert.IsEmpty(_model.Markers);
        }
    }
}
