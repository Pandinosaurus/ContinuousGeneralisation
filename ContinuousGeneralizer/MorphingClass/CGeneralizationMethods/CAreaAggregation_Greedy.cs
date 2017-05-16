﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Catalog;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.DataSourcesFile;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Editor;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.GeoAnalyst;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Maplex;
using ESRI.ArcGIS.Output;
using ESRI.ArcGIS.SystemUI;

using MorphingClass.CAid;
using MorphingClass.CEntity;
using MorphingClass.CMorphingMethods;
using MorphingClass.CMorphingMethods.CMorphingMethodsBase;
using MorphingClass.CUtility;
using MorphingClass.CGeometry;
using MorphingClass.CGeometry.CGeometryBase;
using MorphingClass.CCorrepondObjects;

namespace MorphingClass.CGeneralizationMethods
{
    public class CAreaAggregation_Greedy : CAreaAggregation_Base
    {
        public CAreaAggregation_Greedy()
        {

        }

        public CAreaAggregation_Greedy(CParameterInitialize ParameterInitialize, string strSpecifiedFieldName = null, string strSpecifiedValue = null)
        {
            Preprocessing(ParameterInitialize, strSpecifiedFieldName, strSpecifiedValue);
        }

        public void AreaAggregation()
        {

            SetupBasic();
            

            CRegion._lngEstCountEdgeNumber = 0;
            CRegion._lngEstCountEdgeLength = 0;
            CRegion._lngEstCountEqual = 0;

            for (int i = _intStart; i < _intEnd; i++)
            {
                Greedy(LSCrgLt[i], SSCrgLt[i], this.StrObjLtSD, _ParameterInitialize.strAreaAggregation);
            }

        }


        public CRegion Greedy(CRegion LSCrg, CRegion SSCrg, CStrObjLtSD StrObjLtSD, string strAreaAggregation)
        {
            var ExistingCorrCphsSD0 = LSCrg.SetInitialAdjacency();  //also count the number of edges

            Stopwatch pStopwatchOverHead = new Stopwatch();
            pStopwatchOverHead.Start();            


            AddLineToStrObjLtSD(StrObjLtSD, LSCrg);

            long lngStartMemory = 0;
            Console.WriteLine();
            Console.WriteLine("Crg:  ID  " + LSCrg.ID + ";    n  " + LSCrg.CphTypeIndexSD_Area_CphGID.Count + ";    m  " +
                    LSCrg.AdjCorrCphsSD.Count + "   " + CConstants.strShapeConstraint + "   " + strAreaAggregation);

            lngStartMemory = GC.GetTotalMemory(true);
            long lngTimeOverHead = pStopwatchOverHead.ElapsedMilliseconds;
            pStopwatchOverHead.Stop();

            Stopwatch pStopwatchLast = new Stopwatch();
            long lngTime = 0;

            CRegion resultcrg = new CRegion(-2);
            try
            {
                pStopwatchLast.Restart();
                var ExistingCorrCphsSD = new SortedDictionary<CCorrCphs, CCorrCphs>(ExistingCorrCphsSD0, ExistingCorrCphsSD0.Comparer);
                LSCrg.cenumColor = CEnumColor.white;

                resultcrg = Compute(LSCrg, SSCrg, SSCrg.GetSoloCphTypeIndex(), strAreaAggregation, ExistingCorrCphsSD, StrObjLtSD, this._adblTD);
            }
            catch (System.OutOfMemoryException ex)
            {
                Console.WriteLine(ex.Message);
            }
            lngTime = pStopwatchLast.ElapsedMilliseconds + lngTimeOverHead;


            StrObjLtSD.SetLastObj("Factor", 1);
            Console.WriteLine("d: " + resultcrg.d
                + "            Type: " + resultcrg.dblCostExactType
                + "            Compactness: " + resultcrg.dblCostExactComp);

            //int intExploredRegionAll = CRegion._intStaticGID - CRegion._intStartStaticGIDLast;  //we don't need to +1 because +1 is already included in _intStaticGID
            double dblConsumedMemoryInMB = CHelpFunc.GetConsumedMemoryInMB(false);

            StrObjLtSD.SetLastObj("#Edges", CRegion._intEdgeCount);
            StrObjLtSD.SetLastObj("TimeFirst(ms)", lngTime);
            StrObjLtSD.SetLastObj("TimeLast(ms)", lngTime);
            StrObjLtSD.SetLastObj("Time(ms)", lngTime);
            StrObjLtSD.SetLastObj("Memory(MB)", CHelpFunc.GetConsumedMemoryInMB(false, lngStartMemory));

            Console.WriteLine("We have visited " + 
                CRegion._intNodeCount + " Nodes and " + CRegion._intEdgeCount + " Edges.");

            return resultcrg;
        }

