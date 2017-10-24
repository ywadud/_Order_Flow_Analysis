#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;

using System.Collections;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
	public class VolumeAnalytics : Indicator
	{
		private decimal ask = 0;
		private decimal bid = 0;
		private decimal buySellRatio;
		private decimal sigRatio = (decimal)(10/7);
		
		private int activeBar = 0;
		private decimal lastOpen = 0;
		private decimal lastClose = 0;
		private decimal lastHigh = 0;
		private decimal lastLow = 0;
		private int lastVol = 0;
		
		private decimal currOpen = 0;
		private decimal currClose = 0;
		private decimal currHigh = 0;
		private decimal currLow = 0;
		private int currVol = 0;
		private int currIndex = 0;
		private decimal ticker = 0;
		
		private int delayCount = 5;
		private int delay = 5;
		
		private List<decimal> priceLevels = new List<decimal>(20);
		private List<int> volumes = new List<int>(20);
		
		private List<int> buyVols = new List<int>(20);
		private List<int> sellVols = new List<int>(20);
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"For candlestick chartstyles only. Analyze volume and identify high volume clusters on each bar, as well as the price level with the highest volume (not necessarily within the cluster). In addition, while in real-time the the buy sell ratios are analyzed for high buy-sell price level for that bar.";
				Name										= "VolumeAnalytics";
				Calculate									= Calculate.OnEachTick;
				IsOverlay									= true;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				IsSuspendedWhileInactive	= true;
				ClusterShade				= Brushes.Yellow;
				POCShade					= Brushes.Orange;
				AddPlot(new Stroke(Brushes.Yellow, 8), PlotStyle.Hash, "ClusterTop");
				AddPlot(new Stroke(Brushes.Yellow, 8), PlotStyle.Hash, "ClusterMid");
				AddPlot(new Stroke(Brushes.Yellow, 8), PlotStyle.Hash, "ClusterBot");
				AddPlot(new Stroke(Brushes.Orange, 8), PlotStyle.Hash, "POC"); 
				
			}
			else if (State == State.DataLoaded)
			{
				ticker = (decimal)TickSize;
			}
		}
		
		protected override void OnBarUpdate()
		{
			if (CurrentBar < activeBar)
				return;
			
			ask = (decimal)GetCurrentAsk();
			bid = (decimal)GetCurrentBid();
			
			if (CurrentBar != activeBar)
			{
				priceLevels.Clear(); 
				volumes.Clear(); 
				
				buyVols.Clear(); 
				sellVols.Clear(); 
				
				activeBar = CurrentBar; 
				
				updateLast();
				
				priceLevels.Add((decimal)Close[0]);
				volumes.Add((int)Volume[0]);
				
				if (lastClose >= ask)
				{
					buyVols.Add(0);
					sellVols.Add(lastVol);
				}
				else if (lastClose <= bid)
				{
					buyVols.Add(lastVol);
					sellVols.Add(0);
				}
			}
			else
			{
				updateCurrent();
				
				checkAndUpdate();
				checkForGap();
				findPOCAndCluster();
				
				updateLast();
			}
		}
		
		private void updateLast()
		{
			lastClose = (decimal)Close[0];
			lastOpen = (decimal)Open[0];
			
			lastHigh = (decimal)High[0];
			lastLow = (decimal)Low[0];
			
			lastVol = (int)Volume[0];
		}
		
		private void updateCurrent()
		{
			currClose = (decimal)Close[0];
			currOpen = (decimal)Open[0];
			
			currHigh = (decimal)High[0];
			currLow = (decimal)Low[0];
			
			currVol = (int)Volume[0];
		}
		
		private void checkAndUpdate()
		{
			
			for(int i = 0; i < priceLevels.Count; i++)
			{
				// Prices will be listed high to low
				// Check where Close[0] is in the list for an update to volume or if it needs to be inserted as a new price point 
				if(currClose > priceLevels[i]) 
				{
					// Insert
					priceLevels.Insert(i, currClose);
					volumes.Insert(i, currVol - lastVol);
					if (lastClose >= ask)
					{
						buyVols.Insert(i, 0);
						sellVols.Insert(i, currVol - lastVol);
					}
					else if (lastClose <= bid)
					{
						buyVols.Insert(i, currVol - lastVol);
						sellVols.Insert(i, 0);
					}
					
					break;
				}
				else if (currClose == priceLevels[i]) 
				{
					// Update
					volumes[i] += (int)currVol - lastVol;
					if (lastClose >= ask)
					{
						sellVols[i] += (int)currVol - lastVol;
					}
					else if (lastClose <= bid)
					{
						buyVols[i] += (int)currVol - lastVol;
					}
					break;
				}
				else if (currClose < priceLevels[i])
				{
					if (priceLevels.Count == (i + 1))
					{
						// Add if you're already at the end of the list
						priceLevels.Add(currClose); 
						volumes.Add(currVol); 
						if (lastClose >= ask)
						{
							buyVols.Add(0);
							sellVols.Add(currVol);
						}
						else if (lastClose <= bid)
						{
							buyVols.Add(currVol);
							sellVols.Add(0);
						}
					}
					else
					{
						// Insert 
						priceLevels.Insert(i + 1, currClose);
						volumes.Insert(i + 1, currVol - lastVol);
						if (lastClose >= ask)
						{
							buyVols.Insert(i + 1, 0);
							sellVols.Insert(i + 1, currVol - lastVol);
						}
						else if (lastClose <= bid)
						{
							buyVols.Insert(i + 1, currVol - lastVol);
							sellVols.Insert(i + 1, 0);
						}
					}
					
					break;
				}
			}
		}
		
		private void checkForGap() 
		{
			for (int i = 0; i < (priceLevels.Count - 1); i++)
			{
				while ((priceLevels[i] - priceLevels[i + 1]) > ticker)
				{
					priceLevels.Insert(i + 1, priceLevels[i] - ticker);
					volumes.Insert(i + 1, 0);
					buyVols.Insert(i + 1, 0);
					sellVols.Insert(i + 1, 0);
				}
			}
		}
		
		private void findPOCAndCluster()
		{
			decimal tempClusterTop = 0;
			decimal highVolPrice = 0;
			int highestVol = 0;
			int clusterVol = 0;
			
			for (int i = 0; i < priceLevels.Count; i++)
			{
				// Find Clusters
				if (priceLevels.Count > 5)
				{
					if ((i+2) < priceLevels.Count)
					{
						if (clusterVol < (volumes[i] + volumes[i + 1] + volumes[i + 2]))
						{
							clusterVol = volumes[i] + volumes[i + 1] + volumes[i + 2];
							tempClusterTop = priceLevels[i];
						}
					}
//					if (i < (priceLevels.Count - 2))
//					{
//						if ((buyVols[i + 1] / sellVols[i]) >= sigRatio && (buyVols[i + 2] / sellVols[i + 1]) >= sigRatio & (buyVols[i + 3] / sellVols[i + 2]) >= sigRatio)
//						{
//							// Draw a blue dot or arrow from buyVols[i + 1] and sellVols[i + 1]) to buyVols[i + 2] / sellVols[i + 2]
//							string strTag = "Bar" + CurrentBar + "Price" + priceLevels[i + 1]; 
//							Draw.Dot(this, strTag, true, 0, (double)(buyVols[i + 1] + sellVols[i + 1])/2, Brushes.Blue, true);
//						}
//						else if ((sellVols[i] / buyVols[i + 1]) >= sigRatio && (sellVols[i + 1] / buyVols[i + 2]) >= sigRatio & (sellVols[i + 2] / buyVols[i + 3]) >= sigRatio)
//						{
//							// Draw a white or purple dot or arrow from buyVols[i + 1] and sellVols[i + 1]) to buyVols[i + 2] / sellVols[i + 2]
//							string strTag = "Bar" + CurrentBar + "Price" + priceLevels[i + 1]; 
//							Draw.Dot(this, strTag, true, 0, (double)(buyVols[i + 1] + sellVols[i + 1])/2, Brushes.Purple, true);
//						}
//					}
				}
//				else if (priceLevels.Count > 3 && i < (priceLevels.Count - 2))
//				{
//					if ((buyVols[i + 1] / sellVols[i]) >= sigRatio && (buyVols[i + 2] / sellVols[i + 1]) >= sigRatio & (buyVols[i + 3] / sellVols[i + 2]) >= sigRatio)
//					{
//						// Draw a blue dot or arrow from buyVols[i + 1] and sellVols[i + 1]) to buyVols[i + 2] / sellVols[i + 2]
//						string strTag = "Bar" + CurrentBar + "Price" + priceLevels[i + 1]; 
//						Draw.Dot(this, strTag, true, 0, (double)(buyVols[i + 1] + sellVols[i + 1])/2, Brushes.Blue, true);
//					}
//					else if ((sellVols[i] / buyVols[i + 1]) >= sigRatio && (sellVols[i + 1] / buyVols[i + 2]) >= sigRatio & (sellVols[i + 2] / buyVols[i + 3]) >= sigRatio)
//					{
//						// Draw a white or purple dot or arrow from buyVols[i + 1] and sellVols[i + 1]) to buyVols[i + 2] / sellVols[i + 2]
//						string strTag = "Bar" + CurrentBar + "Price" + priceLevels[i + 1]; 
//						Draw.Dot(this, strTag, true, 0, (double)(buyVols[i + 1] + sellVols[i + 1])/2, Brushes.Purple, true);
//					}
//				}
				
				// Find POC
				if(highestVol < volumes[i])
				{
					highVolPrice = priceLevels[i];
					highestVol = volumes[i];
				}
				
				// Outputs for debugging
				Print("\n\nPrice: ");
				Print(priceLevels[i]);
				Print("\nVolume:");
				Print(volumes[i]);
				Print("Highest Vol Price: ");
				Print(highVolPrice);
				Print("Highest Vol: ");
				Print(highestVol);
			} 
			
			if (priceLevels.Count > 5)
			{
				Values[0][0] = ((double)tempClusterTop); 
				Values[1][0] = (double)(tempClusterTop - ticker); 
				Values[2][0] = (double)(tempClusterTop - ticker - ticker); 
			}
			
			Values[3][0] = ((double)highVolPrice); 
		}

		#region Properties
		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="ClusterShade", Order=1, GroupName="Parameters")]
		public Brush ClusterShade
		{ get; set; }

		[Browsable(false)]
		public string ClusterShadeSerializable
		{
			get { return Serialize.BrushToString(ClusterShade); }
			set { ClusterShade = Serialize.StringToBrush(value); }
		}			

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="POCShade", Order=2, GroupName="Parameters")]
		public Brush POCShade
		{ get; set; }

		[Browsable(false)]
		public string POCShadeSerializable
		{
			get { return Serialize.BrushToString(POCShade); }
			set { POCShade = Serialize.StringToBrush(value); }
		}			

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> ClusterTop
		{
			get { return Values[0]; }
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> ClusterMid
		{
			get { return Values[1]; }
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> ClusterBot
		{
			get { return Values[2]; }
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> POC
		{
			get { return Values[3]; }
		}
		#endregion

	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private VolumeAnalytics[] cacheVolumeAnalytics;
		public VolumeAnalytics VolumeAnalytics(Brush clusterShade, Brush pOCShade)
		{
			return VolumeAnalytics(Input, clusterShade, pOCShade);
		}

		public VolumeAnalytics VolumeAnalytics(ISeries<double> input, Brush clusterShade, Brush pOCShade)
		{
			if (cacheVolumeAnalytics != null)
				for (int idx = 0; idx < cacheVolumeAnalytics.Length; idx++)
					if (cacheVolumeAnalytics[idx] != null && cacheVolumeAnalytics[idx].ClusterShade == clusterShade && cacheVolumeAnalytics[idx].POCShade == pOCShade && cacheVolumeAnalytics[idx].EqualsInput(input))
						return cacheVolumeAnalytics[idx];
			return CacheIndicator<VolumeAnalytics>(new VolumeAnalytics(){ ClusterShade = clusterShade, POCShade = pOCShade }, input, ref cacheVolumeAnalytics);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.VolumeAnalytics VolumeAnalytics(Brush clusterShade, Brush pOCShade)
		{
			return indicator.VolumeAnalytics(Input, clusterShade, pOCShade);
		}

		public Indicators.VolumeAnalytics VolumeAnalytics(ISeries<double> input , Brush clusterShade, Brush pOCShade)
		{
			return indicator.VolumeAnalytics(input, clusterShade, pOCShade);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.VolumeAnalytics VolumeAnalytics(Brush clusterShade, Brush pOCShade)
		{
			return indicator.VolumeAnalytics(Input, clusterShade, pOCShade);
		}

		public Indicators.VolumeAnalytics VolumeAnalytics(ISeries<double> input , Brush clusterShade, Brush pOCShade)
		{
			return indicator.VolumeAnalytics(input, clusterShade, pOCShade);
		}
	}
}

#endregion
