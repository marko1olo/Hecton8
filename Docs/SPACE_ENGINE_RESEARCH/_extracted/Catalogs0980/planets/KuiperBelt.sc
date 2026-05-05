////////////////////////////////////////////////////////////
//                                                        //
//    Catalog of Kuiper belt asteroids for SpaceEngine    //
//                                                        //
// Data from  Minor Planet Center:                        //
// http://www.minorplanetcenter.net/iau/MPCORB.html       //
// Latest revision:  18 March 2013                        //
//                                                        //
// This file contains the most big or famous KBOs and     //
// Centaurs. Dwarf planets are in the SolarSys.sc         //
//                                                        //
////////////////////////////////////////////////////////////

Asteroid	"Chiron/(2060) Chiron"
{
	ParentBody     "Sol"
	AsterType      "Centaur"
	Radius          117   // 144
	RotationPeriod  5.918
	Albedo          0.075 // 0.048
	AbsMagn         6.5   // 6.2
	SlopeParam      0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.0195367
		SemiMajorAxis    13.6532
		Eccentricity     0.380366
		Inclination      6.92945
		AscendingNode    209.356
		ArgOfPericenter  339.33
		MeanAnomaly      122.844
	}
}

Asteroid	"Sedna/(90377) Sedna"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	Radius      850
	Albedo      0.05
	Color     ( 0.750 0.450 0.350 )
	AbsMagn     1.5
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       7.785e-005
		SemiMajorAxis    543.202
		Eccentricity     0.859663
		Inclination      11.9284
		AscendingNode    144.468
		ArgOfPericenter  311.006
		MeanAnomaly      358.225
	}
}

Asteroid	"Varuna/(20000) Varuna"
{
	ParentBody "Sol"
	AsterType  "Cubewano"
	Radius      450
	Oblateness  0.333
	Albedo      0.07
	Color     ( 0.650 0.450 0.350 )
	AbsMagn     3.6
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.0034725
		SemiMajorAxis    43.1892
		Eccentricity     0.0534397
		Inclination      17.1393
		AscendingNode    97.2572
		ArgOfPericenter  273.335
		MeanAnomaly      98.8274
	}
}

Asteroid	"Ixion/(28978) Ixion"
{
	ParentBody "Sol"
	AsterType  "Plutino"
	Radius      600
	Albedo      0.04
	Color     ( 0.650 0.450 0.350 )
	AbsMagn     3.3
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00398931
		SemiMajorAxis    39.3735
		Eccentricity     0.24466
		Inclination      19.6726
		AscendingNode    71.0499
		ArgOfPericenter  300.561
		MeanAnomaly      275.985
	}
}

// Data from: http://www.minorplanetcenter.net/db_search/show_object?object_id=2015+RR245
Asteroid	"2015 RR245"
{
    ParentBody  "Sol"
    
    Radius      350
    AbsMagn     3.8
    SlopeParam  0.15
    
    Orbit
    {
        Epoch           2457600.5
        SemiMajorAxis   81.4395185
        Period          735
        PericenterDist  33.6909348
        Inclination     7.57484
        Eccentricity    0.586373
        MeanAnomaly     322.64901
        AscendingNode   211.68252
        ArgOfPericen    261.38216
        RefPlane        "Ecliptic"
    }
}

// Rings texture by HarbingerDawn
Asteroid    "Chariklo/(10199) Chariklo"
{
	ParentBody "Sol"
	Class      "Asteroid"
	AsterType  "Centaur"
	Radius      143
	Albedo      0.031
	Color     ( 0.710 0.631 0.604 )
	AbsMagn     6.6
	SlopeParam  0.15
	Oblateness  0.33
	RotationPeriod 3.5

	Orbit
	{
		Epoch            2456401
		MeanMotion       0.015787
		SemiMajorAxis    15.7375
		Eccentricity     0.170708
		Inclination      23.4067
		AscendingNode    300.392
		ArgOfPericenter  241.399
		MeanAnomaly      54.0798
	}

	Rings
	{
		// Rings texture author: Sean Young "HarbingerDawn"
		Texture     "Chariklo-rings.*"
		InnerRadius  387.1
		OuterRadius  406.3
		// inner ring width = 7 km
		// outer ring width = 3.5 km
		// gap = 8.7 km
	}
}

