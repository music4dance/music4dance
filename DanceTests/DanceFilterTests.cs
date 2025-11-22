using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using Newtonsoft.Json;

namespace DanceLibrary.Tests
{
    [TestClass]
    public class DanceFilterTests
    {
        #region Reduce
        [TestMethod]
        public void ReduceReturnsFlattenedTypeWhenOrgainzationsAreCovered()
        {
            var dance = CreateSamba();
            var filter = new DanceFilter(organizations: ["DanceSport", "NDCA"]);

            var result = filter.Reduce(dance);

            Assert.IsNotNull(result);
            Assert.AreEqual(dance.Instances.Count, result.Instances.Count);
            Assert.AreEqual(dance.Meter, result.Meter);
            Assert.AreEqual(dance.TempoRange, result.TempoRange);
            for (var i = 0; i < dance.Instances.Count; i++)
            {
                var org = dance.Instances[i];
                var res = result.Instances[i];

                Assert.AreEqual(org.Style, res.Style);
                Assert.AreEqual(org.TempoRange, res.TempoRange);
                Assert.AreEqual(0, res.Exceptions.Count);
            }
        }

        [TestMethod]
        public void ReduceReturnsSingleInstanceWhenSingleStyleSpecified()
        {
            var dance = CreateSamba();
            var filter = new DanceFilter(styles: ["International Latin"]);

            var result = filter.Reduce(dance);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Instances.Count);
            Assert.AreEqual(dance.Meter, result.Meter);
            Assert.AreEqual(new TempoRange(100, 104), result.TempoRange);
            var instance = result.Instances[0];
            Assert.AreEqual("International Latin", instance.Style);
            Assert.AreEqual(0, instance.Exceptions.Count);
        }

