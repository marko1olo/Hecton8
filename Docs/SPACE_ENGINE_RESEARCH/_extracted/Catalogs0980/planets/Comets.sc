////////////////////////////////////////////////////////////
//                                                        //
//           Catalog of comets for SpaceEngine            //
//                                                        //
// Data from  Minor Planet Center:                        //
// http://www.minorplanetcenter.net/iau/MPCORB.html       //
// Latest revision:  18 March 2013                        //
//                                                        //
////////////////////////////////////////////////////////////

////////////////////////////////////////////////////////////
//                                                        //
//  Comets with error fixes, additional parameters,       //
//  or missed in the MPC catalog                          //
//                                                        //
////////////////////////////////////////////////////////////

Comet "Halley/1P (Halley)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     1
	SlopeParam  4

	Radius         5.500
	Mass           3.681392236e-11 // 2.2e14 kg to Earth mass
	RotationPeriod 52.8

	Orbit
	{
		Epoch            2449400.5
		Period           75.3158906863
		SemiMajorAxis    17.8341442926
		Eccentricity     0.9671429085
		Inclination      162.2626905792
		AscendingNode    58.4200809766
		ArgOfPericenter  111.3324851045
		MeanAnomaly      38.3842644764
	}

	CometTail
	{
		MaxLength   0.5
		Bright		1.0
		GasColor   (0.003 0.009 0.015)
		DustColor  (0.050 0.050 0.050)
	}
}

Comet "Churyumov-Gerasimenko/67P (Churyumov-Gerasimenko)"
{
	ParentBody     "Sol"
	CometType      "P"
	AbsMagn         11.4
	SlopeParam      11
	Radius          2
	Mass            1.673920e-12
	RotationPeriod  12.76137

	Orbit
	{
		Epoch            2456981.5
		Period           6.44
		SemiMajorAxis    3.462817302992186
		Eccentricity     0.6409739314162571
		Inclination      7.040200902346087
		AscendingNode    50.14210951437195
		ArgOfPericenter  12.78560606538363
		MeanAnomaly      319.3033467788339
	}
}

Comet "Hale-Bopp/C1995 O1 (Hale-Bopp)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn    -2
	SlopeParam  4

	Radius          33.000
	Obliquity       375
	EqAscendNode    283
	RotationPeriod  11.78

	Orbit
	{
		Epoch            2450538.093
		PericenterDist   0.915964
		Period           2533.9746898811
		Eccentricity     0.994929
		Inclination      89.5645
		AscendingNode    282.074
		ArgOfPericenter  130.641
		MeanAnomaly      0
	}

	CometTail
	{
		MaxLength   1.0
		Bright      1.0
		GasToDust   0.15
		Particles   2500
		GasColor   (0.003 0.009 0.020)
		DustColor  (0.050 0.050 0.050)
	}
}

Comet "Borrelly 3/19P (Borrelly)"
{
	ParentBody      "Sol"
	CometType       "P"
	Radius          2.400 //8*4*4 km
	Mass            3.346720214e-12
	Obliquity       375
	EqAscendNode    283
	RotationPeriod  10

	Orbit
	{
		Epoch            2454952.5
		Period           6.8522077238
		SemiMajorAxis    3.6075709287
		Eccentricity     0.6245174635
		Inclination      30.3238109876
		AscendingNode    75.4385376235
		ArgOfPericenter  353.3709430440
		MeanAnomaly      40.6585682383
	}

	CometTail
	{
		MaxLength   0.01
		Bright      0.3
		GasToDust   0.00
		Particles   1000
		GasColor   (0.010 0.030 0.050)
		DustColor  (0.050 0.050 0.050)
	}
}

Comet "Ikeya-Zhang/153P (Ikeya-Zhang)"
{
	ParentBody      "Sol"
	CometType       "C"
	Radius          10  // guess
	RotationPeriod  10  // guess

	Orbit
	{
		Epoch             2452560.5
		Period            366.5100771540
		SemiMajorAxis     51.2135811371
		Eccentricity      0.9900975229
		Inclination       28.1198791625
		AscendingNode     93.3702692929
		ArgOfPericenter   34.6731680508
		MeanAnomaly       0.5594094752
	}

	CometTail
	{
		MaxLength   0.1
		Bright      0.7
		GasToDust   0.1
		Particles   2000
		GasColor   (0.010 0.030 0.050)
		DustColor  (0.030 0.030 0.030)
	}
}

Comet "Lovejoy/C2011 W3 (Lovejoy)"
{
	ParentBody  "Sol"
	CometType   "C"
	Radius      0.080

	Orbit
	{
		Epoch           2455911.511809000032
		Period          697.96
		MeanMotion		0.001412157732224157	//degrees/day
		SemiMajorAxis   78.68293963959538
		Eccentricity    0.9999294152687143
		Inclination     134.3558107377023
		AscendingNode   326.3691470244605
		ArgOfPericenter 53.50921241435645
		MeanAnomaly     359.9858617465071
	}

	CometTail
	{
		Bright		1.0
	}
}


Comet "McNaught/C2006 P1 (McNaught)"
{
	ParentBody    "Sol"
	CometType     "C"
	Radius         12 // upper range estimate
	RotationPeriod 21

	Orbit
	{
		Epoch           2454113.2960995	// 12 Jan 2007, 19:06:23 TT
		MeanMotion		1.163294231335977E-6
		PericenterDist  0.1707420
		Eccentricity    1.0000190
		Inclination     77.8349000
		AscendingNode   267.4144000
		ArgOfPericenter 155.9771000
		MeanAnomaly     0.0
	}

	CometTail
	{
		MaxLength   0.5
		Bright		1.0
		GasToDust   0.25
		Particles   2000
		GasColor   (0.002 0.006 0.010)
		DustColor  (0.150 0.150 0.150)
	}
}


Comet "PANSTARRS/C2011 L4 (PANSTARRS)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     5.5
	SlopeParam  4
	Radius      1

	Orbit
	{
		Epoch            2456361.652997927017
		MeanMotion       4.746544341216939E-6	//degrees/day
		PericenterDist   0.3016097991857733
		Eccentricity     1.00008601240538
		Inclination      84.19941668595264
		AscendingNode    65.66544228753845
		ArgOfPericenter  333.6425286345507
		MeanAnomaly      0
	}

	CometTail
	{
		MaxLength   0.2
		Bright      0.4
		GasToDust   0.2
		Particles   2000
		GasColor   (0.015 0.015 0.018)
		DustColor  (0.050 0.050 0.050)
   }
}


Comet "Lemmon/C2012 F6 (Lemmon)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     4 // 10 in MPC database
	SlopeParam  4
	Radius      1

	Orbit
	{
		Epoch            2456376.0138
		MeanMotion		 9.16975218418177E-5	//degrees/day
		PericenterDist   0.731243
		Eccentricity     0.998533
		Inclination      82.6078
		AscendingNode    332.715
		ArgOfPericenter  304.988
		MeanAnomaly      0
	}

	CometTail
	{
		MaxLength  0.1
		Bright     0.1
		GasToDust  0.9
		Particles  750
		GasColor  (0.030 0.090 0.150)
		DustColor (0.023 0.058 0.055)
	}
}


Comet "Siding Spring/C2013 A1 (Siding Spring)"
{
	ParentBody  "Sol"
	CometType  "C"
	AbsMagn     6
	SlopeParam  4
	Radius      2

	Orbit
	{
		Epoch            2456956.0012
		PericenterDist   1.39944
		Eccentricity     1.00042
		Inclination      129.024
		AscendingNode    300.967
		ArgOfPericenter  2.4315
		MeanAnomaly		 0
	}
}


Comet "ISON/C2012 S1 (ISON)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     7
	SlopeParam  3.2
	Radius      20

	Orbit
	{
		Epoch            2456625.263
		PericenterDist   0.012484
		Eccentricity     1.000003917079469
		Inclination      62.0112
		AscendingNode    295.711
		ArgOfPericenter  345.525
		MeanAnomaly      0
	}
}

////////////////////////////////////////////////////////////
//                                                        //
//                   Other comets                         //
//                                                        //
////////////////////////////////////////////////////////////

Comet	"P1996 R2 (Lagerkvist)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11.5
	SlopeParam  4
	Orbit
	{
		Epoch            2455849.8595
		PericenterDist   2.60707
		Eccentricity     0.3109
		Inclination      2.5995
		AscendingNode    40.1329
		ArgOfPericenter  333.562
		MeanAnomaly      0
	}
}

Comet	"P1997 T3 (Lagerkvist-Carsenty)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     13
	SlopeParam  2
	Orbit
	{
		Epoch            2457150.1115
		PericenterDist   4.22697
		Eccentricity     0.364648
		Inclination      4.847
		AscendingNode    63.1538
		ArgOfPericenter  333.921
		MeanAnomaly      0
	}
}

Comet	"P1998 U3 (Jager)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     6.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456731.1361
		PericenterDist   2.15546
		Eccentricity     0.648229
		Inclination      19.0529
		AscendingNode    303.441
		ArgOfPericenter  180.787
		MeanAnomaly      0
	}
}

Comet	"P1998 VS24 (LINEAR)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     13
	SlopeParam  2
	Orbit
	{
		Epoch            2454615.9669
		PericenterDist   3.4291
		Eccentricity     0.242816
		Inclination      5.0229
		AscendingNode    159.122
		ArgOfPericenter  245.055
		MeanAnomaly      0
	}
}

Comet	"P1998 Y2 (Li)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     9.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456692.3979
		PericenterDist   2.52268
		Eccentricity     0.587891
		Inclination      24.3571
		AscendingNode    91.864
		ArgOfPericenter  319.028
		MeanAnomaly      0
	}
}

Comet	"P1999 D1 (Hermann)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     15
	SlopeParam  4
	Orbit
	{
		Epoch            2456279.9072
		PericenterDist   1.64383
		Eccentricity     0.713823
		Inclination      21.348
		AscendingNode    348.788
		ArgOfPericenter  173.96
		MeanAnomaly      0
	}
}

