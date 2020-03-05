﻿using System;
using System.Collections.ObjectModel;
using Common;
using Common.Tests;
using Database.Tables.BasicHouseholds;
using NUnit.Framework;

namespace Database.Tests.Tables.BasicHouseholds
{
    [TestFixture]
    public class DeviceActionGroupTests : UnitTestBaseClass
    {
        [Test]
        [Category("BasicTest")]
        public void DeviceActionGroupTestDeleteFromSim()
        {
            Config.IsInUnitTesting = true;
            DatabaseSetup db = new DatabaseSetup(Utili.GetCurrentMethodAndClass(), DatabaseSetup.TestPackage.DatabaseIo);

            Logger.Threshold = Severity.Error;
            Simulator sim = new Simulator(db.ConnectionString);
            sim.DeviceActionGroups.It[0].DeleteFromDB();
            db.Cleanup();
        }

        [Test]
        [Category("BasicTest")]
        public void DeviceActionGroupTestsAll()
        {
            DatabaseSetup db = new DatabaseSetup(Utili.GetCurrentMethodAndClass(), DatabaseSetup.TestPackage.DatabaseIo);

            ObservableCollection<DeviceActionGroup> dags = new ObservableCollection<DeviceActionGroup>();

            DeviceActionGroup.LoadFromDatabase(dags, db.ConnectionString, false);
            db.ClearTable(DeviceActionGroup.TableName);
            DeviceActionGroup dag = new DeviceActionGroup("bla", db.ConnectionString, "desc", Guid.NewGuid().ToString());
            dag.SaveToDB();
            dags.Clear();
            DeviceActionGroup.LoadFromDatabase(dags, db.ConnectionString, false);
            Assert.AreEqual(1, dags.Count);
            dags[0].DeleteFromDB();
            dags.Clear();
            DeviceActionGroup.LoadFromDatabase(dags, db.ConnectionString, false);
            Assert.AreEqual(0, dags.Count);
            db.Cleanup();
        }
    }
}