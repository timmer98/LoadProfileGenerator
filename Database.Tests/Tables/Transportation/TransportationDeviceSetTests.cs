﻿using System.Collections.ObjectModel;
using Common;
using Common.Tests;
using Database.Tables.BasicHouseholds;
using Database.Tables.Transportation;
using NUnit.Framework;

namespace Database.Tests.Tables.Transportation
{
    [TestFixture]
    public class TransportationDeviceSetTests : UnitTestBaseClass
    {
        [Test]
        [Category("BasicTest")]
        public void TransportationDeviceSetTest()
        {
            DatabaseSetup db = new DatabaseSetup(Utili.GetCurrentMethodAndClass(), DatabaseSetup.TestPackage.DatabaseIo);
            db.ClearTable(TransportationDeviceSet.TableName);
            db.ClearTable(TransportationDevice.TableName);
            db.ClearTable(TransportationDeviceCategory.TableName);
            TransportationDeviceSet tds =
                new TransportationDeviceSet("transportationdeviceset", null, db.ConnectionString, "desc",
                    System.Guid.NewGuid().ToString());
            tds.SaveToDB();

            TransportationDeviceCategory tdc = new TransportationDeviceCategory("transportationdevicecategory",
                null,db.ConnectionString,"desc",true,  System.Guid.NewGuid().ToString());
            tdc.SaveToDB();
            VLoadType vlt = (VLoadType) VLoadType.CreateNewItem(null,db.ConnectionString);
            vlt.SaveToDB();
            TransportationDevice tdev = new TransportationDevice("mydevice",null,db.ConnectionString,"",1,SpeedUnit.Kmh,
                tdc,1000,10,100,100,vlt,
                System.Guid.NewGuid().ToString());
            tdev.SaveToDB();
            ObservableCollection<TransportationDevice> transportationDevices = new ObservableCollection<TransportationDevice>
            {
                tdev
            };
            tds.AddDevice(tdev);
            /*ObservableCollection<TransportationDeviceCategory> categories = new ObservableCollection<TransportationDeviceCategory>
            {
                tdc
            };*/
            ObservableCollection<TransportationDeviceSet> result = new ObservableCollection<TransportationDeviceSet>();
            TransportationDeviceSet.LoadFromDatabase(result, db.ConnectionString, false, transportationDevices);
            db.Cleanup();
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].TransportationDeviceSetEntries.Count);
        }
        [Test]
        [Category("BasicTest")]
        public void TransportationDeviceImportTest()
        {
            var db1 = new DatabaseSetup(Utili.GetCurrentMethodAndClass() + "1", DatabaseSetup.TestPackage.DatabaseIo);
            var db2 = new DatabaseSetup(Utili.GetCurrentMethodAndClass() + "2", DatabaseSetup.TestPackage.DatabaseIo);
            db1.ClearTable(TransportationDeviceSet.TableName);
            var srcSim = new Simulator(db2.ConnectionString);
            var dstSim = new Simulator(db1.ConnectionString);
            foreach (var device in srcSim.TransportationDeviceSets.It)
            {
                TransportationDeviceSet.ImportFromItem(device, dstSim);
            }
        }
    }
}