Comet	"P1999 XN120 (Catalina)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     13.5
	SlopeParam  2
	Orbit
	{
		Epoch            2457914.856
		PericenterDist   3.29762
		Eccentricity     0.212448
		Inclination      5.0286
		AscendingNode    285.336
		ArgOfPericenter  161.753
		MeanAnomaly      0
	}
}

Comet	"P2000 QJ46 (LINEAR)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     14
	SlopeParam  2
	Orbit
	{
		Epoch            2457011.832
		PericenterDist   1.88871
		Eccentricity     0.674754
		Inclination      4.4252
		AscendingNode    158.114
		ArgOfPericenter  222.12
		MeanAnomaly      0
	}
}

Comet	"P2000 R2 (LINEAR)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     18
	SlopeParam  4
	Orbit
	{
		Epoch            2456325.9473
		PericenterDist   1.45784
		Eccentricity     0.564658
		Inclination      10.995
		AscendingNode    163.04
		ArgOfPericenter  172.391
		MeanAnomaly      0
	}
}

Comet	"C2002 VQ94 (LINEAR)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     9.5
	SlopeParam  2
	Orbit
	{
		Epoch            2453773.9145
		PericenterDist   6.78319
		Eccentricity     0.964714
		Inclination      70.5999
		AscendingNode    34.9854
		ArgOfPericenter  99.9633
		MeanAnomaly      0
	}
}

Comet	"P2003 S1 (NEAT)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456642.5727
		PericenterDist   2.59113
		Eccentricity     0.430542
		Inclination      5.9569
		AscendingNode    241.042
		ArgOfPericenter  176.056
		MeanAnomaly      0
	}
}

Comet	"P2003 U2 (LINEAR)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     15
	SlopeParam  4
	Orbit
	{
		Epoch            2456472.5179
		PericenterDist   1.69077
		Eccentricity     0.623625
		Inclination      24.6012
		AscendingNode    186.389
		ArgOfPericenter  177.462
		MeanAnomaly      0
	}
}

Comet	"P2004 A1 (LONEOS)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     6.5
	SlopeParam  4
	Orbit
	{
		Epoch            2452978.5088
		PericenterDist   5.45651
		Eccentricity     0.311993
		Inclination      10.5689
		AscendingNode    124.945
		ArgOfPericenter  21.4446
		MeanAnomaly      0
	}
}

Comet	"P2004 FY140 (LINEAR)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     12.5
	SlopeParam  2
	Orbit
	{
		Epoch            2457218.1624
		PericenterDist   4.07262
		Eccentricity     0.171216
		Inclination      2.1325
		AscendingNode    326.836
		ArgOfPericenter  240.516
		MeanAnomaly      0
	}
}

Comet	"P2005 L1 (McNaught)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     9.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456621.3321
		PericenterDist   3.15922
		Eccentricity     0.207551
		Inclination      7.7306
		AscendingNode    138.271
		ArgOfPericenter  149.809
		MeanAnomaly      0
	}
}

Comet	"C2005 L3 (McNaught)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     4
	SlopeParam  4
	Orbit
	{
		Epoch            2454481.3688
		PericenterDist   5.58386
		Eccentricity     1.00013
		Inclination      139.4
		AscendingNode    288.842
		ArgOfPericenter  47.1155
		MeanAnomaly      0
	}
}

Comet	"P2005 RV25 (LONEOS-Christensen)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     9.5
	SlopeParam  4
	Orbit
	{
		Epoch            2457321.5977
		PericenterDist   3.58547
		Eccentricity     0.168512
		Inclination      9.8975
		AscendingNode    246.88
		ArgOfPericenter  191.364
		MeanAnomaly      0
	}
}

Comet	"P2005 SB216 (LONEOS)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     12
	SlopeParam  2
	Orbit
	{
		Epoch            2454145.3352
		PericenterDist   3.79641
		Eccentricity     0.465352
		Inclination      24.0851
		AscendingNode    1.5922
		ArgOfPericenter  83.3495
		MeanAnomaly      0
	}
}

Comet	"P2005 T2 (Christensen)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     14.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456207.619
		PericenterDist   2.20926
		Eccentricity     0.421813
		Inclination      8.3375
		AscendingNode    260.454
		ArgOfPericenter  58.6613
		MeanAnomaly      0
	}
}

Comet	"P2006 F1 (Kowalski)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     8
	SlopeParam  4
	Orbit
	{
		Epoch            2458197.3393
		PericenterDist   4.12192
		Eccentricity     0.117846
		Inclination      21.2781
		AscendingNode    124.756
		ArgOfPericenter  186.188
		MeanAnomaly      0
	}
}

Comet	"P2006 F4 (Spacewatch)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     15
	SlopeParam  4
	Orbit
	{
		Epoch            2456275.5699
		PericenterDist   2.34207
		Eccentricity     0.336724
		Inclination      12.3806
		AscendingNode    184.062
		ArgOfPericenter  31.0348
		MeanAnomaly      0
	}
}

Comet	"P2006 R2 (Christensen)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11
	SlopeParam  4
	Orbit
	{
		Epoch            2457021.0803
		PericenterDist   3.05266
		Eccentricity     0.269528
		Inclination      16.2987
		AscendingNode    139.076
		ArgOfPericenter  189.181
		MeanAnomaly      0
	}
}

Comet	"P2006 S1 (Christensen)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     17.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456368.4748
		PericenterDist   1.3596
		Eccentricity     0.61098
		Inclination      11.8889
		AscendingNode    213.478
		ArgOfPericenter  128.299
		MeanAnomaly      0
	}
}

Comet	"C2006 S3 (LONEOS)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     2
	SlopeParam  4
	Orbit
	{
		Epoch            2456033.6075
		PericenterDist   5.13058
		Eccentricity     1.00375
		Inclination      166.032
		AscendingNode    38.3715
		ArgOfPericenter  140.102
		MeanAnomaly      0
	}
}

Comet	"C2006 W3 (Christensen)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     5
	SlopeParam  4
	Orbit
	{
		Epoch            2455019.4962
		PericenterDist   3.12522
		Eccentricity     1.00042
		Inclination      127.09
		AscendingNode    113.657
		ArgOfPericenter  133.574
		MeanAnomaly      0
	}
}

Comet	"P2007 H1 (McNaught)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     10
	SlopeParam  4
	Orbit
	{
		Epoch            2456629.1968
		PericenterDist   2.28871
		Eccentricity     0.377122
		Inclination      11.8619
		AscendingNode    144.308
		ArgOfPericenter  202.873
		MeanAnomaly      0
	}
}

Comet	"C2007 Q3 (Siding Spring)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     4.5
	SlopeParam  4
	Orbit
	{
		Epoch            2455111.7385
		PericenterDist   2.24863
		Eccentricity     0.999612
		Inclination      65.6913
		AscendingNode    149.328
		ArgOfPericenter  2.0435
		MeanAnomaly      0
	}
}

Comet	"P2007 R1 (Larson)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     8
	SlopeParam  4
	Orbit
	{
		Epoch            2454094.4808
		PericenterDist   4.37358
		Eccentricity     0.277052
		Inclination      7.871
		AscendingNode    181.655
		ArgOfPericenter  175.704
		MeanAnomaly      0
	}
}

Comet	"P2007 T2 (Kowalski)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     18.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456349.1278
		PericenterDist   0.694706
		Eccentricity     0.775114
		Inclination      9.8927
		AscendingNode    3.9429
		ArgOfPericenter  358.65
		MeanAnomaly      0
	}
}

Comet	"P2008 CL94 (Lemmon)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     8
	SlopeParam  4
	Orbit
	{
		Epoch            2453908.2475
		PericenterDist   5.40358
		Eccentricity     0.121312
		Inclination      8.3539
		AscendingNode    33.4352
		ArgOfPericenter  80.8466
		MeanAnomaly      0
	}
}

Comet	"C2008 FK75 (Lemmon-Siding Spring)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     5
	SlopeParam  4
	Orbit
	{
		Epoch            2455194.8274
		PericenterDist   4.51502
		Eccentricity     1.00118
		Inclination      61.1775
		AscendingNode    218.269
		ArgOfPericenter  80.4989
		MeanAnomaly      0
	}
}

Comet	"P2008 J2 (Beshore)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     9
	SlopeParam  4
	Orbit
	{
		Epoch            2456656.311
		PericenterDist   2.3489
		Eccentricity     0.318665
		Inclination      10.3242
		AscendingNode    97.705
		ArgOfPericenter  131.965
		MeanAnomaly      0
	}
}

Comet	"P2008 O2 (McNaught)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     9
	SlopeParam  4
	Orbit
	{
		Epoch            2454941.9059
		PericenterDist   3.81061
		Eccentricity     0.151291
		Inclination      9.5157
		AscendingNode    325.865
		ArgOfPericenter  27.5397
		MeanAnomaly      0
	}
}

Comet	"C2008 S3 (Boattini)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     4
	SlopeParam  4
	Orbit
	{
		Epoch            2455722.1375
		PericenterDist   8.01999
		Eccentricity     1.00172
		Inclination      162.706
		AscendingNode    54.9508
		ArgOfPericenter  40.1058
		MeanAnomaly      0
	}
}

Comet	"C2009 F2 (McNaught)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     6
	SlopeParam  4
	Orbit
	{
		Epoch            2455148.4886
		PericenterDist   5.86804
		Eccentricity     0.982188
		Inclination      59.3715
		AscendingNode    214.046
		ArgOfPericenter  336.213
		MeanAnomaly      0
	}
}

Comet	"C2009 F4 (McNaught)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     3
	SlopeParam  4
	Orbit
	{
		Epoch            2455927.4505
		PericenterDist   5.45533
		Eccentricity     1.00039
		Inclination      79.3486
		AscendingNode    53.5822
		ArgOfPericenter  260.399
		MeanAnomaly      0
	}
}

Comet	"C2009 P1 (Garradd)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     4
	SlopeParam  4
	Orbit
	{
		Epoch            2455919.1202
		PericenterDist   1.55069
		Eccentricity     1.00078
		Inclination      106.17
		AscendingNode    325.998
		ArgOfPericenter  90.7417
		MeanAnomaly      0
	}
}