Asteroid	"1996 TO66/(19308) 1996 TO66"
{
	ParentBody "Sol"
	AsterType  "Cubewano"
	AbsMagn     4.5
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00344817
		SemiMajorAxis    43.392
		Eccentricity     0.115455
		Inclination      27.4295
		AscendingNode    355.228
		ArgOfPericenter  240.509
		MeanAnomaly      132.466
	}
}

Asteroid	"1998 SM165/(26308) 1998 SM165"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     5.8
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00297672
		SemiMajorAxis    47.8605
		Eccentricity     0.370911
		Inclination      13.4911
		AscendingNode    183.122
		ArgOfPericenter  131.655
		MeanAnomaly      41.5974
	}
}

Asteroid	"1998 SN165/(35671) 1998 SN165"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     5.6
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00420804
		SemiMajorAxis    37.997
		Eccentricity     0.0454006
		Inclination      4.59877
		AscendingNode    192.101
		ArgOfPericenter  261.751
		MeanAnomaly      285.648
	}
}

Asteroid	"2001 UR163/(42301) 2001 UR163"
{
	ParentBody "Sol"
	AbsMagn     4.2
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00264895
		SemiMajorAxis    51.7313
		Eccentricity     0.281463
		Inclination      0.75242
		AscendingNode    302.36
		ArgOfPericenter  343.423
		MeanAnomaly      73.5933
	}
}

Asteroid	"2002 AW197/(55565) 2002 AW197"
{
	ParentBody "Sol"
	AsterType  "Cubewano"
	Radius      450
	Albedo      0.1
	Color     ( 0.650 0.450 0.350 )
	AbsMagn     3.4
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00302527
		SemiMajorAxis    47.3471
		Eccentricity     0.127467
		Inclination      24.337
		AscendingNode    297.416
		ArgOfPericenter  294.334
		MeanAnomaly      289.47
	}
}

Asteroid	"2002 TX300/(55636) 2002 TX300"
{
	ParentBody "Sol"
	AsterType  "Cubewano"
	AbsMagn     3.2
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00344596
		SemiMajorAxis    43.4106
		Eccentricity     0.1223
		Inclination      25.8683
		AscendingNode    324.653
		ArgOfPericenter  342.159
		MeanAnomaly      67.2214
	}
}

Asteroid	"2002 XW93/(78799) 2002 XW93"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     5.5
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00425996
		SemiMajorAxis    37.6876
		Eccentricity     0.244258
		Inclination      14.3357
		AscendingNode    46.7626
		ArgOfPericenter  248.571
		MeanAnomaly      134.694
	}
}

Asteroid	"1999 CC158/(79978) 1999 CC158"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     5.8
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00246824
		SemiMajorAxis    54.2264
		Eccentricity     0.279639
		Inclination      18.7263
		AscendingNode    337.008
		ArgOfPericenter  101.902
		MeanAnomaly      39.9509
	}
}

Asteroid	"2000 YW134/(82075) 2000 YW134"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     4.8
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00221563
		SemiMajorAxis    58.2736
		Eccentricity     0.294114
		Inclination      19.7785
		AscendingNode    126.945
		ArgOfPericenter  316.526
		MeanAnomaly      27.6202
	}
}

Asteroid	"2002 TC302/(84522) 2002 TC302"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     3.8
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00237127
		SemiMajorAxis    55.6949
		Eccentricity     0.297828
		Inclination      35.0076
		AscendingNode    23.8404
		ArgOfPericenter  86.1149
		MeanAnomaly      320.693
	}
}

Asteroid	"2003 VS2/(84922) 2003 VS2"
{
	ParentBody "Sol"
	AsterType  "Plutino"
	AbsMagn     4.1
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00394465
		SemiMajorAxis    39.6701
		Eccentricity     0.0813026
		Inclination      14.7724
		AscendingNode    302.809
		ArgOfPericenter  113.92
		MeanAnomaly      11.7355
	}
}