        private CRegion Compute(CRegion lscrg, CRegion sscrg, int intFinalTypeIndex, string strAreaAggregation,
            SortedDictionary<CCorrCphs, CCorrCphs> ExistingCorrCphsSD, CStrObjLtSD StrObjLtSD, double[,] padblTD)
        {
            CRegion._intNodeCount = 1;
            CRegion._intEdgeCount = 0;
            var currentCrg = lscrg;

            //after an aggregation, we whould have the largest compactness
            //it's urgent to remove the smallest one
            while (currentCrg.GetCphCount() > 1)
            {
                var smallestcph = currentCrg.GetSmallestCph();
                //var smallestneighborcph = new CPatch();
                //smallestneighborcph.dblArea = lscrg.dblArea;  //just an initial value
                //CCorrCphs unitingCorrCphs = null;
                //foreach (var pCorrCphs in currentCrg.AdjCorrCphsSD.Keys)
                //{
                //    var neighborcph = CRegion.TryGetNeighbor(smallestcph, pCorrCphs);
                //    if (neighborcph != null && neighborcph.dblArea < smallestneighborcph.dblArea)
                //    {
                //        smallestneighborcph = neighborcph;
                //        unitingCorrCphs = pCorrCphs;
                //    }
                //}

                CCphRecord pcphRecord = null;
                if (CConstants.strShapeConstraint == "MaximizeMinComp_EdgeNumber" || CConstants.strShapeConstraint == "MaximizeMinComp_Combine" ||
                    CConstants.strShapeConstraint == "MaximizeAvgComp_EdgeNumber" || CConstants.strShapeConstraint == "MaximizeAvgComp_Combine")
                {
                    pcphRecord = GetNeighborCphByCompactness(currentCrg, smallestcph);
                }
                else if (CConstants.strShapeConstraint == "MinimizeInteriorBoundaries")
                {
                    pcphRecord = GetNeighborCphByLength(currentCrg, smallestcph);
                }

                var neighborcph = pcphRecord.Cph;
                var unitingCorrCphs = pcphRecord.CorrCphs;
                var unitedcph = smallestcph.Unite(neighborcph, unitingCorrCphs.dblSharedSegLength);
                CPatch activecph = neighborcph;
                CPatch passivecph = smallestcph;                
                if (padblTD[currentCrg.GetCphTypeIndex(smallestcph), intFinalTypeIndex] <
                    padblTD[currentCrg.GetCphTypeIndex(neighborcph), intFinalTypeIndex])
                {
                    activecph = smallestcph;
                    passivecph = neighborcph;
                }


                int intfrTypeIndex = currentCrg.GetCphTypeIndex(activecph);
                int inttoTypeIndex = currentCrg.GetCphTypeIndex(passivecph);

                var newAdjCorrCphsSD = CRegion.ComputeNewAdjCorrCphsSDAndUpdateExistingCorrCphsSD
                    (currentCrg.AdjCorrCphsSD, unitingCorrCphs, unitedcph, ExistingCorrCphsSD);
                var newcrg = currentCrg.GenerateCrgChildAndComputeCost(lscrg, newAdjCorrCphsSD,
             activecph, passivecph, unitedcph, unitingCorrCphs, intfrTypeIndex, inttoTypeIndex, padblTD);

                newcrg.d = newcrg.dblCostExact;

                CRegion._intNodeCount++;
                CRegion._intEdgeCount++;
                currentCrg = newcrg;
            }

            RecordResultForCrg(StrObjLtSD, lscrg, currentCrg, intFinalTypeIndex);



            return currentCrg;
        }


        private CCphRecord GetNeighborCphByCompactness(CRegion crg, CPatch cph)
        {
            double dblSumComp = 0;  //the sum compactness of neighbours
            var CphRecordsEb = crg.GetNeighborCphRecords(cph);
            foreach (var cphrecord in CphRecordsEb)
            {
                dblSumComp += cphrecord.Cph.dblComp;
            }


            double dblMaxSumComp = 0;
            CCphRecord maxCphRecord = null;
            foreach (var cphrecord in CphRecordsEb)
            {
                var unitedcph = cph.Unite(cphrecord.Cph, cphrecord.CorrCphs.dblSharedSegLength);
                double dblNewSumComp = dblSumComp - cphrecord.Cph.dblComp + unitedcph.dblComp;

                if (dblMaxSumComp < dblNewSumComp)
                {
                    dblMaxSumComp = dblNewSumComp;
                    maxCphRecord = cphrecord;
                }
            }

            return maxCphRecord;
        }

        private CCphRecord GetNeighborCphByLength(CRegion crg, CPatch cph)
        {
            var CphRecordsEb = crg.GetNeighborCphRecords(cph);

            double dblMaxSharedLength = 0;
            CCphRecord maxCphRecord = null;
            foreach (var cphrecord in CphRecordsEb)
            {
                if (dblMaxSharedLength < cphrecord.CorrCphs.dblSharedSegLength)
                {
                    maxCphRecord = cphrecord;
                    dblMaxSharedLength = cphrecord.CorrCphs.dblSharedSegLength;
                }
            }

            return maxCphRecord;
        }
    }
}