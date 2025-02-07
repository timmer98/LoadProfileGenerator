﻿using System;
using System.Collections.Generic;
using System.IO;
using Automation.ResultFiles;
using Common;
using Database;
using Database.Tables.ModularHouseholds;
using Newtonsoft.Json;

namespace SimulationEngineLib.Other {
    public class JsonDatabaseImporter {
        [JetBrains.Annotations.NotNull] private readonly string _connectionString;
        [JetBrains.Annotations.NotNull] private readonly CalculationProfiler _calculationProfiler;
        public JsonDatabaseImporter([JetBrains.Annotations.NotNull] string connectionString)
        {
            _connectionString = connectionString;
            _calculationProfiler = new CalculationProfiler();
        }

        public bool Import([JetBrains.Annotations.NotNull] JsonDatabaseImportOptions calcDirectoryOptions)
        {
            _calculationProfiler.StartPart(Utili.GetCurrentMethodAndClass());
            string jsonFileName = calcDirectoryOptions.Input;
            if (jsonFileName == null)
            {
                Logger.Error("No file was set.");
                return false;
            }

            if (!File.Exists(jsonFileName)) {
                Logger.Error("File does not exist");
                return false;
            }
            Logger.Info("Loading...");
            var sim = new Simulator(_connectionString);
            Logger.Info("Loading finished.");
            switch (calcDirectoryOptions.Type) {
                case TypesToProcess.HouseholdTemplates: {
                    string json = File.ReadAllText(jsonFileName);
                        var hhts = JsonConvert.DeserializeObject<List<HouseholdTemplate.JsonDto>>(json);
                        if(hhts == null) {
                            throw new LPGException("hhts was null");
                        }
                    Logger.Info("Started loading " + hhts.Count + " household templates");

                    HouseholdTemplate.ImportObjectFromJson(sim, hhts);
                    Logger.Info("Finished loading " + hhts.Count + " household templates");
                    break;
                }
                case TypesToProcess.ModularHouseholds: {
                    string json = File.ReadAllText(jsonFileName);
                        var hhts = JsonConvert.DeserializeObject<List<ModularHousehold.JsonModularHousehold>>(json);
                        if (hhts == null) {
                            throw new LPGException("hhts was null");
                        }
                    Logger.Info("Started loading " + hhts.Count + " households");
                    ModularHousehold.ImportObjectFromJson(sim, hhts);
                    Logger.Info("Finished loading " + hhts.Count + " households");
                    break;
                }
                case TypesToProcess.None:
                    throw new LPGException("You need to set a type that you want to process");
                case TypesToProcess.HouseholdTraits: {
                    string json = File.ReadAllText(jsonFileName);
                    var hhts = JsonConvert.DeserializeObject<List<HouseholdTrait.JsonDto>>(json);
                        if (hhts == null) {
                            throw new LPGException("hhts was null");
                        }
                    Logger.Info("Started loading " + hhts.Count + " traits");
                    HouseholdTrait.ImportObjectFromJson(sim, hhts);
                    Logger.Info("Finished loading " + hhts.Count + " traits");
                }
                    break;
                case TypesToProcess.HouseholdTraitsWithDeviceCategories:
                    throw new LPGException("You need to set a type that you want to process");
                default:
                    throw new ArgumentOutOfRangeException(nameof(JsonDatabaseImportOptions));
            }

            _calculationProfiler.StopPart(Utili.GetCurrentMethodAndClass());
            return true;
        }

    }
}