Asteroid	"2004 GV9/(90568) 2004 GV9"
{
	ParentBody "Sol"
	AsterType  "Cubewano"
	AbsMagn     4
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00363633
		SemiMajorAxis    41.882
		Eccentricity     0.0764651
		Inclination      22.0248
		AscendingNode    250.539
		ArgOfPericenter  290.52
		MeanAnomaly      33.7544
	}
}

Asteroid	"2002 KX14/(119951) 2002 KX14"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     4.4
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.0041001
		SemiMajorAxis    38.661
		Eccentricity     0.0451131
		Inclination      0.40603
		AscendingNode    286.449
		ArgOfPericenter  78.0788
		MeanAnomaly      251.28
	}
}

Asteroid	"2003 OP32/(120178) 2003 OP32"
{
	ParentBody "Sol"
	AsterType  "Cubewano"
	AbsMagn     3.6
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00348028
		SemiMajorAxis    43.1248
		Eccentricity     0.103769
		Inclination      27.1542
		AscendingNode    183.016
		ArgOfPericenter  68.4566
		MeanAnomaly      67.2374
	}
}

Asteroid	"2004 TY364/(120348) 2004 TY364"
{
	ParentBody "Sol"
	AsterType  "Plutino"
	AbsMagn     4.5
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00404305
		SemiMajorAxis    39.0239
		Eccentricity     0.0662866
		Inclination      24.8443
		AscendingNode    140.568
		ArgOfPericenter  354.321
		MeanAnomaly      267.302
	}
}

Asteroid	"2001 QT322/(135182) 2001 QT322"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     6.2
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00435306
		SemiMajorAxis    37.1484
		Eccentricity     0.0172851
		Inclination      1.83413
		AscendingNode    224.381
		ArgOfPericenter  59.3088
		MeanAnomaly      83.968
	}
}

Asteroid	"2004 UX10/(144897) 2004 UX10"
{
	ParentBody "Sol"
	AsterType  "Plutino"
	AbsMagn     4.5
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00402095
		SemiMajorAxis    39.1667
		Eccentricity     0.039982
		Inclination      9.53472
		AscendingNode    148.003
		ArgOfPericenter  158.76
		MeanAnomaly      84.0587
	}
}

Asteroid	"2005 RM43/(145451) 2005 RM43"
{
	ParentBody "Sol"
	AbsMagn     4.4
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00111623
		SemiMajorAxis    92.0381
		Eccentricity     0.61839
		Inclination      28.7153
		AscendingNode    84.6735
		ArgOfPericenter  318.523
		MeanAnomaly      3.39283
	}
}

Asteroid	"2005 RN43/(145452) 2005 RN43"
{
	ParentBody "Sol"
	AsterType  "Cubewano"
	AbsMagn     3.9
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00368786
		SemiMajorAxis    41.4909
		Eccentricity     0.0230086
		Inclination      19.2631
		AscendingNode    187.012
		ArgOfPericenter  177.88
		MeanAnomaly      331.81
	}
}

Asteroid	"2005 RR43/(145453) 2005 RR43"
{
	ParentBody "Sol"
	AsterType  "Cubewano"
	AbsMagn     4
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00343968
		SemiMajorAxis    43.4634
		Eccentricity     0.141393
		Inclination      28.4561
		AscendingNode    85.904
		ArgOfPericenter  281.134
		MeanAnomaly      38.4563
	}
}

Asteroid	"2005 TB190/(145480) 2005 TB190"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     4.7
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00149442
		SemiMajorAxis    75.7683
		Eccentricity     0.390365
		Inclination      26.4741
		AscendingNode    180.458
		ArgOfPericenter  171.867
		MeanAnomaly      357.862
	}
}

Asteroid	"2000 CR105/(148209) 2000 CR105"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     6.3
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00028483
		SemiMajorAxis    228.777
		Eccentricity     0.807119
		Inclination      22.7208
		AscendingNode    128.248
		ArgOfPericenter  316.881
		MeanAnomaly      4.9797
	}
}

Asteroid	"2004 PF115/(175113) 2004 PF115"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     4.4
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00405631
		SemiMajorAxis    38.9388
		Eccentricity     0.0675723
		Inclination      13.3672
		AscendingNode    84.7267
		ArgOfPericenter  82.4969
		MeanAnomaly      163.259
	}
}

