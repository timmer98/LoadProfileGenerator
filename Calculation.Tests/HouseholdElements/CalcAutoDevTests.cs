﻿using System;
using System.Collections.Generic;
using System.Globalization;
using Automation.ResultFiles;
using CalculationController.DtoFactories;
using CalculationEngine.Helper;
using CalculationEngine.HouseholdElements;
using CalculationEngine.OnlineDeviceLogging;
using CalculationEngine.OnlineLogging;
using Common;
using Common.CalcDto;
using Common.Enums;
using Common.JSON;
using Common.SQLResultLogging;
using Common.SQLResultLogging.InputLoggers;
using Common.Tests;
using NUnit.Framework;

namespace Calculation.Tests.HouseholdElements {
    [TestFixture]
    public class CalcAutoDevTests : UnitTestBaseClass
    {
        [Test]
        [Category("BasicTest")]
        public void CheckConditionTest() {
            Assert.IsTrue(VariableConditionHelper.CheckCondition(1, VariableCondition.Equal, 1));
            Assert.IsFalse(VariableConditionHelper.CheckCondition(0, VariableCondition.Equal, 1));

            Assert.IsTrue(VariableConditionHelper.CheckCondition(1, VariableCondition.EqualOrGreater, 1));
            Assert.IsTrue(VariableConditionHelper.CheckCondition(1.5, VariableCondition.EqualOrGreater, 1));
            Assert.IsFalse(VariableConditionHelper.CheckCondition(0, VariableCondition.EqualOrGreater, 1));

            Assert.IsTrue(VariableConditionHelper.CheckCondition(0, VariableCondition.EqualOrLess, 1));
            Assert.IsTrue(VariableConditionHelper.CheckCondition(1, VariableCondition.EqualOrLess, 1));
            Assert.IsFalse(VariableConditionHelper.CheckCondition(1.1, VariableCondition.EqualOrLess, 1));

            Assert.IsTrue(VariableConditionHelper.CheckCondition(0, VariableCondition.Less, 1));
            Assert.IsFalse(VariableConditionHelper.CheckCondition(1, VariableCondition.Less, 1));
            Assert.IsFalse(VariableConditionHelper.CheckCondition(1.1, VariableCondition.Less, 1));

            Assert.IsTrue(VariableConditionHelper.CheckCondition(1.5, VariableCondition.Greater, 1));
            Assert.IsFalse(VariableConditionHelper.CheckCondition(1, VariableCondition.Greater, 1));
            Assert.IsFalse(VariableConditionHelper.CheckCondition(0, VariableCondition.Greater, 1));
        }

        [Test]
        [Category("BasicTest")]
        public void CheckResultingProfile() {
            var wd = new WorkingDir(Utili.GetCurrentMethodAndClass());
            wd.InputDataLogger.AddSaver(new ColumnEntryLogger(wd.SqlResultLoggingService));
            wd.InputDataLogger.AddSaver(new ResultFileEntryLogger(wd.SqlResultLoggingService));
            wd.InputDataLogger.AddSaver(new HouseholdKeyLogger(wd.SqlResultLoggingService));
            DateTime startdate = new DateTime(2018, 1, 1);
            DateTime enddate = startdate.AddMinutes(100);
            CalcParameters calculationParameters = CalcParametersFactory.MakeGoodDefaults().SetStartDate(startdate).SetEndDate(enddate).EnableShowSettlingPeriod();
            var profile = new CalcProfile("profile", Guid.NewGuid().ToString(), new TimeSpan(0, 1, 0), ProfileType.Relative, "blub");
            profile.AddNewTimepoint(new TimeSpan(0), 0.01);
            profile.AddNewTimepoint(new TimeSpan(1, 0, 0), 0.01);
            profile.ConvertToTimesteps();
            var cloadtype = new CalcLoadType("loadtype",  "power", "sum", 1, true, Guid.NewGuid().ToString());
            var loads = new List<CalcDeviceLoad>();
            var cdl = new CalcDeviceLoad("cdevload",  100, cloadtype, 100, 0.1, Guid.NewGuid().ToString());
            loads.Add(cdl);
            var r = new Random(5);
            HouseholdKey key = new HouseholdKey("hh1");
            var nr = new NormalRandom(0, 1, r);
            var fft = new FileFactoryAndTracker(wd.WorkingDirectory,"householdname",wd.InputDataLogger);
            fft.RegisterHousehold(key,"hh1",HouseholdKeyType.Household, "desc",null,null);
            fft.RegisterHousehold(Constants.GeneralHouseholdKey, "general",HouseholdKeyType.General,"desc",null,null);

            //SqlResultLoggingService srls = new SqlResultLoggingService(wd.WorkingDirectory);
            DateStampCreator dsc = new DateStampCreator(calculationParameters);
            OnlineLoggingData old = new OnlineLoggingData(dsc,wd.InputDataLogger,calculationParameters);
            using (var lf = new LogFile(calculationParameters,fft,old,wd.SqlResultLoggingService, true)) {
                var odap = new OnlineDeviceActivationProcessor(nr, lf,calculationParameters);
                var location = new CalcLocation("calcloc", Guid.NewGuid().ToString());
                CalcVariableRepository crv = new CalcVariableRepository();
                string variableGuid = Guid.NewGuid().ToString();
                CalcVariable cv = new CalcVariable("varname",variableGuid,0,location.Name,location.Guid,key);
                crv.RegisterVariable(cv);
                VariableRequirement vreq = new VariableRequirement(cv.Name,0,location.Name,location.Guid,
                    VariableCondition.Equal,crv,variableGuid);
                List<VariableRequirement> requirements = new List<VariableRequirement>
                {
                    vreq
                };
                string deviceCategoryGuid = Guid.NewGuid().ToString();
                var cad = new CalcAutoDev("autodevnamename", profile, cloadtype, loads,
                    0.8, deviceCategoryGuid, odap, key, 1, location,
                     "device category",calculationParameters, Guid.NewGuid().ToString(),requirements);
                for (var i = 0; i < 100; i++) {
                    TimeStep ts  =new TimeStep(i, calculationParameters);
                    if (!cad.IsBusyDuringTimespan(ts, 1, 0.7, cloadtype)) {
                        cad.Activate(ts, nr);
                    }
                    var rows = odap.ProcessOneTimestep(ts);
                    foreach (var energyFileRow in rows) {
                        foreach (var energyEntry in energyFileRow.EnergyEntries) {
                            Logger.Info(energyEntry.ToString(CultureInfo.CurrentCulture));
                        }
                    }
                }
            }
            wd.CleanUp();
        }
    }
}