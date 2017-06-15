//  Copyright 2005-2010 Portland State University, University of Wisconsin
//  Authors:  Robert M. Scheller, James B. Domingo

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
        public static readonly string ExtensionName = "Output Biomass";

        public static IEnumerable<ISpecies> speciesToMap;
        public static string speciesTemplateToMap;
        public static string poolsToMap;
        public static string poolsTemplateToMap;
        private IEnumerable<ISpecies> selectedSpecies;
        private string speciesMapNameTemplate;
        private string selectedPools;
        private string poolMapNameTemplate;
        private IInputParameters parameters;
        private static ICore modelCore;
        private bool makeTable;
        public static MetadataTable<SummaryLog> summaryLog;

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
            speciesToMap = this.selectedSpecies;
            this.speciesMapNameTemplate = parameters.SpeciesMapNames;
            speciesTemplateToMap = this.speciesMapNameTemplate;
            this.selectedPools = parameters.SelectedPools;
            poolsToMap = this.selectedPools;
            this.poolMapNameTemplate = parameters.PoolMapNames;
            poolsTemplateToMap = this.poolMapNameTemplate;
            this.makeTable = parameters.MakeTable;

            if (makeTable)
            {
                InitializeLogFile();
            }

            SiteVars.Initialize();
        }

        //---------------------------------------------------------------------

        public override void Run()
        {
            WriteMapForAllSpecies();

            if (makeTable)
                WriteLogFile();

            WritePoolMaps();

            if (selectedSpecies != null)
            {
                WriteSpeciesMaps();
                
            }
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
        public void InitializeLogFile()
        {
            MetadataHandler.InitializeMetadata(parameters.Timestep, "spp-biomass-log.csv");
        } 


        //---------------------------------------------------------------------

        private void WriteLogFile()
        {


            double[,] allSppEcos = new double[ModelCore.Ecoregions.Count, ModelCore.Species.Count];

            int[] activeSiteCount = new int[ModelCore.Ecoregions.Count];

            //UI.WriteLine("Next, reset all values to zero.");

            foreach (IEcoregion ecoregion in ModelCore.Ecoregions)
            {
                foreach (ISpecies species in ModelCore.Species)
                {
                    allSppEcos[ecoregion.Index, species.Index] = 0.0;
                }

                activeSiteCount[ecoregion.Index] = 0;
            }

            //UI.WriteLine("Next, accumulate data.");


            foreach (ActiveSite site in ModelCore.Landscape)
            {
                IEcoregion ecoregion = ModelCore.Ecoregion[site];

                foreach (ISpecies species in ModelCore.Species)
                {
                    allSppEcos[ecoregion.Index, species.Index] += ComputeSpeciesBiomass(SiteVars.Cohorts[site][species]);
                }

                activeSiteCount[ecoregion.Index]++;
            }

            foreach (IEcoregion ecoregion in ModelCore.Ecoregions)
            {
                summaryLog.Clear();
                SummaryLog sl = new SummaryLog();
                sl.Time = modelCore.CurrentTime;
                sl.EcoName = ecoregion.Name;
                sl.NumActiveSites = activeSiteCount[ecoregion.Index];
                double[] aboveBiomass = new double[modelCore.Species.Count];

                foreach (ISpecies species in ModelCore.Species)
                {
                    aboveBiomass[species.Index] = allSppEcos[ecoregion.Index, species.Index] / (double)activeSiteCount[ecoregion.Index];
                }
                sl.AboveGroundBiomass_ = aboveBiomass;

                summaryLog.AddObject(sl);
                summaryLog.WriteToFile();
            }
        }
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
