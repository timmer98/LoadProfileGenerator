﻿using System.Collections.ObjectModel;
using Common;
using Common.Tests;
using Database.Tables.BasicElements;
using NUnit.Framework;

namespace Database.Tests.Tables.BasicElements
{
    [TestFixture]
    public class VariableTests : UnitTestBaseClass
    {
        [Test]
        [Category("BasicTest")]
        public void VariableTest()
        {
            DatabaseSetup db = new DatabaseSetup(Utili.GetCurrentMethodAndClass(), DatabaseSetup.TestPackage.DatabaseIo);
            db.ClearTable(Variable.TableName);
            Variable t = new Variable("blub", "desc", "unit", db.ConnectionString, System.Guid.NewGuid().ToString());
            t.SaveToDB();
            ObservableCollection<Variable> allVariables = new ObservableCollection<Variable>();
            Variable.LoadFromDatabase(allVariables, db.ConnectionString, false);
            Assert.AreEqual(1, allVariables.Count);
            allVariables[0].DeleteFromDB();
            allVariables.Clear();
            Variable.LoadFromDatabase(allVariables, db.ConnectionString, false);
            Assert.AreEqual(0, allVariables.Count);
            db.Cleanup();
        }
    }
}