Comet	"C2009 P2 (Boattini)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     6
	SlopeParam  4
	Orbit
	{
		Epoch            2455241.0758
		PericenterDist   6.54755
		Eccentricity     1.00277
		Inclination      163.459
		AscendingNode    60.4644
		ArgOfPericenter  76.3564
		MeanAnomaly      0
	}
}

Comet	"C2009 S3 (Lemmon)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     6.5
	SlopeParam  4
	Orbit
	{
		Epoch            2455907.836
		PericenterDist   6.47633
		Eccentricity     1.00143
		Inclination      60.383
		AscendingNode    225.133
		ArgOfPericenter  129.857
		MeanAnomaly      0
	}
}

Comet	"P2010 A2 (LINEAR)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     15.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456435.4587
		PericenterDist   2.00399
		Eccentricity     0.125309
		Inclination      5.2559
		AscendingNode    320.243
		ArgOfPericenter  132.916
		MeanAnomaly      0
	}
}

Comet	"C2010 B1 (Cardinal)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     7.5
	SlopeParam  4
	Orbit
	{
		Epoch            2455599.7571
		PericenterDist   2.94358
		Eccentricity     0.999205
		Inclination      101.984
		AscendingNode    277.198
		ArgOfPericenter  211.585
		MeanAnomaly      0
	}
}

Comet	"C2010 G2 (Hill)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     8
	SlopeParam  4
	Orbit
	{
		Epoch            2455532.3756
		PericenterDist   1.98016
		Eccentricity     0.97887
		Inclination      103.762
		AscendingNode    246.788
		ArgOfPericenter  137.381
		MeanAnomaly      0
	}
}

Comet	"P2010 H2 (Vales)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     6
	SlopeParam  4
	Orbit
	{
		Epoch            2455262.8086
		PericenterDist   3.10332
		Eccentricity     0.192696
		Inclination      14.2573
		AscendingNode    64.3099
		ArgOfPericenter  129.683
		MeanAnomaly      0
	}
}

Comet	"P2010 H5 (Scotti)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     13
	SlopeParam  2
	Orbit
	{
		Epoch            2455295.1223
		PericenterDist   6.02029
		Eccentricity     0.155645
		Inclination      14.0854
		AscendingNode    24.8792
		ArgOfPericenter  174.481
		MeanAnomaly      0
	}
}

Comet	"P2010 J5 (McNaught)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     10
	SlopeParam  4
	Orbit
	{
		Epoch            2455136.3014
		PericenterDist   3.74666
		Eccentricity     0.086774
		Inclination      7.3565
		AscendingNode    65.6701
		ArgOfPericenter  149.609
		MeanAnomaly      0
	}
}

Comet	"P2010 JC81 (WISE)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     9
	SlopeParam  4
	Orbit
	{
		Epoch            2455678.0193
		PericenterDist   1.81282
		Eccentricity     0.777303
		Inclination      38.6835
		AscendingNode    30.7811
		ArgOfPericenter  12.6204
		MeanAnomaly      0
	}
}

Comet	"C2010 L3 (Catalina)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     4.5
	SlopeParam  4
	Orbit
	{
		Epoch            2455510.9429
		PericenterDist   9.88194
		Eccentricity     1.00256
		Inclination      102.626
		AscendingNode    38.2746
		ArgOfPericenter  121.755
		MeanAnomaly      0
	}
}

Comet	"C2010 M1 (Gibbs)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     9
	SlopeParam  4
	Orbit
	{
		Epoch            2455965.34
		PericenterDist   2.29869
		Eccentricity     1
		Inclination      78.373
		AscendingNode    82.15
		ArgOfPericenter  265.318
		MeanAnomaly      0
	}
}

Comet	"C2010 R1 (LINEAR)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     6
	SlopeParam  4
	Orbit
	{
		Epoch            2456066.0768
		PericenterDist   5.62101
		Eccentricity     1.00331
		Inclination      156.928
		AscendingNode    343.666
		ArgOfPericenter  114.48
		MeanAnomaly      0
	}
}

Comet	"P2010 R2 (La Sagra)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     13
	SlopeParam  4
	Orbit
	{
		Epoch            2457357.9265
		PericenterDist   2.61785
		Eccentricity     0.153857
		Inclination      21.4201
		AscendingNode    270.652
		ArgOfPericenter  58.8754
		MeanAnomaly      0
	}
}

Comet	"C2010 S1 (LINEAR)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     3.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456432.8225
		PericenterDist   5.89982
		Eccentricity     1.00167
		Inclination      125.336
		AscendingNode    93.431
		ArgOfPericenter  118.618
		MeanAnomaly      0
	}
}

Comet	"P2010 TO20 (LINEAR-Grauer)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     9
	SlopeParam  4
	Orbit
	{
		Epoch            2454865.1723
		PericenterDist   5.33549
		Eccentricity     0.068036
		Inclination      2.5115
		AscendingNode    43.9462
		ArgOfPericenter  267.647
		MeanAnomaly      0
	}
}

Comet	"C2010 U3 (Boattini)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     1
	SlopeParam  4
	Orbit
	{
		Epoch            2458538.4925
		PericenterDist   8.4603
		Eccentricity     1.00284
		Inclination      55.4324
		AscendingNode    43.0323
		ArgOfPericenter  87.9185
		MeanAnomaly      0
	}
}

Comet	"P2010 UH55 (Spacewatch)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11
	SlopeParam  4
	Orbit
	{
		Epoch            2455692.2921
		PericenterDist   2.76976
		Eccentricity     0.575801
		Inclination      8.6628
		AscendingNode    235.257
		ArgOfPericenter  221.734
		MeanAnomaly      0
	}
}

Comet	"P2010 V1 (Ikeya-Murakami)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     8
	SlopeParam  4
	Orbit
	{
		Epoch            2455482.7905
		PericenterDist   1.57485
		Eccentricity     0.486716
		Inclination      9.3822
		AscendingNode    3.7771
		ArgOfPericenter  152.236
		MeanAnomaly      0
	}
}

Comet	"C2011 A3 (Gibbs)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     10
	SlopeParam  4
	Orbit
	{
		Epoch            2455911.5006
		PericenterDist   2.34467
		Eccentricity     0.997311
		Inclination      26.0786
		AscendingNode    124.894
		ArgOfPericenter  141.141
		MeanAnomaly      0
	}
}

Comet	"P2011 C2 (Gibbs)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     9
	SlopeParam  4
	Orbit
	{
		Epoch            2455934.0181
		PericenterDist   5.38838
		Eccentricity     0.269938
		Inclination      10.9039
		AscendingNode    12.2029
		ArgOfPericenter  160.518
		MeanAnomaly      0
	}
}

Comet	"C2011 F1 (LINEAR)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     5
	SlopeParam  4
	Orbit
	{
		Epoch            2456300.5124
		PericenterDist   1.81912
		Eccentricity     1.00006
		Inclination      56.6129
		AscendingNode    85.1151
		ArgOfPericenter  192.552
		MeanAnomaly      0
	}
}

Comet	"P2011 FR143 (Lemmon)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     14
	SlopeParam  2
	Orbit
	{
		Epoch            2455630.8128
		PericenterDist   3.7334
		Eccentricity     0.453375
		Inclination      16.0105
		AscendingNode    191.003
		ArgOfPericenter  349.859
		MeanAnomaly      0
	}
}

Comet	"C2011 J2 (LINEAR)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     6
	SlopeParam  4
	Orbit
	{
		Epoch            2456651.7316
		PericenterDist   3.44364
		Eccentricity     1.00039
		Inclination      122.796
		AscendingNode    163.94
		ArgOfPericenter  85.2737
		MeanAnomaly      0
	}
}

Comet	"P2011 JB15 (Spacewatch-Boattini)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     9
	SlopeParam  4
	Orbit
	{
		Epoch            2455947.1775
		PericenterDist   5.0182
		Eccentricity     0.317606
		Inclination      19.1472
		AscendingNode    153.738
		ArgOfPericenter  110.856
		MeanAnomaly      0
	}
}

Comet	"C2011 KP36 (Spacewatch)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     4.5
	SlopeParam  4
	Orbit
	{
		Epoch            2457536.212
		PericenterDist   4.88128
		Eccentricity     0.872912
		Inclination      18.9811
		AscendingNode    173.459
		ArgOfPericenter  180.618
		MeanAnomaly      0
	}
}

Comet	"P2011 N1 (ASH)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456077.8807
		PericenterDist   2.85628
		Eccentricity     0.545943
		Inclination      35.6904
		AscendingNode    77.6732
		ArgOfPericenter  330.848
		MeanAnomaly      0
	}
}

Comet	"C2011 O1 (LINEAR)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     7
	SlopeParam  4
	Orbit
	{
		Epoch            2455913.9426
		PericenterDist   3.89065
		Eccentricity     0.996342
		Inclination      76.4997
		AscendingNode    89.8185
		ArgOfPericenter  232.379
		MeanAnomaly      0
	}
}

Comet	"P2011 P1 (McNaught)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     9
	SlopeParam  4
	Orbit
	{
		Epoch            2455395.6432
		PericenterDist   5.0194
		Eccentricity     0.330357
		Inclination      5.6558
		AscendingNode    5.0115
		ArgOfPericenter  347.941
		MeanAnomaly      0
	}
}

Comet	"C2011 Q2 (McNaught)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     10
	SlopeParam  4
	Orbit
	{
		Epoch            2455946.3079
		PericenterDist   1.35086
		Eccentricity     1.00046
		Inclination      36.98
		AscendingNode    287.239
		ArgOfPericenter  34.7903
		MeanAnomaly      0
	}
}

Comet	"C2011 R1 (McNaught)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     6.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456220.1169
		PericenterDist   2.07952
		Eccentricity     1.00077
		Inclination      116.195
		AscendingNode    221.409
		ArgOfPericenter  308.859
		MeanAnomaly      0
	}
}