Asteroid	"2005 UQ513/(202421) 2005 UQ513"
{
	ParentBody "Sol"
	AsterType  "Cubewano"
	AbsMagn     3.4
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00343957
		SemiMajorAxis    43.4643
		Eccentricity     0.14526
		Inclination      25.7317
		AscendingNode    307.874
		ArgOfPericenter  220.109
		MeanAnomaly      222.346
	}
}

Asteroid	"2007 OR10/(225088) 2007 OR10"
{
	ParentBody "Sol"
	AbsMagn     2
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00180157
		SemiMajorAxis    66.8911
		Eccentricity     0.503176
		Inclination      30.8554
		AscendingNode    336.831
		ArgOfPericenter  206.682
		MeanAnomaly      103.018
	}
}

Asteroid	"2004 XA192/(230965) 2004 XA192"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     4
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00301214
		SemiMajorAxis    47.4846
		Eccentricity     0.252833
		Inclination      38.0952
		AscendingNode    328.705
		ArgOfPericenter  131.902
		MeanAnomaly      353.843
	}
}

Asteroid	"2007 JJ43/(278361) 2007 JJ43"
{
	ParentBody "Sol"
	AsterType  "Cubewano"
	AbsMagn     3.9
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00298305
		SemiMajorAxis    47.7928
		Eccentricity     0.156222
		Inclination      12.0845
		AscendingNode    272.456
		ArgOfPericenter  8.43206
		MeanAnomaly      333.65
	}
}

Asteroid	"2005 QU182/(303775) 2005 QU182"
{
	ParentBody "Sol"
	AbsMagn     3.5
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00083184
		SemiMajorAxis    111.972
		Eccentricity     0.669608
		Inclination      14.0178
		AscendingNode    78.5193
		ArgOfPericenter  224.169
		MeanAnomaly      12.673
	}
}

Asteroid	"2002 MS4/(307261) 2002 MS4"
{
	ParentBody "Sol"
	AsterType  "Cubewano"
	AbsMagn     3.7
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00367177
		SemiMajorAxis    41.612
		Eccentricity     0.148416
		Inclination      17.7101
		AscendingNode    216.167
		ArgOfPericenter  215.001
		MeanAnomaly      213.209
	}
}

Asteroid	"2010 EP65/(312645) 2010 EP65"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     5.5
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00300134
		SemiMajorAxis    47.5984
		Eccentricity     0.305503
		Inclination      18.9075
		AscendingNode    205.009
		ArgOfPericenter  351.736
		MeanAnomaly      358.066
	}
}

Asteroid	"2003 UZ413"
{
	ParentBody "Sol"
	AsterType  "Plutino"
	AbsMagn     4.2
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00398816
		SemiMajorAxis    39.3811
		Eccentricity     0.219664
		Inclination      12.043
		AscendingNode    136.114
		ArgOfPericenter  146.24
		MeanAnomaly      103.587
	}
}

Asteroid	"2004 KH19"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     6.5
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00379687
		SemiMajorAxis    40.6929
		Eccentricity     0.119625
		Inclination      35.3021
		AscendingNode    232.971
		ArgOfPericenter  225.17
		MeanAnomaly      133.084
	}
}

Asteroid	"2004 NT33"
{
	ParentBody "Sol"
	AsterType  "Cubewano"
	AbsMagn     4.4
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00345129
		SemiMajorAxis    43.3659
		Eccentricity     0.148454
		Inclination      31.2338
		AscendingNode    241.133
		ArgOfPericenter  37.986
		MeanAnomaly      35.7964
	}
}

Asteroid	"2004 OJ14"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     6.4
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00240998
		SemiMajorAxis    55.0968
		Eccentricity     0.288322
		Inclination      22.5074
		AscendingNode    104.253
		ArgOfPericenter  130.604
		MeanAnomaly      42.4781
	}
}

Asteroid	"2004 PT107"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     6
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00381952
		SemiMajorAxis    40.5319
		Eccentricity     0.0578631
		Inclination      26.1594
		AscendingNode    320.951
		ArgOfPericenter  21.838
		MeanAnomaly      347.097
	}
}

