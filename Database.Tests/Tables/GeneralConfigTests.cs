﻿//-----------------------------------------------------------------------

// <copyright>
//
// Copyright (c) TU Chemnitz, Prof. Technische Thermodynamik
// Written by Noah Pflugradt.
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
//
// Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//  Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer
// in the documentation and/or other materials provided with the distribution.
//  All advertising materials mentioning features or use of this software must display the following acknowledgement:
//  “This product includes software developed by the TU Chemnitz, Prof. Technische Thermodynamik and its contributors.”
//  Neither the name of the University nor the names of its contributors may be used to endorse or promote products
//  derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE UNIVERSITY 'AS IS' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING,
// BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE UNIVERSITY OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, S
// PECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; L
// OSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
// STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
// ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

// </copyright>

//-----------------------------------------------------------------------

using System;
using System.Linq;
using Automation;
using Common;
using Common.Tests;
using Database.Tables;
using NUnit.Framework;

namespace Database.Tests.Tables {
    [TestFixture]
    public class GeneralConfigTests : UnitTestBaseClass
    {
        [Test]
        [Category("BasicTest")]
        public void LoadFromDatabaseTest() {
            var db = new DatabaseSetup(Utili.GetCurrentMethodAndClass(), DatabaseSetup.TestPackage.DatabaseIo);

            GeneralConfig.LoadFromDatabase(db.ConnectionString, false);
            db.ClearTable(GeneralConfig.TableName);
            db.ClearTable(SingleOption.TableName);
            var gc = GeneralConfig.LoadFromDatabase(db.ConnectionString, false);

            // options test
            // first make sure none are enabled after clearing the table
            var count = gc.Options.Count(x => x.Value.SettingValue);
            Assert.AreEqual(0, count);
            // enable one and check
            gc.Enable(CalcOption.OverallDats);
            var count2 = gc.Options.Count(x => x.Value.SettingValue);
            Assert.AreEqual(1, count2);
            var gc2 = GeneralConfig.LoadFromDatabase(db.ConnectionString, false);
            var count3 = gc2.Options.Count(x => x.Value.SettingValue);
            Console.WriteLine(count3);
            db.Cleanup();
        }
    }
}