Comet	"P2011 R3 (Novichonok-Gerke)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11
	SlopeParam  4
	Orbit
	{
		Epoch            2456017.9759
		PericenterDist   3.55636
		Eccentricity     0.264494
		Inclination      19.2195
		AscendingNode    190.458
		ArgOfPericenter  224.932
		MeanAnomaly      0
	}
}

Comet	"P2011 S1 (Gibbs)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     8
	SlopeParam  4
	Orbit
	{
		Epoch            2456649.439
		PericenterDist   6.89493
		Eccentricity     0.203146
		Inclination      2.6793
		AscendingNode    218.897
		ArgOfPericenter  193.298
		MeanAnomaly      0
	}
}

Comet	"P2011 U1 (PANSTARRS)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     14.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456099.2735
		PericenterDist   2.35636
		Eccentricity     0.417793
		Inclination      15.2423
		AscendingNode    135.003
		ArgOfPericenter  353.184
		MeanAnomaly      0
	}
}

Comet	"P2011 U2 (Bressi)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     10
	SlopeParam  4
	Orbit
	{
		Epoch            2456031.5916
		PericenterDist   4.83412
		Eccentricity     0.09497
		Inclination      9.7725
		AscendingNode    266.519
		ArgOfPericenter  155.731
		MeanAnomaly      0
	}
}

Comet	"C2011 UF305 (LINEAR)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     9
	SlopeParam  4
	Orbit
	{
		Epoch            2456130.6671
		PericenterDist   2.13827
		Eccentricity     1.00062
		Inclination      93.9705
		AscendingNode    297.436
		ArgOfPericenter  121.998
		MeanAnomaly      0
	}
}

Comet	"P2011 W1 (PANSTARRS)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11.5
	SlopeParam  4
	Orbit
	{
		Epoch            2455949.1845
		PericenterDist   3.31261
		Eccentricity     0.287796
		Inclination      3.7189
		AscendingNode    161.872
		ArgOfPericenter  282.525
		MeanAnomaly      0
	}
}

Comet	"C2011 Y3 (Boattini)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     8
	SlopeParam  4
	Orbit
	{
		Epoch            2455553.3126
		PericenterDist   3.49868
		Eccentricity     0.704217
		Inclination      26.5196
		AscendingNode    84.8039
		ArgOfPericenter  340.68
		MeanAnomaly      0
	}
}

Comet	"C2012 A1 (PANSTARRS)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     6
	SlopeParam  4
	Orbit
	{
		Epoch            2456626.4562
		PericenterDist   7.60385
		Eccentricity     1.00155
		Inclination      120.902
		AscendingNode    277.972
		ArgOfPericenter  191.782
		MeanAnomaly      0
	}
}

Comet	"C2012 A2 (LINEAR)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     8.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456236.6402
		PericenterDist   3.53755
		Eccentricity     0.996446
		Inclination      125.866
		AscendingNode    191.41
		ArgOfPericenter  101.684
		MeanAnomaly      0
	}
}

Comet	"P2012 B1 (PANSTARRS)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     9
	SlopeParam  4
	Orbit
	{
		Epoch            2456496.6096
		PericenterDist   3.82524
		Eccentricity     0.410562
		Inclination      7.6277
		AscendingNode    36.1951
		ArgOfPericenter  162.177
		MeanAnomaly      0
	}
}

Comet	"C2012 B3 (La Sagra)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     10
	SlopeParam  4
	Orbit
	{
		Epoch            2455902.2541
		PericenterDist   3.53683
		Eccentricity     1.00056
		Inclination      106.933
		AscendingNode    253.001
		ArgOfPericenter  50.7315
		MeanAnomaly      0
	}
}

Comet	"C2012 BJ98 (Lemmon)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     12.5
	SlopeParam  4
	Orbit
	{
		Epoch            2455915.9235
		PericenterDist   2.15652
		Eccentricity     0.873957
		Inclination      2.6369
		AscendingNode    124.027
		ArgOfPericenter  72.9682
		MeanAnomaly      0
	}
}

Comet	"C2012 C1 (McNaught)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     7.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456328.0642
		PericenterDist   4.83797
		Eccentricity     0.996497
		Inclination      96.278
		AscendingNode    300.637
		ArgOfPericenter  279.895
		MeanAnomaly      0
	}
}

Comet	"C2012 CH17 (MOSS)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     11
	SlopeParam  4
	Orbit
	{
		Epoch            2455923.7166
		PericenterDist   1.29616
		Eccentricity     0.999853
		Inclination      27.7448
		AscendingNode    125.982
		ArgOfPericenter  137.988
		MeanAnomaly      0
	}
}

Comet	"C2012 E1 (Hill)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     6.5
	SlopeParam  4
	Orbit
	{
		Epoch            2455746.2697
		PericenterDist   7.50253
		Eccentricity     0.997373
		Inclination      122.545
		AscendingNode    286.336
		ArgOfPericenter  48.8827
		MeanAnomaly      0
	}
}

Comet	"C2012 E2 (SWAN)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     10
	SlopeParam  4
	Orbit
	{
		Epoch            2456001.5314
		PericenterDist   0.006952
		Eccentricity     1.005
		Inclination      144.222
		AscendingNode    7.7078
		ArgOfPericenter  83.3435
		MeanAnomaly      0
	}
}

Comet	"P2012 F2 (PANSTARRS)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     12
	SlopeParam  4
	Orbit
	{
		Epoch            2456392.492
		PericenterDist   2.8971
		Eccentricity     0.54225
		Inclination      14.7245
		AscendingNode    227.135
		ArgOfPericenter  33.1875
		MeanAnomaly      0
	}
}

Comet	"C2012 F3 (PANSTARRS)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     6.5
	SlopeParam  4
	Orbit
	{
		Epoch            2457119.6614
		PericenterDist   3.45514
		Eccentricity     1.0003
		Inclination      11.3602
		AscendingNode    164.632
		ArgOfPericenter  104.075
		MeanAnomaly      0
	}
}

Comet	"P2012 F5 (Gibbs)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     12
	SlopeParam  4
	Orbit
	{
		Epoch            2457187.3496
		PericenterDist   2.87926
		Eccentricity     0.041758
		Inclination      9.7381
		AscendingNode    216.863
		ArgOfPericenter  177.714
		MeanAnomaly      0
	}
}

Comet	"C2012 J1 (Catalina)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     8
	SlopeParam  4
	Orbit
	{
		Epoch            2456268.7711
		PericenterDist   3.15874
		Eccentricity     1.00189
		Inclination      34.1839
		AscendingNode    235.218
		ArgOfPericenter  147.279
		MeanAnomaly      0
	}
}

Comet	"C2012 K1 (PANSTARRS)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     4.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456654.1443
		PericenterDist   1.05506
		Eccentricity     1.0003
		Inclination      142.425
		AscendingNode    317.711
		ArgOfPericenter  203.063
		MeanAnomaly      0
	}
}

Comet	"C2012 K5 (LINEAR)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     12
	SlopeParam  4
	Orbit
	{
		Epoch            2456260.1843
		PericenterDist   1.14174
		Eccentricity     0.998509
		Inclination      92.8496
		AscendingNode    279.039
		ArgOfPericenter  139.285
		MeanAnomaly      0
	}
}

Comet	"C2012 K6 (McNaught)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     8.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456434.0117
		PericenterDist   3.35303
		Eccentricity     0.999512
		Inclination      135.221
		AscendingNode    206.898
		ArgOfPericenter  338.836
		MeanAnomaly      0
	}
}

Comet	"C2012 K8 (Lemmon)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     6
	SlopeParam  4
	Orbit
	{
		Epoch            2456645.6008
		PericenterDist   6.46476
		Eccentricity     1.00315
		Inclination      106.108
		AscendingNode    312.798
		ArgOfPericenter  75.8221
		MeanAnomaly      0
	}
}

Comet	"C2012 L1 (LINEAR)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     10.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456286.9123
		PericenterDist   2.26238
		Eccentricity     0.997365
		Inclination      87.222
		AscendingNode    271.765
		ArgOfPericenter  140.288
		MeanAnomaly      0
	}
}

Comet	"C2012 L2 (LINEAR)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     10
	SlopeParam  4
	Orbit
	{
		Epoch            2456421.8258
		PericenterDist   1.50865
		Eccentricity     0.997421
		Inclination      70.9821
		AscendingNode    270.301
		ArgOfPericenter  205.778
		MeanAnomaly      0
	}
}

Comet	"C2012 LP26 (Palomar)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     6.5
	SlopeParam  4
	Orbit
	{
		Epoch            2457008.9144
		PericenterDist   6.53381
		Eccentricity     0.998531
		Inclination      25.3759
		AscendingNode    154.033
		ArgOfPericenter  145.186
		MeanAnomaly      0
	}
}

Comet	"C2012 Q1 (Kowalski)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     4
	SlopeParam  4
	Orbit
	{
		Epoch            2455966.8527
		PericenterDist   9.4823
		Eccentricity     0.637014
		Inclination      45.1847
		AscendingNode    184.436
		ArgOfPericenter  139.242
		MeanAnomaly      0
	}
}

Comet	"C2012 S3 (PANSTARRS)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     10
	SlopeParam  4
	Orbit
	{
		Epoch            2456292.6252
		PericenterDist   2.30803
		Eccentricity     1.00077
		Inclination      112.93
		AscendingNode    121.308
		ArgOfPericenter  183.754
		MeanAnomaly      0
	}
}

Comet	"C2012 S4 (PANSTARRS)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     8.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456471.5227
		PericenterDist   4.34868
		Eccentricity     0.999845
		Inclination      126.541
		AscendingNode    173.104
		ArgOfPericenter  163.615
		MeanAnomaly      0
	}
}

Comet	"P2012 SB6 (Lemmon)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     14
	SlopeParam  4
	Orbit
	{
		Epoch            2456232.5296
		PericenterDist   2.40646
		Eccentricity     0.385048
		Inclination      10.9872
		AscendingNode    9.5462
		ArgOfPericenter  12.8113
		MeanAnomaly      0
	}
}

