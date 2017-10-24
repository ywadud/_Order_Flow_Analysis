# _Order_Flow_Analysis

A real-time only tool/indicator I made for NinaTrader 8 back in 2016.

Its an attempt to mimic the idea of order flow analysis. 
The volumeAnalytics file is the code for the indicator. It takes in volume data and sorts it for graphing clusters of high volume price levels for each candlestick bar in the chart. 
It is fairly resourse heavy.
There is some commented code that should give a good view of high/low buy-sell ratio, but there is a bug making it crash. Commented out, the clusters show without issue.

This could probably be much better using SQL databases and making a custom graph instead of an indicator, but the NinjaTrader 8 developer support isn't that great.

Any suggestions would be appreciated.