Asteroid	"2004 TF282"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     6.3
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00133399
		SemiMajorAxis    81.7276
		Eccentricity     0.517792
		Inclination      23.1469
		AscendingNode    234.964
		ArgOfPericenter  171.409
		MeanAnomaly      5.24835
	}
}

Asteroid	"2004 VN112"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     6.4
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00015497
		SemiMajorAxis    343.271
		Eccentricity     0.862118
		Inclination      25.5114
		AscendingNode    66.0673
		ArgOfPericenter  327.195
		MeanAnomaly      0.19775
	}
}

Asteroid	"2004 XR190"
{
	ParentBody "Sol"
	AsterType  "Cubewano"
	Radius      750
	AbsMagn     4.4
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00224417
		SemiMajorAxis    57.7784
		Eccentricity     0.107617
		Inclination      46.5257
		AscendingNode    252.353
		ArgOfPericenter  280.877
		MeanAnomaly      276.84
	}
}

Asteroid	"2005 SD278"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     6.5
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00235727
		SemiMajorAxis    55.9152
		Eccentricity     0.285596
		Inclination      17.8519
		AscendingNode    152.635
		ArgOfPericenter  219.25
		MeanAnomaly      23.1496
	}
}

Asteroid	"2006 QH181"
{
	ParentBody "Sol"
	AbsMagn     3.8
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00177236
		SemiMajorAxis    67.624
		Eccentricity     0.434244
		Inclination      19.1425
		AscendingNode    73.827
		ArgOfPericenter  211.52
		MeanAnomaly      100.109
	}
}

Asteroid	"2008 ST291"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     4.3
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.0009903
		SemiMajorAxis    99.6838
		Eccentricity     0.572782
		Inclination      20.8042
		AscendingNode    331.104
		ArgOfPericenter  325.088
		MeanAnomaly      21.2172
	}
}

Asteroid	"2009 YE7"
{
	ParentBody "Sol"
	AsterType  "Cubewano"
	AbsMagn     4.3
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00331407
		SemiMajorAxis    44.5549
		Eccentricity     0.137877
		Inclination      29.0837
		AscendingNode    141.657
		ArgOfPericenter  99.6644
		MeanAnomaly      176.853
	}
}

Asteroid	"2010 ER65"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     5.4
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00101086
		SemiMajorAxis    98.3278
		Eccentricity     0.593217
		Inclination      21.2701
		AscendingNode    212.601
		ArgOfPericenter  323.883
		MeanAnomaly      2.50714
	}
}

Asteroid	"2010 ET65"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     5.2
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00200234
		SemiMajorAxis    62.3415
		Eccentricity     0.364411
		Inclination      30.6204
		AscendingNode    189.565
		ArgOfPericenter  353.613
		MeanAnomaly      359.908
	}
}

Asteroid	"2010 EK139"
{
	ParentBody "Sol"
	AbsMagn     3.8
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00171983
		SemiMajorAxis    68.9943
		Eccentricity     0.528735
		Inclination      29.4527
		AscendingNode    346.163
		ArgOfPericenter  284.752
		MeanAnomaly      343.883
	}
}

Asteroid	"2010 FC49"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     5.9
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00405027
		SemiMajorAxis    38.9774
		Eccentricity     0.0498921
		Inclination      39.75
		AscendingNode    1.75911
		ArgOfPericenter  301.109
		MeanAnomaly      246.531
	}
}

Asteroid	"2010 FX86"
{
	ParentBody "Sol"
	AsterType  "Cubewano"
	AbsMagn     4.3
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00308933
		SemiMajorAxis    46.6903
		Eccentricity     0.0592955
		Inclination      25.1785
		AscendingNode    310.823
		ArgOfPericenter  355.508
		MeanAnomaly      280.617
	}
}

Asteroid	"2010 HE79"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     5.2
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00406743
		SemiMajorAxis    38.8678
		Eccentricity     0.180716
		Inclination      15.7507
		AscendingNode    238.691
		ArgOfPericenter  281.231
		MeanAnomaly      57.4422
	}
}

Asteroid	"2010 KZ39"
{
	ParentBody "Sol"
	AsterType  "Cubewano"
	AbsMagn     4
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00325968
		SemiMajorAxis    45.0491
		Eccentricity     0.0557265
		Inclination      26.1349
		AscendingNode    53.2132
		ArgOfPericenter  321.86
		MeanAnomaly      243.619
	}
}