Comet	"P2012 T2 (PANSTARRS)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     10
	SlopeParam  4
	Orbit
	{
		Epoch            2456402.9196
		PericenterDist   4.81687
		Eccentricity     0.161104
		Inclination      12.5602
		AscendingNode    73.7523
		ArgOfPericenter  310.358
		MeanAnomaly      0
	}
}

Comet	"C2012 T4 (McNaught)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     13.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456211.3627
		PericenterDist   1.95419
		Eccentricity     0.985222
		Inclination      24.0901
		AscendingNode    99.4252
		ArgOfPericenter  219.858
		MeanAnomaly      0
	}
}

Comet	"C2012 T5 (Bressi)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     13
	SlopeParam  4
	Orbit
	{
		Epoch            2456347.5594
		PericenterDist   0.322813
		Eccentricity     1.0004
		Inclination      72.0944
		AscendingNode    230.594
		ArgOfPericenter  318.097
		MeanAnomaly      0
	}
}

Comet	"P2012 TK8 (Tenagra)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     13
	SlopeParam  4
	Orbit
	{
		Epoch            2456423.0104
		PericenterDist   3.09125
		Eccentricity     0.261544
		Inclination      6.2948
		AscendingNode    289.735
		ArgOfPericenter  128.179
		MeanAnomaly      0
	}
}

Comet	"C2012 U1 (PANSTARRS)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     7.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456843.0914
		PericenterDist   5.27078
		Eccentricity     0.997728
		Inclination      56.3666
		AscendingNode    26.9907
		ArgOfPericenter  69.9425
		MeanAnomaly      0
	}
}

Comet	"P2012 U2 (PANSTARRS)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     12.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456265.2865
		PericenterDist   3.62751
		Eccentricity     0.508086
		Inclination      10.5214
		AscendingNode    186.75
		ArgOfPericenter  229.557
		MeanAnomaly      0
	}
}

Comet	"P2012 US27 (Siding Spring)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     13.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456332.05
		PericenterDist   1.82084
		Eccentricity     0.648672
		Inclination      39.2913
		AscendingNode    49.2065
		ArgOfPericenter  1.2661
		MeanAnomaly      0
	}
}

Comet	"C2012 V1 (PANSTARRS)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     12
	SlopeParam  4
	Orbit
	{
		Epoch            2456494.999
		PericenterDist   2.09011
		Eccentricity     0.999837
		Inclination      157.843
		AscendingNode    85.3795
		ArgOfPericenter  123.313
		MeanAnomaly      0
	}
}

Comet	"C2012 V2 (LINEAR)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     9
	SlopeParam  4
	Orbit
	{
		Epoch            2456278.0021
		PericenterDist   1.45476
		Eccentricity     0.997616
		Inclination      67.1855
		AscendingNode    262.164
		ArgOfPericenter  217.324
		MeanAnomaly      0
	}
}

Comet	"P2012 WA34 (Lemmon-PANSTARRS)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     13.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456317.0587
		PericenterDist   3.17333
		Eccentricity     0.339348
		Inclination      6.1195
		AscendingNode    94.5231
		ArgOfPericenter  353.123
		MeanAnomaly      0
	}
}

Comet	"C2012 X1 (LINEAR)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     8
	SlopeParam  4
	Orbit
	{
		Epoch            2456710.1652
		PericenterDist   1.59939
		Eccentricity     0.989362
		Inclination      44.3611
		AscendingNode    113.142
		ArgOfPericenter  132.107
		MeanAnomaly      0
	}
}

Comet	"C2012 X2 (PANSTARRS)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     9
	SlopeParam  4
	Orbit
	{
		Epoch            2456382.6877
		PericenterDist   4.74832
		Eccentricity     0.770695
		Inclination      34.1237
		AscendingNode    271.023
		ArgOfPericenter  215.615
		MeanAnomaly      0
	}
}

Comet	"C2012 Y1 (LINEAR)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     15
	SlopeParam  4
	Orbit
	{
		Epoch            2456310.6963
		PericenterDist   2.01621
		Eccentricity     0.946774
		Inclination      20.9597
		AscendingNode    193.242
		ArgOfPericenter  268.834
		MeanAnomaly      0
	}
}

Comet	"C2012 Y3 (McNaught)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     11
	SlopeParam  4
	Orbit
	{
		Epoch            2455921.3445
		PericenterDist   1.76483
		Eccentricity     0.939894
		Inclination      73.2326
		AscendingNode    122.707
		ArgOfPericenter  235.694
		MeanAnomaly      0
	}
}

Comet	"P2013 A2 (Scotti)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     15.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456330.792
		PericenterDist   2.17745
		Eccentricity     0.455809
		Inclination      3.3664
		AscendingNode    355.84
		ArgOfPericenter  134.507
		MeanAnomaly      0
	}
}

Comet	"P2013 AL76 (Catalina)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     16
	SlopeParam  4
	Orbit
	{
		Epoch            2456274.8226
		PericenterDist   2.04753
		Eccentricity     0.684982
		Inclination      144.861
		AscendingNode    145.945
		ArgOfPericenter  27.2023
		MeanAnomaly      0
	}
}

Comet	"C2013 B2 (Catalina)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     11
	SlopeParam  4
	Orbit
	{
		Epoch            2456475.6704
		PericenterDist   3.73239
		Eccentricity     1.00614
		Inclination      43.4722
		AscendingNode    331.929
		ArgOfPericenter  156.602
		MeanAnomaly      0
	}
}

Comet	"C2013 C2 (Tenagra)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     10
	SlopeParam  4
	Orbit
	{
		Epoch            2457023.4498
		PericenterDist   9.14783
		Eccentricity     0.423491
		Inclination      21.3468
		AscendingNode    247.51
		ArgOfPericenter  308.697
		MeanAnomaly      0
	}
}

Comet	"P2013 CE31 (MOSS)"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11
	SlopeParam  4
	Orbit
	{
		Epoch            2456062.6281
		PericenterDist   4.01418
		Eccentricity     0.17303
		Inclination      4.7237
		AscendingNode    87.1715
		ArgOfPericenter  26.7757
		MeanAnomaly      0
	}
}

Comet	"C2013 D1 (Holvorcem)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     14
	SlopeParam  4
	Orbit
	{
		Epoch            2456396.1283
		PericenterDist   2.45877
		Eccentricity     0.780106
		Inclination      10.0861
		AscendingNode    294.937
		ArgOfPericenter  252.642
		MeanAnomaly      0
	}
}

Comet	"C2013 E1 (McNaught)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     5.5
	SlopeParam  4
	Orbit
	{
		Epoch            2455903.5776
		PericenterDist   7.70311
		Eccentricity     1
		Inclination      158.897
		AscendingNode    133.319
		ArgOfPericenter  290.911
		MeanAnomaly      0
	}
}

Comet	"C2013 E2 (Iwamoto)"
{
	ParentBody "Sol"
	CometType  "C"
	AbsMagn     11.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456357.0102
		PericenterDist   1.39625
		Eccentricity     1
		Inclination      21.9078
		AscendingNode    181.979
		ArgOfPericenter  92.9367
		MeanAnomaly      0
	}
}

Comet	"2P Encke"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11.5
	SlopeParam  6
	Orbit
	{
		Epoch            2456618.2034
		PericenterDist   0.336135
		Eccentricity     0.848205
		Inclination      11.779
		AscendingNode    334.575
		ArgOfPericenter  186.537
		MeanAnomaly      0
	}
}

Comet	"4P Faye"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     8
	SlopeParam  6
	Orbit
	{
		Epoch            2456807.0472
		PericenterDist   1.65531
		Eccentricity     0.568694
		Inclination      9.0494
		AscendingNode    199.289
		ArgOfPericenter  205.023
		MeanAnomaly      0
	}
}

Comet	"9P Tempel"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     5.5
	SlopeParam  10
	Orbit
	{
		Epoch            2455572.8132
		PericenterDist   1.52417
		Eccentricity     0.513873
		Inclination      10.5238
		AscendingNode    68.875
		ArgOfPericenter  179.327
		MeanAnomaly      0
	}
}

Comet	"10P Tempel"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     5
	SlopeParam  10
	Orbit
	{
		Epoch            2457341.72
		PericenterDist   1.42102
		Eccentricity     0.536354
		Inclination      12.0268
		AscendingNode    117.802
		ArgOfPericenter  195.548
		MeanAnomaly      0
	}
}

Comet	"17P Holmes"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     10
	SlopeParam  6
	Orbit
	{
		Epoch            2456743.9609
		PericenterDist   2.05636
		Eccentricity     0.432131
		Inclination      19.0893
		AscendingNode    326.773
		ArgOfPericenter  24.5082
		MeanAnomaly      0
	}
}

Comet	"21P Giacobini-Zinner"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     9
	SlopeParam  6
	Orbit
	{
		Epoch            2455969.1833
		PericenterDist   1.0305
		Eccentricity     0.706897
		Inclination      31.9096
		AscendingNode    195.394
		ArgOfPericenter  172.589
		MeanAnomaly      0
	}
}

Comet	"26P Grigg-Skjellerup"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     12
	SlopeParam  16
	Orbit
	{
		Epoch            2456479.514
		PericenterDist   1.08589
		Eccentricity     0.640113
		Inclination      22.4238
		AscendingNode    211.553
		ArgOfPericenter  2.1498
		MeanAnomaly      0
	}
}

Comet	"29P Schwassmann-Wachmann"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     4
	SlopeParam  4
	Orbit
	{
		Epoch            2458588.4417
		PericenterDist   5.74481
		Eccentricity     0.043016
		Inclination      9.3747
		AscendingNode    312.505
		ArgOfPericenter  50.7295
		MeanAnomaly      0
	}
}

Comet	"31P Schwassmann-Wachmann"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     5
	SlopeParam  8
	Orbit
	{
		Epoch            2455195.6896
		PericenterDist   3.42306
		Eccentricity     0.193905
		Inclination      4.5467
		AscendingNode    114.155
		ArgOfPericenter  18.0187
		MeanAnomaly      0
	}
}