        [TestMethod]
        public void ReduceReturnsCorrectTempoWhenOrganizationExceptionSpecified()
        {
            var dance = CreateSamba();
            var filter = new DanceFilter(styles: ["American Rhythm"], organizations: ["NDCA", "DanceSport"]);

            var result = filter.Reduce(dance);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Instances.Count);
            Assert.AreEqual(dance.Meter, result.Meter);
            Assert.AreEqual(new TempoRange(96, 100), result.TempoRange);
            var instance = result.Instances[0];
            Assert.AreEqual("American Rhythm", instance.Style);
            Assert.AreEqual(0, instance.Exceptions.Count);
        }

        [TestMethod]
        public void ReduceReturnsCorrectTempoWhenMultipleOrganizationsSpecified()
        {
            var dance = CreateSamba();
            var filter = new DanceFilter(styles: ["American Rhythm"], organizations: ["NDCA"]);

            var result = filter.Reduce(dance);

            // New tempo range should be 100
            var newTempo = new TempoRange(100, 100);
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Instances.Count);
            Assert.AreEqual(dance.Meter, result.Meter);
            Assert.AreEqual(newTempo, result.TempoRange);
            var instance = result.Instances[0];
            Assert.AreEqual("American Rhythm", instance.Style);
            Assert.AreEqual(0, instance.Exceptions.Count);
            Assert.AreEqual(newTempo, instance.TempoRange);
        }


        [TestMethod]
        public void ReduceReturnsNullWhenIncompatibleOrganizationSpecified()
        {
            var dance = CreateSamba();
            var filter = new DanceFilter(styles: ["International Standard"]);

            var result = filter.Reduce(dance);

            Assert.IsNull(result);
        }

        private static readonly string smbJson = @"
          {
            'id': 'SMB',
            'name': 'Samba',
            'meter': {
              'numerator': 2,
              'denominator': 4
            },
            'instances': [
              {
                'style': 'International Latin',
                'organizations': [
                  'DanceSport',
                  'NDCA'
                ],
                'tempoRange': {
                  'min': 100.0,
                  'max': 104.0
                },
                'competitionGroup': 'Ballroom',
                'competitionOrder': 2,
                'exceptions': [
                  {
                    'organization': 'NDCA',
                    'tempoRange': {
                      'min': 100.0,
                      'max': 100.0
                    }
                  }
                ]
              },
              {
                'style': 'American Rhythm',
                'organizations': [
                  'DanceSport',
                  'NDCA'
                ],
                'tempoRange': {
                  'min': 96.0,
                  'max': 100.0
                },
                'competitionGroup': 'Ballroom',
                'exceptions': [
                  {
                    'organization': 'NDCA',
                    'tempoRange': {
                      'min': 100.0,
                      'max': 100.0
                    }
                  },
                  {
                    'organization': 'DanceSport',
                    'tempoRange': {
                      'min': 96.0,
                      'max': 96.0
                    }
                  }
                ]
              }
            ]
          }
        ";

        private static DanceType CreateSamba()
        {
            var samba = JsonConvert.DeserializeObject<DanceType>(smbJson);
            return samba;
        }
        #endregion

        #region MatchMeter
        [TestMethod]
        public void MatchMeterSucceedsWhenFilterMeterEqualsDanceMeter()
        {
            var dance = new DanceType() { Meter = new Meter(4, 4) };
            var filter = new DanceFilter(meter: new Meter(4, 4));
            Assert.IsTrue(filter.MatchMeter(dance));
        }

        [TestMethod]
        public void MatchMeterSucceedsWhenFilterMeterIsEmpty()
        {
            var dance = new DanceType() { Meter = new Meter(4, 4) };
            var filter = new DanceFilter();
            Assert.IsTrue(filter.MatchMeter(dance));
        }

        [TestMethod]
        public void MatchMeterFailsWhenFilterMeterNotEqualToDanceMeter()
        {
            var dance = new DanceType() { Meter = new Meter(4, 4) };
            var filter = new DanceFilter(meter: new Meter(3, 4));

            var result = filter.MatchMeter(dance);

            Assert.IsFalse(result);
        }

        #endregion

        #region MatchGroups
        [TestMethod]
        public void MatchGroupsSucceedsWhenGroupsMatch()
        {
            var filter = new DanceFilter(groups: ["Group1", "Group2"]);
            var type = CreateTypeWithGroups(["Group1", "Group2"]);

            var result = filter.MatchGroups(type);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void MatchGroupsSucceedsWhenFilterIsSubset()
        {
            var filter = new DanceFilter(groups: ["Group1"]);
            var type = CreateTypeWithGroups(["Group2", "Group1"]);

            var result = filter.MatchGroups(type);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void MatchGroupsSucceedsWhenFilterIsSuperset()
        {
            var filter = new DanceFilter(groups: ["Group1", "Group2"]);
            var type = CreateTypeWithGroups(["Group1"]);

            var result = filter.MatchGroups(type);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void MatchGroupsSucceedsWhenGroupsOverlap()
        {
            var filter = new DanceFilter(groups: ["Group1", "Group2"]);
            var type = CreateTypeWithGroups(["Group2", "Group3"]);

            var result = filter.MatchGroups(type);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void MatchGroupsSucceedsWhenGroupsAreEmpty()
        {
            var filter = new DanceFilter();
            var type = CreateTypeWithGroups(["Group1", "Group2"]);

            var result = filter.MatchGroups(type);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void MatchGroupsFailsWhenGroupsDoNotMatch()
        {
            var filter = new DanceFilter(groups: ["Group1", "Group2"]);
            var type = CreateTypeWithGroups(["Group3", "Group4"]);

            var result = filter.MatchGroups(type);

            Assert.IsFalse(result);
        }

        private static DanceType CreateTypeWithGroups(IEnumerable<string> groups)
        {
            var g = groups.Select(g => new DanceGroup(g, g, ["D"])).ToList();
            var mock = new Mock<DanceType>();
            mock.Setup(t => t.Groups).Returns(g);
            return mock.Object;
        }


        [TestMethod]
        public void MatchGroupsFailsWhenTypeHasNoGroups()
        {
            var filter = new DanceFilter(groups: ["Group1", "Group2"]);
            var type = new DanceType();

            var result = filter.MatchGroups(type);

            Assert.IsFalse(result);
        }
        #endregion

        #region MatchOrganization
        [TestMethod]
        public void MatchOrganizationsSucceedsWhenOrganizationsMatch()
        {
            var filter = new DanceFilter(organizations: ["Org1", "Org2"]);
            var type = CreateTypeWithOrganizations(["Org2", "Org1"]);

            var result = filter.MatchOrganizations(type);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void MatchOrganizationsSucceedsWhenFilterIsSubset()
        {
            var filter = new DanceFilter(organizations: ["Org1"]);
            var type = CreateTypeWithOrganizations(["Org2", "Org1"]);

            var result = filter.MatchOrganizations(type);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void MatchOrganizationsSucceedsWhenFilterIsSuperset()
        {
            var filter = new DanceFilter(organizations: ["Org1", "Org2"]);
            var type = CreateTypeWithOrganizations(["Org1"]);

            var result = filter.MatchOrganizations(type);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void MatchOrganizationsSucceedsWhenOrganizationsOverlap()
        {
            var filter = new DanceFilter(organizations: ["Org1", "Org2"]);
            var type = CreateTypeWithOrganizations(["Org2", "Org3"]);

            var result = filter.MatchOrganizations(type);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void MatchOrganizationsSucceedsWhenOrganizationsAreEmpty()
        {
            var filter = new DanceFilter();
            var type = CreateTypeWithOrganizations(["Org2", "Org1"]);

            var result = filter.MatchOrganizations(type);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void MatchOrganizationsFailsWhenOrganizationsDoNotMatch()
        {
            var filter = new DanceFilter(organizations: ["Org1", "Org2"]);
            var type = CreateTypeWithOrganizations(["Org3", "Org4"]);

            var result = filter.MatchOrganizations(type);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void MatchOrganizationsFailsWhenTypeHasNoOrganizations()
        {
            var filter = new DanceFilter(organizations: ["Org1", "Org2"]);
            var type = CreateTypeWithOrganizations([]);

            var result = filter.MatchOrganizations(type);

            Assert.IsFalse(result);
        }

        private static DanceType CreateTypeWithOrganizations(string[] organizations)
        {
            var instance = new DanceInstance(
                "Test Style",
                new TempoRange(100, 120),
                [],
                organizations
            );
            var type = new DanceType("Test", new Meter(4, 4), [instance]);
            return type;
        }

        #endregion
    }
}