Asteroid	"2010 RF43"
{
	ParentBody "Sol"
	AbsMagn     4.1
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00283727
		SemiMajorAxis    49.4162
		Eccentricity     0.251046
		Inclination      30.6248
		AscendingNode    25.3664
		ArgOfPericenter  192.118
		MeanAnomaly      91.1426
	}
}

Asteroid	"2010 RE64"
{
	ParentBody "Sol"
	AbsMagn     4.3
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00194381
		SemiMajorAxis    63.5868
		Eccentricity     0.415966
		Inclination      13.5282
		AscendingNode    67.5569
		ArgOfPericenter  20.8602
		MeanAnomaly      313.401
	}
}

Asteroid	"2010 TJ"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     5
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00196208
		SemiMajorAxis    63.1915
		Eccentricity     0.368166
		Inclination      38.8714
		AscendingNode    91.4
		ArgOfPericenter  274.036
		MeanAnomaly      8.766
	}
}

Asteroid	"2012 HH2"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     6.2
	SlopeParam  0.15
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.004782
		SemiMajorAxis    34.8923
		Eccentricity     0.165308
		Inclination      28.6118
		AscendingNode    56.3879
		ArgOfPericenter  99.5649
		MeanAnomaly      26.6592
	}
}

Asteroid	"1995 SN55"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     6
	SlopeParam  0.15
	Orbit
	{
		Epoch            2449981
		MeanMotion       0.00861662
		SemiMajorAxis    23.5638
		Eccentricity     0.663131
		Inclination      4.97274
		AscendingNode    144.611
		ArgOfPericenter  49.3275
		MeanAnomaly      180.217
	}
}

Asteroid	"2004 PD112"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     6.1
	SlopeParam  0.15
	Orbit
	{
		Epoch            2453261
		MeanMotion       0.0019118
		SemiMajorAxis    64.2946
		Eccentricity     0.322236
		Inclination      6.72727
		AscendingNode    17.0466
		ArgOfPericenter  44.7008
		MeanAnomaly      317.699
	}
}

Asteroid	"2006 QT180"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     6.1
	SlopeParam  0.15
	Orbit
	{
		Epoch            2453961
		MeanMotion       0.00427299
		SemiMajorAxis    37.611
		Eccentricity     0.0148731
		Inclination      31.2138
		AscendingNode    123.917
		ArgOfPericenter  178.46
		MeanAnomaly      359.966
	}
}

Asteroid	"2010 PK66"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     5.6
	SlopeParam  0.15
	Orbit
	{
		Epoch            2455461
		MeanMotion       0.00376409
		SemiMajorAxis    40.9289
		Eccentricity     0.0050604
		Inclination      13.6344
		AscendingNode    331.653
		ArgOfPericenter  333.215
		MeanAnomaly      359.666
	}
}

Asteroid	"2010 RN64"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     5.7
	SlopeParam  0.15
	Orbit
	{
		Epoch            2455481
		MeanMotion       0.00376441
		SemiMajorAxis    40.9265
		Eccentricity     0.0627816
		Inclination      19.872
		AscendingNode    68.5235
		ArgOfPericenter  41.2512
		MeanAnomaly      262.052
	}
}

Asteroid	"2010 TR19"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     5.4
	SlopeParam  0.15
	Orbit
	{
		Epoch            2455481
		MeanMotion       0.00443775
		SemiMajorAxis    36.6742
		Eccentricity     0.686052
		Inclination      25.4126
		AscendingNode    94.7616
		ArgOfPericenter  35.7364
		MeanAnomaly      304.708
	}
}

Asteroid	"2010 VX11"
{
	ParentBody "Sol"
	AsterType  "Centaur"
	AbsMagn     6.3
	SlopeParam  0.15
	Orbit
	{
		Epoch            2455521
		MeanMotion       0.00362145
		SemiMajorAxis    41.9966
		Eccentricity     0.492166
		Inclination      22.4109
		AscendingNode    171.097
		ArgOfPericenter  332.378
		MeanAnomaly      326.174
	}
}

