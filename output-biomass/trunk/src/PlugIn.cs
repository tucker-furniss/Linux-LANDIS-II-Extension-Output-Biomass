//  Copyright 2005-2016 Portland State University, University of Wisconsin
//  Authors:  Robert M. Scheller

using Landis.Core;
using Edu.Wisc.Forest.Flel.Util;
using Landis.Library.BiomassCohorts;
using Landis.SpatialModeling;
using Landis.Library.Biomass;
using Landis.Library.Metadata;

using System;
using System.Collections.Generic;
using System.IO;

namespace Landis.Extension.Output.Biomass
{
    public class PlugIn
        : ExtensionMain
    {
        public static readonly ExtensionType ExtType = new ExtensionType("output");
        public static readonly string ExtensionName = "Biomass Output";
        public static MetadataTable<SppBiomassLog> sppBiomassLog;
        public static MetadataTable<SppBiomassLogLandscape> sppBiomassLogLandscape;
        public static bool MakeMaps;


        private IEnumerable<ISpecies> selectedSpecies;
        private string speciesMapNameTemplate;
        private string selectedPools;
        private string poolMapNameTemplate;
        private IInputParameters parameters;
        private static ICore modelCore;
        private bool makeTable;
        //private StreamWriter log;

        //---------------------------------------------------------------------

        public PlugIn()
            : base(ExtensionName, ExtType)
        {
        }

        //---------------------------------------------------------------------

        public static ICore ModelCore
        {
            get
            {
                return modelCore;
            }
        }
        //---------------------------------------------------------------------

        public override void LoadParameters(string dataFile, ICore mCore)
        {
            modelCore = mCore;
            InputParametersParser parser = new InputParametersParser();
            parameters = Landis.Data.Load<IInputParameters>(dataFile, parser);
        }

        //---------------------------------------------------------------------

        public override void Initialize()
        {

            Timestep = parameters.Timestep;
            this.selectedSpecies = parameters.SelectedSpecies;
            this.speciesMapNameTemplate = parameters.SpeciesMapNames;
            this.selectedPools = parameters.SelectedPools;
            this.poolMapNameTemplate = parameters.PoolMapNames;
            this.makeTable = parameters.MakeTable;

            MetadataHandler.InitializeMetadata(parameters.Timestep, this.selectedSpecies, parameters.SpeciesMapNames, modelCore);

            //if (makeTable)
            //    InitializeLogFile();

            SiteVars.Initialize();
        }

        //---------------------------------------------------------------------

        public override void Run()
        {
            WriteMapForAllSpecies();

            //if (makeTable)
            //    WriteLogFile();

            WritePoolMaps();

            if (selectedSpecies != null)
            {
                WriteSpeciesMaps();
                
            }
            WriteLogFile();
            WriteLandscapeLogFiles();

        }

        //---------------------------------------------------------------------

        private void WriteSpeciesMaps()
        {
            foreach (ISpecies species in selectedSpecies) {
                string path = MakeSpeciesMapName(species.Name);
                PlugIn.ModelCore.UI.WriteLine("   Writing {0} biomass map to {1} ...", species.Name, path);

                using (IOutputRaster<IntPixel> outputRaster = modelCore.CreateRaster<IntPixel>(path, modelCore.Landscape.Dimensions))
                {
                    IntPixel pixel = outputRaster.BufferPixel;
                    foreach (Site site in PlugIn.ModelCore.Landscape.AllSites)
                    {
                        if (site.IsActive)
                            pixel.MapCode.Value = (int)Math.Round((double)ComputeSpeciesBiomass(SiteVars.Cohorts[site][species]));
                        else
                            pixel.MapCode.Value = 0;

                        outputRaster.WriteBufferPixel();
                    }
                }
            }

        }

        //---------------------------------------------------------------------

        private void WriteMapForAllSpecies()
        {
            // Biomass map for all species
            string path = MakeSpeciesMapName("TotalBiomass");
            PlugIn.ModelCore.UI.WriteLine("   Writing total biomass map to {0} ...", path);
            using (IOutputRaster<IntPixel> outputRaster = modelCore.CreateRaster<IntPixel>(path, modelCore.Landscape.Dimensions))
            {
                IntPixel pixel = outputRaster.BufferPixel;
                foreach (Site site in PlugIn.ModelCore.Landscape.AllSites)
                {
                    if (site.IsActive)
                        pixel.MapCode.Value = (int) Math.Round((double) ComputeTotalBiomass(SiteVars.Cohorts[site]));
                    else
                        pixel.MapCode.Value = 0;

                    outputRaster.WriteBufferPixel();
                }
            }
        }

        //---------------------------------------------------------------------

        private string MakeSpeciesMapName(string species)
        {
            return SpeciesMapNames.ReplaceTemplateVars(speciesMapNameTemplate,
                                                       species,
                                                       PlugIn.ModelCore.CurrentTime);
        }


        //---------------------------------------------------------------------

        private void WritePoolMaps()
        {
            if(selectedPools == "woody" || selectedPools == "both")
                WritePoolMap("woody", SiteVars.WoodyDebris);

            if(selectedPools == "non-woody" || selectedPools == "both")
                WritePoolMap("non-woody", SiteVars.Litter);
        }

        //---------------------------------------------------------------------

        private void WritePoolMap(string         poolName,
                                  ISiteVar<Pool> poolSiteVar)
        {
            string path = PoolMapNames.ReplaceTemplateVars(poolMapNameTemplate,
                                                           poolName,
                                                           PlugIn.ModelCore.CurrentTime);
            if(poolSiteVar != null)
            {
                PlugIn.ModelCore.UI.WriteLine("   Writing {0} biomass map to {1} ...", poolName, path);
                using (IOutputRaster<IntPixel> outputRaster = modelCore.CreateRaster<IntPixel>(path, modelCore.Landscape.Dimensions))
                {
                    IntPixel pixel = outputRaster.BufferPixel;
                    foreach (Site site in PlugIn.ModelCore.Landscape.AllSites)
                    {
                        if (site.IsActive)
                            pixel.MapCode.Value = (int)((float)poolSiteVar[site].Mass);
                        else
                            pixel.MapCode.Value = 0;

                        outputRaster.WriteBufferPixel();
                    }
                }
            }
        }
        //---------------------------------------------------------------------
        //---------------------------------------------------------------------

        private void WriteLandscapeLogFiles()
        {

            double[] sppBiomass = new double[modelCore.Species.Count];

            foreach (ActiveSite site in ModelCore.Landscape)
            {
                foreach (ISpecies species in selectedSpecies)
                {
                    sppBiomass[species.Index] += (double) ComputeSpeciesBiomass(SiteVars.Cohorts[site][species]) / ModelCore.Landscape.ActiveSiteCount;
                }
            }

            //int selectSppCnt = 0;

            //foreach (ISpecies species in selectedSpecies)
            //{

            sppBiomassLogLandscape.Clear();
            SppBiomassLogLandscape sbil = new SppBiomassLogLandscape();
            sbil.Time = ModelCore.CurrentTime;
            sbil.Biomass_ = sppBiomass;
            sppBiomassLogLandscape.AddObject(sbil);
            sppBiomassLogLandscape.WriteToFile();

            //    selectSppCnt++;

            //}

        }


        //---------------------------------------------------------------------

        private void WriteLogFile()
        {

            double[,] allSppEcos = new double[ModelCore.Ecoregions.Count, ModelCore.Species.Count];

            int[] activeSiteCount = new int[ModelCore.Ecoregions.Count];

            //UI.WriteLine("Next, reset all values to zero.");

            foreach (IEcoregion ecoregion in ModelCore.Ecoregions)
            {
                int sppCnt = 0;
                foreach (ISpecies species in selectedSpecies)
                {
                    allSppEcos[ecoregion.Index, sppCnt] = 0.0;
                    sppCnt++;
                }

                activeSiteCount[ecoregion.Index] = 0;
            }

            //UI.WriteLine("Next, accumulate data.");


            foreach (ActiveSite site in ModelCore.Landscape)
            {
                IEcoregion ecoregion = ModelCore.Ecoregion[site];

                int sppCnt = 0;
                foreach (ISpecies species in selectedSpecies)
                {
                    allSppEcos[ecoregion.Index, sppCnt] += ComputeSpeciesBiomass(SiteVars.Cohorts[site][species]);
                    sppCnt++;
                }

                activeSiteCount[ecoregion.Index]++;
            }

            foreach (IEcoregion ecoregion in ModelCore.Ecoregions)
            {
                sppBiomassLog.Clear();
                SppBiomassLog sbl = new SppBiomassLog();
                double[] sppBiomass = new double[modelCore.Species.Count];

                //int sppCnt = 0;
                foreach (ISpecies species in ModelCore.Species)
                {
                    sppBiomass[species.Index] = allSppEcos[ecoregion.Index, species.Index] / (double)activeSiteCount[ecoregion.Index];
                }
                sbl.Time = ModelCore.CurrentTime;
                sbl.Ecoregion = ecoregion.Name;
                sbl.EcoregionIndex = ecoregion.Index;
                sbl.NumSites = activeSiteCount[ecoregion.Index];
                sbl.Biomass_ = sppBiomass;
                sppBiomassLog.AddObject(sbl);
                sppBiomassLog.WriteToFile();


            }
        }
        //public void InitializeLogFile()
        //{

        //    string logFileName = "spp-biomass-log.csv";
        //    PlugIn.ModelCore.UI.WriteLine("   Opening species biomass log file \"{0}\" ...", logFileName);
        //    try
        //    {
        //        log = Landis.Data.CreateTextFile(logFileName);
        //    }
        //    catch (Exception err)
        //    {
        //        string mesg = string.Format("{0}", err.Message);
        //        throw new System.ApplicationException(mesg);
        //    }

        //    log.AutoFlush = true;
        //    log.Write("Time, Ecoregion, NumSites,");

        //    foreach (ISpecies species in ModelCore.Species)
        //        log.Write("{0},", species.Name);

        //    log.WriteLine("");


        //}


        //---------------------------------------------------------------------

        //private void WriteLogFile()
        //{


        //    double[,] allSppEcos = new double[ModelCore.Ecoregions.Count, ModelCore.Species.Count];

        //    int[] activeSiteCount = new int[ModelCore.Ecoregions.Count];

        //    //UI.WriteLine("Next, reset all values to zero.");

        //    foreach (IEcoregion ecoregion in ModelCore.Ecoregions)
        //    {
        //        foreach (ISpecies species in ModelCore.Species)
        //        {
        //            allSppEcos[ecoregion.Index, species.Index] = 0.0;
        //        }

        //        activeSiteCount[ecoregion.Index] = 0;
        //    }

        //    //UI.WriteLine("Next, accumulate data.");


        //    foreach (ActiveSite site in ModelCore.Landscape)
        //    {
        //        IEcoregion ecoregion = ModelCore.Ecoregion[site];

        //        foreach (ISpecies species in ModelCore.Species)
        //        {
        //            allSppEcos[ecoregion.Index, species.Index] += ComputeSpeciesBiomass(SiteVars.Cohorts[site][species]);
        //        }

        //        activeSiteCount[ecoregion.Index]++;
        //    }

        //    foreach (IEcoregion ecoregion in ModelCore.Ecoregions)
        //    {
        //        log.Write("{0}, {1}, {2}, ",
        //            ModelCore.CurrentTime,                 // 0
        //            ecoregion.Name,                         // 1
        //            activeSiteCount[ecoregion.Index]       // 2
        //            );
        //        foreach (ISpecies species in ModelCore.Species)
        //        {
        //            log.Write("{0}, ",
        //                (allSppEcos[ecoregion.Index, species.Index] / (double)activeSiteCount[ecoregion.Index])
        //                );

        //        }

        //        log.WriteLine("");
        //    }
        //}
        //---------------------------------------------------------------------
        private static int ComputeSpeciesBiomass(ISpeciesCohorts cohorts)
        {
            int total = 0;
            if (cohorts != null)
                foreach (ICohort cohort in cohorts)
                    total += cohort.Biomass;
            return total;
        }

        //---------------------------------------------------------------------

        private static int ComputeTotalBiomass(ISiteCohorts cohorts)
        {
            int total = 0;
            if (cohorts != null)
                foreach (ISpeciesCohorts speciesCohorts in cohorts)
                {
                    total += ComputeSpeciesBiomass(speciesCohorts);
                }
            return total;
        }
    }
}