Comet	"32P Comas Sola"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     6.5
	SlopeParam  8
	Orbit
	{
		Epoch            2456948.1316
		PericenterDist   2.00217
		Eccentricity     0.555813
		Inclination      9.9713
		AscendingNode    57.8589
		ArgOfPericenter  53.3094
		MeanAnomaly      0
	}
}

Comet	"36P Whipple"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     8.5
	SlopeParam  6
	Orbit
	{
		Epoch            2455921.6988
		PericenterDist   3.08184
		Eccentricity     0.261678
		Inclination      9.9116
		AscendingNode    182.13
		ArgOfPericenter  201.121
		MeanAnomaly      0
	}
}

Comet	"37P Forbes"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     10.5
	SlopeParam  4.8
	Orbit
	{
		Epoch            2455906.6351
		PericenterDist   1.57825
		Eccentricity     0.540578
		Inclination      8.9553
		AscendingNode    314.927
		ArgOfPericenter  329.644
		MeanAnomaly      0
	}
}

Comet	"46P Wirtanen"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     9
	SlopeParam  6
	Orbit
	{
		Epoch            2456482.8987
		PericenterDist   1.05208
		Eccentricity     0.659288
		Inclination      11.7572
		AscendingNode    82.1633
		ArgOfPericenter  356.343
		MeanAnomaly      0
	}
}

Comet	"47P Ashbrook-Jackson"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     1
	SlopeParam  11.2
	Orbit
	{
		Epoch            2454862.6674
		PericenterDist   2.80984
		Eccentricity     0.317662
		Inclination      13.0432
		AscendingNode    357.003
		ArgOfPericenter  357.921
		MeanAnomaly      0
	}
}

Comet	"49P Arend-Rigaux"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11.3
	SlopeParam  4.4
	Orbit
	{
		Epoch            2455853.6523
		PericenterDist   1.42434
		Eccentricity     0.60045
		Inclination      19.0502
		AscendingNode    118.855
		ArgOfPericenter  332.85
		MeanAnomaly      0
	}
}

Comet	"60P Tsuchinshan"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11.5
	SlopeParam  6
	Orbit
	{
		Epoch            2456061.0736
		PericenterDist   1.61832
		Eccentricity     0.538585
		Inclination      3.6108
		AscendingNode    267.679
		ArgOfPericenter  216.409
		MeanAnomaly      0
	}
}

Comet	"63P Wild"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     10.5
	SlopeParam  6
	Orbit
	{
		Epoch            2456393.2789
		PericenterDist   1.95049
		Eccentricity     0.650676
		Inclination      19.7818
		AscendingNode    358.011
		ArgOfPericenter  169.004
		MeanAnomaly      0
	}
}

Comet	"65P Gunn"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     5
	SlopeParam  6
	Orbit
	{
		Epoch            2455279.4512
		PericenterDist   2.62677
		Eccentricity     0.30048
		Inclination      10.2921
		AscendingNode    67.6534
		ArgOfPericenter  205.642
		MeanAnomaly      0
	}
}

Comet	"74P Smirnova-Chernykh"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     5
	SlopeParam  6
	Orbit
	{
		Epoch            2455039.3385
		PericenterDist   3.54875
		Eccentricity     0.147921
		Inclination      6.651
		AscendingNode    77.0832
		ArgOfPericenter  86.5505
		MeanAnomaly      0
	}
}

Comet	"76P West-Kohoutek-Ikemura"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     8
	SlopeParam  12
	Orbit
	{
		Epoch            2456420.265
		PericenterDist   1.60026
		Eccentricity     0.539005
		Inclination      30.4832
		AscendingNode    84.1235
		ArgOfPericenter  0.0623
		MeanAnomaly      0
	}
}

Comet	"78P Gehrels"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     5.5
	SlopeParam  8
	Orbit
	{
		Epoch            2455939.2128
		PericenterDist   2.0085
		Eccentricity     0.462309
		Inclination      6.2551
		AscendingNode    210.555
		ArgOfPericenter  192.739
		MeanAnomaly      0
	}
}

Comet	"79P du Toit-Hartley"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     16
	SlopeParam  4
	Orbit
	{
		Epoch            2456284.7766
		PericenterDist   1.12391
		Eccentricity     0.618574
		Inclination      3.1454
		AscendingNode    280.642
		ArgOfPericenter  281.673
		MeanAnomaly      0
	}
}

Comet	"84P Giclas"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     9.5
	SlopeParam  8
	Orbit
	{
		Epoch            2456496.7108
		PericenterDist   1.83957
		Eccentricity     0.494353
		Inclination      7.2865
		AscendingNode    112.383
		ArgOfPericenter  276.475
		MeanAnomaly      0
	}
}

Comet	"87P Bus"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     7.2
	SlopeParam  10
	Orbit
	{
		Epoch            2456645.9539
		PericenterDist   2.10221
		Eccentricity     0.388846
		Inclination      2.6004
		AscendingNode    181.907
		ArgOfPericenter  24.6677
		MeanAnomaly      0
	}
}

Comet	"88P Howell"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11
	SlopeParam  6
	Orbit
	{
		Epoch            2457119.2251
		PericenterDist   1.36155
		Eccentricity     0.562178
		Inclination      4.3825
		AscendingNode    56.7406
		ArgOfPericenter  235.841
		MeanAnomaly      0
	}
}

Comet	"91P Russell"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     7.5
	SlopeParam  6
	Orbit
	{
		Epoch            2456352.6698
		PericenterDist   2.61681
		Eccentricity     0.329045
		Inclination      14.0757
		AscendingNode    247.871
		ArgOfPericenter  354.648
		MeanAnomaly      0
	}
}

Comet	"97P Metcalf-Brewington"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     5.5
	SlopeParam  6
	Orbit
	{
		Epoch            2455551.3472
		PericenterDist   2.59761
		Eccentricity     0.458458
		Inclination      17.8842
		AscendingNode    185.203
		ArgOfPericenter  228.223
		MeanAnomaly      0
	}
}

Comet	"98P Takamizawa"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     9
	SlopeParam  8
	Orbit
	{
		Epoch            2456266.8971
		PericenterDist   1.67349
		Eccentricity     0.56062
		Inclination      10.5439
		AscendingNode    114.743
		ArgOfPericenter  157.898
		MeanAnomaly      0
	}
}

Comet	"102P Shoemaker"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     6.5
	SlopeParam  8
	Orbit
	{
		Epoch            2456262.5266
		PericenterDist   1.96844
		Eccentricity     0.472991
		Inclination      26.246
		AscendingNode    339.857
		ArgOfPericenter  18.7794
		MeanAnomaly      0
	}
}

Comet	"110P Hartley"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     1
	SlopeParam  12
	Orbit
	{
		Epoch            2457009.1198
		PericenterDist   2.47682
		Eccentricity     0.314156
		Inclination      11.6935
		AscendingNode    287.712
		ArgOfPericenter  167.674
		MeanAnomaly      0
	}
}

Comet	"111P Helin-Roman-Crockett"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     5
	SlopeParam  8
	Orbit
	{
		Epoch            2456322.3862
		PericenterDist   3.70424
		Eccentricity     0.10901
		Inclination      4.2292
		AscendingNode    89.795
		ArgOfPericenter  3.2665
		MeanAnomaly      0
	}
}

Comet	"112P Urata-Niijima"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     14
	SlopeParam  6
	Orbit
	{
		Epoch            2456467.8125
		PericenterDist   1.45532
		Eccentricity     0.588048
		Inclination      24.2036
		AscendingNode    31.9273
		ArgOfPericenter  21.4502
		MeanAnomaly      0
	}
}

Comet	"114P Wiseman-Skiff"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11.5
	SlopeParam  6
	Orbit
	{
		Epoch            2456426.4098
		PericenterDist   1.57485
		Eccentricity     0.555538
		Inclination      18.2839
		AscendingNode    271.055
		ArgOfPericenter  172.853
		MeanAnomaly      0
	}
}

Comet	"116P Wild"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     2.5
	SlopeParam  10
	Orbit
	{
		Epoch            2457400.1018
		PericenterDist   2.1828
		Eccentricity     0.372741
		Inclination      3.6105
		AscendingNode    21.0511
		ArgOfPericenter  173.579
		MeanAnomaly      0
	}
}

Comet	"117P Helin-Roman-Alu"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     2.5
	SlopeParam  8
	Orbit
	{
		Epoch            2456744.0649
		PericenterDist   3.05579
		Eccentricity     0.253745
		Inclination      8.6988
		AscendingNode    58.8993
		ArgOfPericenter  222.766
		MeanAnomaly      0
	}
}

Comet	"119P Parker-Hartley"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     3.5
	SlopeParam  8
	Orbit
	{
		Epoch            2456749.9334
		PericenterDist   3.02696
		Eccentricity     0.292187
		Inclination      5.1961
		AscendingNode    244.101
		ArgOfPericenter  181.261
		MeanAnomaly      0
	}
}

Comet	"120P Mueller"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     12
	SlopeParam  4
	Orbit
	{
		Epoch            2456345.9286
		PericenterDist   2.72904
		Eccentricity     0.339003
		Inclination      8.7966
		AscendingNode    4.4496
		ArgOfPericenter  30.1138
		MeanAnomaly      0
	}
}

Comet	"121P Shoemaker-Holt"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     6.5
	SlopeParam  8
	Orbit
	{
		Epoch            2456274.2153
		PericenterDist   3.75306
		Eccentricity     0.190301
		Inclination      20.1468
		AscendingNode    94.2323
		ArgOfPericenter  13.1855
		MeanAnomaly      0
	}
}

Comet	"124P Mrkos"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     13.5
	SlopeParam  2.8
	Orbit
	{
		Epoch            2456757.0904
		PericenterDist   1.64488
		Eccentricity     0.504069
		Inclination      31.5318
		AscendingNode    0.466
		ArgOfPericenter  183.677
		MeanAnomaly      0
	}
}

Comet	"125P Spacewatch"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     13
	SlopeParam  6
	Orbit
	{
		Epoch            2456340.4787
		PericenterDist   1.52546
		Eccentricity     0.512338
		Inclination      9.9858
		AscendingNode    153.188
		ArgOfPericenter  87.2265
		MeanAnomaly      0
	}
}

Comet	"128P-B Shoemaker-Holt"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     8.5
	SlopeParam  4
	Orbit
	{
		Epoch            2457763.0772
		PericenterDist   3.05462
		Eccentricity     0.322078
		Inclination      4.3641
		AscendingNode    214.36
		ArgOfPericenter  210.43
		MeanAnomaly      0
	}
}

Comet	"129P Shoemaker-Levy"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11
	SlopeParam  4
	Orbit
	{
		Epoch            2456695.2689
		PericenterDist   3.91334
		Eccentricity     0.093115
		Inclination      3.4322
		AscendingNode    185.399
		ArgOfPericenter  308.495
		MeanAnomaly      0
	}
}

Comet	"131P Mueller"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11
	SlopeParam  4
	Orbit
	{
		Epoch            2455933.0734
		PericenterDist   2.41657
		Eccentricity     0.343329
		Inclination      7.3548
		AscendingNode    214.202
		ArgOfPericenter  179.303
		MeanAnomaly      0
	}
}

Comet	"133P Elst-Pizarro"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     15.4
	SlopeParam  2
	Orbit
	{
		Epoch            2456332.4574
		PericenterDist   2.64997
		Eccentricity     0.161536
		Inclination      1.3868
		AscendingNode    160.149
		ArgOfPericenter  132.157
		MeanAnomaly      0
	}
}

Comet	"134P Kowal-Vavrova"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456798.9354
		PericenterDist   2.57235
		Eccentricity     0.587093
		Inclination      4.3486
		AscendingNode    202.131
		ArgOfPericenter  18.5329
		MeanAnomaly      0
	}
}

Comet	"136P Mueller"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11
	SlopeParam  4
	Orbit
	{
		Epoch            2457539.5642
		PericenterDist   2.97331
		Eccentricity     0.292005
		Inclination      9.4159
		AscendingNode    137.492
		ArgOfPericenter  225.302
		MeanAnomaly      0
	}
}

Comet	"138P Shoemaker-Levy"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     15
	SlopeParam  6
	Orbit
	{
		Epoch            2456090.1485
		PericenterDist   1.70034
		Eccentricity     0.530526
		Inclination      10.0855
		AscendingNode    309.398
		ArgOfPericenter  95.593
		MeanAnomaly      0
	}
}

Comet	"139P Vaisala-Oterma"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     9.5
	SlopeParam  4
	Orbit
	{
		Epoch            2458096.8669
		PericenterDist   3.40619
		Eccentricity     0.247439
		Inclination      2.3335
		AscendingNode    242.273
		ArgOfPericenter  166.137
		MeanAnomaly      0
	}
}

Comet	"143P Kowal-Mrkos"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     13.5
	SlopeParam  2
	Orbit
	{
		Epoch            2454993.38
		PericenterDist   2.54234
		Eccentricity     0.408577
		Inclination      4.6894
		AscendingNode    245.326
		ArgOfPericenter  320.814
		MeanAnomaly      0
	}
}

Comet	"152P Helin-Lawrence"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456117.5137
		PericenterDist   3.11625
		Eccentricity     0.307158
		Inclination      9.868
		AscendingNode    91.9101
		ArgOfPericenter  163.756
		MeanAnomaly      0
	}
}

Comet	"154P Brewington"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     2.5
	SlopeParam  12
	Orbit
	{
		Epoch            2456638.7113
		PericenterDist   1.60797
		Eccentricity     0.670615
		Inclination      17.8311
		AscendingNode    343.498
		ArgOfPericenter  49.0159
		MeanAnomaly      0
	}
}

Comet	"156P Russell-LINEAR"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     15.5
	SlopeParam  2
	Orbit
	{
		Epoch            2456764.005
		PericenterDist   1.58506
		Eccentricity     0.559176
		Inclination      20.7788
		AscendingNode    38.9917
		ArgOfPericenter  357.772
		MeanAnomaly      0
	}
}

Comet	"158P Kowal-LINEAR"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     9
	SlopeParam  4
	Orbit
	{
		Epoch            2456202.6929
		PericenterDist   4.57658
		Eccentricity     0.030832
		Inclination      7.907
		AscendingNode    137.299
		ArgOfPericenter  233.336
		MeanAnomaly      0
	}
}

Comet	"160P LINEAR"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     15.5
	SlopeParam  2
	Orbit
	{
		Epoch            2455914.0878
		PericenterDist   2.06665
		Eccentricity     0.479344
		Inclination      17.2745
		AscendingNode    336.991
		ArgOfPericenter  18.2317
		MeanAnomaly      0
	}
}

Comet	"162P Siding Spring"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     12
	SlopeParam  4
	Orbit
	{
		Epoch            2457215.1607
		PericenterDist   1.23577
		Eccentricity     0.595593
		Inclination      27.8063
		AscendingNode    31.2364
		ArgOfPericenter  356.406
		MeanAnomaly      0
	}
}

Comet	"164P Christensen"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11
	SlopeParam  4
	Orbit
	{
		Epoch            2455714.8639
		PericenterDist   1.6771
		Eccentricity     0.541121
		Inclination      16.2611
		AscendingNode    88.3133
		ArgOfPericenter  325.935
		MeanAnomaly      0
	}
}

Comet	"166P NEAT"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     5.5
	SlopeParam  4
	Orbit
	{
		Epoch            2452419.5067
		PericenterDist   8.58937
		Eccentricity     0.382124
		Inclination      15.3605
		AscendingNode    64.3652
		ArgOfPericenter  322.268
		MeanAnomaly      0
	}
}

Comet	"167P CINEOS"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     9.5
	SlopeParam  2
	Orbit
	{
		Epoch            2452027.7819
		PericenterDist   11.829
		Eccentricity     0.270705
		Inclination      19.1022
		AscendingNode    295.889
		ArgOfPericenter  344.362
		MeanAnomaly      0
	}
}

Comet	"168P Hergenrother"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     15.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456202.4606
		PericenterDist   1.41484
		Eccentricity     0.609562
		Inclination      21.9281
		AscendingNode    356.457
		ArgOfPericenter  13.9504
		MeanAnomaly      0
	}
}

Comet	"169P NEAT"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     16
	SlopeParam  2
	Orbit
	{
		Epoch            2456703.7448
		PericenterDist   0.607801
		Eccentricity     0.766828
		Inclination      11.2915
		AscendingNode    176.131
		ArgOfPericenter  218.053
		MeanAnomaly      0
	}
}

Comet	"171P Spahr"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     13.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456048.0523
		PericenterDist   1.76497
		Eccentricity     0.503115
		Inclination      21.9495
		AscendingNode    101.717
		ArgOfPericenter  347.107
		MeanAnomaly      0
	}
}

Comet	"173P Mueller"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     7.5
	SlopeParam  4
	Orbit
	{
		Epoch            2454603.2263
		PericenterDist   4.19971
		Eccentricity     0.26189
		Inclination      16.5114
		AscendingNode    100.491
		ArgOfPericenter  29.3848
		MeanAnomaly      0
	}
}

Comet	"174P Echeclus"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     9.4
	SlopeParam  2
	Orbit
	{
		Epoch            2457135.5544
		PericenterDist   5.81567
		Eccentricity     0.456404
		Inclination      4.3417
		AscendingNode    173.369
		ArgOfPericenter  162.963
		MeanAnomaly      0
	}
}

Comet	"175P Hergenrother"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     14
	SlopeParam  4
	Orbit
	{
		Epoch            2456436.0938
		PericenterDist   1.94626
		Eccentricity     0.432095
		Inclination      6.078
		AscendingNode    123.591
		ArgOfPericenter  55.9828
		MeanAnomaly      0
	}
}

Comet	"176P LINEAR"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     15.1
	SlopeParam  2
	Orbit
	{
		Epoch            2455741.5683
		PericenterDist   2.57478
		Eccentricity     0.193552
		Inclination      0.235
		AscendingNode    345.977
		ArgOfPericenter  35.6209
		MeanAnomaly      0
	}
}

Comet	"178P Hug-Bell"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     13.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456496.5464
		PericenterDist   1.93372
		Eccentricity     0.472967
		Inclination      10.9755
		AscendingNode    103.576
		ArgOfPericenter  296.956
		MeanAnomaly      0
	}
}

Comet	"181P Shoemaker-Levy"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456818.8132
		PericenterDist   1.12323
		Eccentricity     0.707495
		Inclination      16.9856
		AscendingNode    37.692
		ArgOfPericenter  333.788
		MeanAnomaly      0
	}
}

Comet	"183P Korlevic-Juric"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     12.5
	SlopeParam  2
	Orbit
	{
		Epoch            2458067.5567
		PericenterDist   3.884
		Eccentricity     0.135435
		Inclination      18.7435
		AscendingNode    5.8349
		ArgOfPericenter  160.781
		MeanAnomaly      0
	}
}

Comet	"184P Lovas"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     13.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456501.9717
		PericenterDist   1.39404
		Eccentricity     0.604309
		Inclination      1.5515
		AscendingNode    277.731
		ArgOfPericenter  78.0741
		MeanAnomaly      0
	}
}

Comet	"185P Petriew"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     15
	SlopeParam  4
	Orbit
	{
		Epoch            2455909.0679
		PericenterDist   0.931841
		Eccentricity     0.699385
		Inclination      14.0076
		AscendingNode    214.089
		ArgOfPericenter  181.939
		MeanAnomaly      0
	}
}

Comet	"186P Garradd"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     7.5
	SlopeParam  4
	Orbit
	{
		Epoch            2454609.9763
		PericenterDist   4.33815
		Eccentricity     0.124655
		Inclination      28.5016
		AscendingNode    327.401
		ArgOfPericenter  287.448
		MeanAnomaly      0
	}
}

Comet	"187P LINEAR"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     9
	SlopeParam  4
	Orbit
	{
		Epoch            2454761.5752
		PericenterDist   3.78784
		Eccentricity     0.164929
		Inclination      13.6532
		AscendingNode    110.92
		ArgOfPericenter  137.511
		MeanAnomaly      0
	}
}

Comet	"197P LINEAR"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     16.5
	SlopeParam  2
	Orbit
	{
		Epoch            2456376.353
		PericenterDist   1.06146
		Eccentricity     0.629756
		Inclination      25.5424
		AscendingNode    66.39
		ArgOfPericenter  188.741
		MeanAnomaly      0
	}
}

Comet	"198P ODAS"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     12.5
	SlopeParam  4
	Orbit
	{
		Epoch            2455973.2685
		PericenterDist   1.99704
		Eccentricity     0.444514
		Inclination      1.3414
		AscendingNode    358.441
		ArgOfPericenter  69.1384
		MeanAnomaly      0
	}
}

Comet	"200P Larsen"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     9
	SlopeParam  4
	Orbit
	{
		Epoch            2454459.86
		PericenterDist   3.28627
		Eccentricity     0.332358
		Inclination      12.1145
		AscendingNode    234.775
		ArgOfPericenter  134.182
		MeanAnomaly      0
	}
}

Comet	"203P Korlevic"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     14.5
	SlopeParam  2
	Orbit
	{
		Epoch            2455237.3965
		PericenterDist   3.18459
		Eccentricity     0.316194
		Inclination      2.9745
		AscendingNode    290.448
		ArgOfPericenter  154.926
		MeanAnomaly      0
	}
}

Comet	"213P Van Ness"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     10.5
	SlopeParam  4
	Orbit
	{
		Epoch            2455730.1488
		PericenterDist   2.11305
		Eccentricity     0.382937
		Inclination      10.2899
		AscendingNode    312.149
		ArgOfPericenter  3.7588
		MeanAnomaly      0
	}
}

Comet	"215P NEAT"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11
	SlopeParam  4
	Orbit
	{
		Epoch            2455173.0377
		PericenterDist   3.26533
		Eccentricity     0.214242
		Inclination      12.8892
		AscendingNode    74.8727
		ArgOfPericenter  231.954
		MeanAnomaly      0
	}
}

Comet	"219P LINEAR"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11
	SlopeParam  4
	Orbit
	{
		Epoch            2455259.745
		PericenterDist   2.36488
		Eccentricity     0.352051
		Inclination      11.5291
		AscendingNode    231.029
		ArgOfPericenter  107.561
		MeanAnomaly      0
	}
}

Comet	"228P LINEAR"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     14.5
	SlopeParam  2
	Orbit
	{
		Epoch            2455554.9933
		PericenterDist   3.43048
		Eccentricity     0.178025
		Inclination      7.9132
		AscendingNode    31.0683
		ArgOfPericenter  114.881
		MeanAnomaly      0
	}
}

Comet	"231P LINEAR-NEAT"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     14.5
	SlopeParam  2
	Orbit
	{
		Epoch            2455697.8123
		PericenterDist   3.03119
		Eccentricity     0.246998
		Inclination      12.3278
		AscendingNode    133.07
		ArgOfPericenter  42.3846
		MeanAnomaly      0
	}
}

Comet	"237P LINEAR"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     14.5
	SlopeParam  2
	Orbit
	{
		Epoch            2455224.4774
		PericenterDist   2.10132
		Eccentricity     0.41298
		Inclination      16.7798
		AscendingNode    251.374
		ArgOfPericenter  18.8783
		MeanAnomaly      0
	}
}

Comet	"242P Spahr"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     8
	SlopeParam  4
	Orbit
	{
		Epoch            2456021.0025
		PericenterDist   3.98019
		Eccentricity     0.277619
		Inclination      32.4824
		AscendingNode    180.724
		ArgOfPericenter  247.728
		MeanAnomaly      0
	}
}

Comet	"244P Scotti"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     9
	SlopeParam  4
	Orbit
	{
		Epoch            2455947.2035
		PericenterDist   3.91882
		Eccentricity     0.198993
		Inclination      2.2595
		AscendingNode    354.133
		ArgOfPericenter  92.685
		MeanAnomaly      0
	}
}

Comet	"246P NEAT"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     10.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456321.1822
		PericenterDist   2.87975
		Eccentricity     0.285061
		Inclination      15.9718
		AscendingNode    78.7804
		ArgOfPericenter  176.183
		MeanAnomaly      0
	}
}

Comet	"256P LINEAR"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     14
	SlopeParam  2
	Orbit
	{
		Epoch            2456368.9055
		PericenterDist   2.68994
		Eccentricity     0.418791
		Inclination      27.637
		AscendingNode    81.446
		ArgOfPericenter  124.12
		MeanAnomaly      0
	}
}

Comet	"257P Catalina"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456447.9294
		PericenterDist   2.12904
		Eccentricity     0.432849
		Inclination      20.2447
		AscendingNode    207.869
		ArgOfPericenter  117.811
		MeanAnomaly      0
	}
}

Comet	"259P Garradd"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     15.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456317.9334
		PericenterDist   1.79769
		Eccentricity     0.340939
		Inclination      15.8988
		AscendingNode    51.9607
		ArgOfPericenter  256.559
		MeanAnomaly      0
	}
}

Comet	"260P McNaught"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     13.5
	SlopeParam  4
	Orbit
	{
		Epoch            2455908.024
		PericenterDist   1.49697
		Eccentricity     0.593648
		Inclination      15.7349
		AscendingNode    351.944
		ArgOfPericenter  15.7047
		MeanAnomaly      0
	}
}

Comet	"261P Larson"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     14
	SlopeParam  4
	Orbit
	{
		Epoch            2455924.7152
		PericenterDist   2.18696
		Eccentricity     0.389878
		Inclination      6.3255
		AscendingNode    298.471
		ArgOfPericenter  58.8564
		MeanAnomaly      0
	}
}

Comet	"262P McNaught-Russell"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     13.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456265.9732
		PericenterDist   1.27986
		Eccentricity     0.815411
		Inclination      29.0777
		AscendingNode    218.01
		ArgOfPericenter  171.19
		MeanAnomaly      0
	}
}

Comet	"265P LINEAR"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     14.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456089.212
		PericenterDist   1.49841
		Eccentricity     0.646956
		Inclination      14.6922
		AscendingNode    344.716
		ArgOfPericenter  32.8395
		MeanAnomaly      0
	}
}

Comet	"266P Christensen"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     12.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456293.359
		PericenterDist   2.32764
		Eccentricity     0.34097
		Inclination      3.4287
		AscendingNode    5.0872
		ArgOfPericenter  98.0497
		MeanAnomaly      0
	}
}

Comet	"269P Jedicke"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     10
	SlopeParam  4
	Orbit
	{
		Epoch            2456978.1028
		PericenterDist   4.07807
		Eccentricity     0.438679
		Inclination      6.617
		AscendingNode    248.766
		ArgOfPericenter  223.549
		MeanAnomaly      0
	}
}

Comet	"270P Gehrels"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     8
	SlopeParam  4
	Orbit
	{
		Epoch            2456482.0042
		PericenterDist   3.60149
		Eccentricity     0.473523
		Inclination      2.8573
		AscendingNode    225.302
		ArgOfPericenter  211.006
		MeanAnomaly      0
	}
}

Comet	"271P van Houten-Lemmon"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11
	SlopeParam  4
	Orbit
	{
		Epoch            2456479.1928
		PericenterDist   4.24965
		Eccentricity     0.390986
		Inclination      6.8553
		AscendingNode    9.5884
		ArgOfPericenter  35.1108
		MeanAnomaly      0
	}
}

Comet	"272P NEAT"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     16
	SlopeParam  2
	Orbit
	{
		Epoch            2456350.6786
		PericenterDist   2.4167
		Eccentricity     0.45573
		Inclination      18.1017
		AscendingNode    109.503
		ArgOfPericenter  27.8918
		MeanAnomaly      0
	}
}

Comet	"273P Pons-Gambart"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456281.1721
		PericenterDist   0.81022
		Eccentricity     0.97531
		Inclination      136.397
		AscendingNode    320.427
		ArgOfPericenter  20.1921
		MeanAnomaly      0
	}
}

Comet	"274P Tombaugh-Tenagra"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     13
	SlopeParam  4
	Orbit
	{
		Epoch            2456346.8473
		PericenterDist   2.44187
		Eccentricity     0.440062
		Inclination      15.8384
		AscendingNode    81.3628
		ArgOfPericenter  38.458
		MeanAnomaly      0
	}
}

Comet	"275P Hermann"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     15
	SlopeParam  4
	Orbit
	{
		Epoch            2456288.7851
		PericenterDist   1.64377
		Eccentricity     0.714168
		Inclination      21.3424
		AscendingNode    348.755
		ArgOfPericenter  173.987
		MeanAnomaly      0
	}
}

Comet	"276P Vorobjov"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     11.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456308.2928
		PericenterDist   3.9238
		Eccentricity     0.273637
		Inclination      14.454
		AscendingNode    214.28
		ArgOfPericenter  205.763
		MeanAnomaly      0
	}
}

Comet	"277P LINEAR"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     14
	SlopeParam  4
	Orbit
	{
		Epoch            2456449.433
		PericenterDist   1.91318
		Eccentricity     0.504443
		Inclination      16.7474
		AscendingNode    276.363
		ArgOfPericenter  152.299
		MeanAnomaly      0
	}
}

Comet	"278P McNaught"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     14
	SlopeParam  4
	Orbit
	{
		Epoch            2456264.0241
		PericenterDist   2.09767
		Eccentricity     0.433177
		Inclination      6.6821
		AscendingNode    15.5043
		ArgOfPericenter  238.001
		MeanAnomaly      0
	}
}

Comet	"280P Larsen"
{
	ParentBody "Sol"
	CometType  "P"
	AbsMagn     12.5
	SlopeParam  4
	Orbit
	{
		Epoch            2456637.6221
		PericenterDist   2.63588
		Eccentricity     0.417218
		Inclination      11.7728
		AscendingNode    131.514
		ArgOfPericenter  104.612
		MeanAnomaly      0
	}
}
