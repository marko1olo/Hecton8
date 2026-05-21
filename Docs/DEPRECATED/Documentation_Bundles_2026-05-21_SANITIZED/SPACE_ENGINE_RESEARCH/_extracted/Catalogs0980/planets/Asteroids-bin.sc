////////////////////////////////////////////////////////////
//                                                        //
//  Catalog of binary/multiple asteroids for SpaceEngine  //
//                                                        //
// This file contains binaty/multiple asteroids from the  //
// main asteroids belt, Kuiper belt, Centaurs and NEO.    //
// Some largest objects (dwarf planets) with satellites   //
// are already in the SolarSys.sc file.                   //
//                                                        //
////////////////////////////////////////////////////////////

////////////////////////////////////////////////////////////
//                Main asteroid belt                      //
////////////////////////////////////////////////////////////

Asteroid	"Ida/(243) Ida"
{
	ParentBody      "Sol"
	Albedo          0.24
	Radius          28.9
	RotationPeriod  4.633632
	RotationOffset  359.46
	Obliquity       156.96
	EqAscendNode    352.77
	AbsMagn         9.94
	SlopeParam      0.15
	Orbit
	{
		Period         4.8417
		SemiMajorAxis  2.863731
		Eccentricity   0.043109
		Inclination    1.13711
		AscendingNode  324.586055
		LongOfPericen  113.017101
		MeanAnomaly    131.594945
	}
}

Asteroid	"Dactyl/Ida I/(243) Ida I"
{
	ParentBody      "Ida"
	Class			"Asteroid"
	Albedo          0.2
	Radius          0.7
	RotationOffset  123

	Orbit
	{
		Period         0.002643013321
		SemiMajorAxis  5.581550801e-007
		Eccentricity   0.13
		Inclination    8
		AscendingNode  90
		LongOfPericen  310
		RefPlane      "Equator"
	}
}

Asteroid	"Sylvia/(87) Sylvia"
{
	ParentBody     "Sol"
	AbsMagn         6.94
	SlopeParam      0.15
	Albedo          0.0435
	Radius          143
	Mass            2.47463e-6
	RotationPeriod  5.183642
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.15114
		SemiMajorAxis    3.49046
		Eccentricity     0.0888657
		Inclination      10.8837
		AscendingNode    73.1368
		ArgOfPericenter  264.51
		MeanAnomaly      117.971
	}
}

Asteroid	"Romulus/Sylvia I/(87) Sylvia I"
{
	ParentBody     "Sylvia"
	Class			"Asteroid"
	AbsMagn         10.7
	Radius          9
	Mass            6.7e-10
	Orbit
	{
		Period         0.00999227
		SemiMajorAxis  9.0642923e-6
		Eccentricity   0.001
		Inclination    1.7
		RefPlane      "Equator"
	}
}

Asteroid	"Remus/Sylvia II/(87) Sylvia II"
{
	ParentBody      "Sylvia"
	Class			"Asteroid"
	AbsMagn         11.1
	Radius          3.5
	Mass            3.3e-11
	Orbit
	{
		Period         0.00377503
		SemiMajorAxis  4.7193144e-6
		Eccentricity   0.016
		Inclination    2
		RefPlane      "Equator"
	}
}

Asteroid "Camilla/(107) Camilla"
{
	ParentBody		"Sol"
	Radius			109.685
	AbsMagn			7.08
	SlopeParam		0.08
	Albedo			0.054
	RotationPeriod	4.84393
	Obliquity		29
    Orbit
    {
		Epoch			2457600.5
		SemiMajorAxis	3.487326707
		Period			6.512491296
		MeanAnomaly		175.0490822972731
		ArgOfPericenter	306.618278
		Eccentricity	0.066773541
		Inclination		10.0032973
		AscendingNode	172.621467
		PericenterDist	3.25446556
    }
}

Asteroid  "S2001 (107) 1"
{
    ParentBody  "(107) Camilla"
    Class       "Asteroid"
    Radius      8

    Orbit
    {
        SemiMajorAxis   8.35573390283556e-6
        Period          0.0101904985
        MeanAnomaly     0
        ArgOfPericenter 0
        Eccentricity    0.0015
        Inclination     3
        AscendingNode   0
        RefPlane        "Equator"
    }
}

Asteroid "Kalliope/(22) Kalliope"
{
	ParentBody	"Sol"
	Radius			83.1
	AbsMagn			6.45
	SlopeParam		0.21
	Albedo			0.169
	RotationPeriod	4.1482
	Obliquity		103
	EqAscendNode    0	// no data
    Orbit
    {
		Epoch			2457600.5
		SemiMajorAxis	2.910478163
		Period			4.965405488
		MeanAnomaly		359.389969131262
		ArgOfPericenter	354.871544
		Eccentricity	0.099848778
		Inclination		13.7157902
		AscendingNode	66.085229
		PericenterDist	2.61987048
    }
}

Asteroid  "Linus/(22) Kalliope I Linus"
{
    ParentBody  "(22) Kalliope"
    Class		"Asteroid"
    Radius		14
    DiscDate	"2001"
    Orbit
    {
		SemiMajorAxis	7.31962289888395e-6
		Period			0.009845522
		MeanAnomaly		0
		ArgOfPericenter	180
		Eccentricity	0.0015
		Inclination		93.4
		AscendingNode   0	// no data
		RefPlane		"Ecliptic"
    }
}

Asteroid "Eugenia/(45) Eugenia"
{
	ParentBody	 	"Sol"
	Radius			103.07
	Mass			9.42385591e-7
	AbsMagn			7.46
	RotationPeriod	5.699160
	Albedo			0.046
	SlopeParam		0.07
	Obliquity		117
	Orbit
    {
        Epoch			2457600.5
		Period			4.486870662
        SemiMajorAxis   2.720342369
        Eccentricity    0.083185113
        Inclination     6.6033570
        AscendingNode   147.681770
        ArgOfPericenter 88.689234
		PericenterDist	2.49405038
        MeanAnomaly     178.148054920922
    }
}

Asteroid "Petit-Prince/(45) Eugenia I Petit-Prince"
{
    ParentBody  "(45) Eugenia"
    Class		"Asteroid"
    Radius		3.5
    DiscDate	"1998"
    Orbit
    {
        Epoch           2452980
        SemiMajorAxis   7.783667e-6
        Period          0.01291198041
		Eccentricity	0.002
		Inclination		8
        MeanAnomaly     5
        ArgOfPericenter 138.0
		AscendingNode	201.93
		RefPlane		"Equator"
    }
}

Asteroid "S2004 (45) 1"
{
    ParentBody  "(45) Eugenia"
    Class		"Asteroid"
    Radius		2.5
    DiscDate	"2004"
    Orbit
    {
        Epoch           2452980
        SemiMajorAxis   4.081542e-6
        Period          0.004909071
		Eccentricity	0.11
        MeanAnomaly     -187
        ArgOfPericenter 95.0
		AscendingNode	206.67
		RefPlane		"Equator"
    }
}

Barycenter "Antiope system"
{
    ParentBody "Sol"
    Orbit
    {
		Epoch			2457600.5
		Period			5.602751
		SemiMajorAxis	3.154487052
		Eccentricity	0.163458400
		Inclination		2.20720
		AscendingNode	70.04471
		ArgOfPericenter	244.40561
		PericenterDist	2.63885965
		MeanAnomaly		328.9060186001217
		RefPlane		"Ecliptic"
    }
}

Asteroid "Antiope A/(90) Antiope A"
{
    ParentBody      "Antiope system"
    Class           "Asteroid"
    AsterType       "Themis"
    AbsMagn     	8.27
	Albedo			0.062
	RotationPeriod	16.5
	TidalLocked     true
    Radius			43.9	// 0.535 ratio of total volume
    SlopeParam 		0.15
    Orbit
    {
		SemiMajorAxis	5.31622728768669E-07 // separation	171 km *(1-volume ratio)
		Period			0.00188286025
		Eccentricity	0.003
		Inclination		63.7
		ArgOfPericenter	0
		MeanAnomaly		0
		RefPlane		"Equator"
    }
}

Asteroid "Antiope B/(90) S 2000"
{
    ParentBody      "Antiope system"
    Class           "Asteroid"
    AsterType 		"Themis"
    Radius 			41.9	// 0.465 of total volume
	RotationPeriod	16.5
	TidalLocked     true
    AbsMagn     	9.02
	Albedo			0.062
    SlopeParam  	0.15
    Orbit
    {
		SemiMajorAxis	6.11441669139236E-07 // separation 	171 km *(1-volume ratio)
		Period			0.00188286025
		Eccentricity	0.003
		Inclination		63.7
		ArgOfPericenter 180
		MeanAnomaly		0
		RefPlane		"Equator"
	}
}

Asteroid "Elektra/(130) Elektra"
{
	ParentBody		"Sol"
	Radius			99.465
	AbsMagn			7.12
	SlopeParam		0.15
	Albedo			0.071
	RotationPeriod	5.22468
	Obliquity       83
    EqAscendNode    12
    Orbit
    {
		Epoch			2457600.5
		SemiMajorAxis	3.123238626
		Period			5.519706084
		MeanAnomaly		128.8846041360816
		ArgOfPericenter	235.640394
		Eccentricity	0.208453864
		Inclination		22.8674704
		AscendingNode	145.408295
		PericenterDist	2.47218747
		RefPlane		"Ecliptic"
    }
}

Asteroid "S2003 (130) 1"
{
    ParentBody  "(130) Elektra"
    Class		"Asteroid"
    Radius		3.5
    Orbit
    {
        SemiMajorAxis   8.81028582714981e-6 //   1318 km
        Period          0.0143959273 //   5.258 days
        Eccentricity    0.13
        Inclination     3
        RefPlane       "Equator"
    }
}

Asteroid "S2014 (130) 1"
{
    ParentBody  "(130) Elektra"
    Class	 	"Asteroid"
    Radius		2.6
    Orbit
    {
        SemiMajorAxis   3.07491007624349e-6 //   460 km
        Period          0.0030117003 //   1.1 days
        Eccentricity    0
        Inclination     0
        MeanAnomaly     0
        RefPlane 		"Equator"
    }
}

Asteroid "Minerva/(93) Minerva"
{
	ParentBody 		"Sol"
	Radius			70.8
	AbsMagn			7.8
	SlopeParam		0.15
	Albedo			0.073
	RotationPeriod	5.981767
	Obliquity       89	// Aegis' orbit
	EqAscendNode    126	// Aegis' orbit
    Orbit
    {
		Epoch			2457600.5
		SemiMajorAxis	2.754124319
		Period			4.57070837
		MeanAnomaly		262.02186155931
		ArgOfPericenter	274.841096
		Eccentricity	0.14122459
		Inclination		8.5606727
		AscendingNode	4.0756
		PericenterDist	2.36517424
    }
}

Asteroid "Aegis/S (93) 1 Aegis"
{
    ParentBody "(93) Minerva"
    Class      "Asteroid"
    Radius	    1.8
    Orbit
    {
		SemiMajorAxis	4.16784E-06 //   623.5 km
		Period			0.0065874   //   2.406 days
		MeanAnomaly		0
		ArgOfPericenter	82
		Eccentricity	0
		Inclination		89
		AscendingNode	126
		RefPlane       "Ecliptic"
    }
}

Asteroid "Gorgoneion/S (93) 2 Gorgoneion"
{
    ParentBody "(93) Minerva"
    Class      "Asteroid"
    Radius      1.6
    Orbit
    {
		SemiMajorAxis	2.50672E-06 //   375 km
		Period			0.0030519   //   1.1147 days
		MeanAnomaly		0
		ArgOfPericenter	347.5
		Eccentricity	0.05
		Inclination		91.4
		AscendingNode	132.6
		RefPlane       "Ecliptic"
    }
}

Asteroid "Pulcova/(762) Pulcova"
{
	ParentBody		"Sol"
	Class			"Asteroid"
	Radius			70.86
	Mass			2.344E-7
	RotationPeriod	5.839
    Orbit
	{
		Epoch			2457400.5
		SemiMajorAxis	3.1562
		Period			5.6074
		Eccentricity	0.10289
		Inclination		13.089
		AscendingNode	305.76
		ArgOfPericenter	189.18
		MeanAnomaly		305.76
		RefPlane		"Ecliptic"
	}
}

Asteroid "S2000 (762) 1"
{
    ParentBody  "(762) Pulcova"
    Class		"Asteroid"
    Radius		8
    Mass		3.35E-10
    Orbit
    {
		Epoch			2457400.5
		SemiMajorAxis	4.699E-6
		Period			0.012151
        Eccentricity	0.03
        Inclination		0
        RefPlane		"Equator"
	}
}

Asteroid "Kleopatra/(216) Kleopatra"
{
	ParentBody     "Sol"
	Radius          67.5
	RotationPeriod  5.385277
	Oblateness      0.6
	Obliquity       8
	EqAscendNode    72
	Mass            7.769E-7
    Orbit
	{
		Epoch			2457400.5
		SemiMajorAxis	2.7968
		Period			4.6774
		Eccentricity	0.2492
		Inclination		13.096
		AscendingNode	215.46
		ArgOfPericenter	180.31
		MeanAnomaly		50.546
		RefPlane		"Ecliptic"
	}
}

Asteroid "Alexhelios/(216) Kleopatra I"
{
	ParentBody  "Kleopatra"
	Class       "Asteroid"
	Radius      4.45
	Orbit
	{
		Epoch           2457400.5
		SemiMajorAxis   4.532E-6
		Period   0.006352
		Eccentricity    0
		Inclination     0
		MeanAnomaly		0
		RefPlane       "Equator"
	}
}

Asteroid "Cleoselene/(216) Kleopatra II"
{
	ParentBody  "Kleopatra"
	Class       "Asteroid"
	Radius      3.45
    Orbit
	{
		Epoch           2457400.5
		SemiMajorAxis   3.035E-6
		Period   0.003395
		Eccentricity    0
		Inclination     0
		MeanAnomaly		0
		ArgOfPericenter 180
		RefPlane       "Equator"
	}
}

Barycenter "Balam system"
{
	ParentBody	"Sol"
    Mass         8.5367e-11
	Orbit
    {
		Epoch			2457600.5
		SemiMajorAxis	2.236702937
		Period			3.345189033
		MeanAnomaly		136.2912684077608
		ArgOfPericenter	173.985299
		Eccentricity	0.10927319
		Inclination		5.3823141
		AscendingNode	295.740745
		PericenterDist	1.99229127
		RefPlane		"Ecliptic"
    }
}

Barycenter "Balam-S2007"
{
    ParentBody "Balam system"
    Orbit
    {
		SemiMajorAxis	1.66144e-7
		Period			0.1670124692
		ArgOfPericenter	0
		Eccentricity	0.9
		MeanAnomaly		0
		RefPlane		"Equator"
	}
}

Asteroid "Balam/(3749) Balam"
{
	ParentBody		"Balam-S2007"
	Radius			1.975
	AbsMagn			13.1
	SlopeParam		0.15
	Albedo			0.355
	Obliquity		0	// no data
	RotationPeriod	2.80483
    Orbit
    {
		SemiMajorAxis	9.237e-9
		Period			0.0038084319
		ArgOfPericenter	0
		Eccentricity	0
		MeanAnomaly		0
		RefPlane		"Equator"
	}
}

Asteroid "S2007 (3749) 1"
{
	ParentBody	"Balam-S2007"
	Class		"Asteroid"
	Radius		0.83
	SlopeParam	0.15
	Albedo		0.355
	Obliquity	0	// no data
	Orbit
    {
		SemiMajorAxis	1.24454E-07 //   20 km    1.33691742445e-7 AU
		Period			0.0038084319 //   1.391 days
		ArgOfPericenter	180
		Eccentricity	0
		MeanAnomaly		0
		RefPlane		"Equator"
    }
}

Asteroid "S2002 (3749) 1"
{
    ParentBody  "Balam system"
    Class		"Asteroid"
    Radius		0.92
    SlopeParam	0.15
    Orbit
    {
        SemiMajorAxis   1.7657011e-6 //   289 km  1.93184567834e-6
        Period          0.1670124692 //   61 days
        ArgOfPericenter 180
        MeanAnomaly     0
        Eccentricity    0.9
        RefPlane        "Equator"
    }
}

Barycenter "Wolff system"
{
    ParentBody "Sol"
    Orbit
    {
		Epoch           2457400.5
		SemiMajorAxis   2.3574
		Period          3.6194
		Eccentricity    0.16635
		Inclination     1.7035
		AscendingNode   39.819
		ArgOfPericenter 311.55
		MeanAnomaly     40.268
		RefPlane       "Ecliptic"
    }
}

Asteroid "Wolff/(5674) Wolff"
{
    ParentBody		"Wolff system"
    Class			"Asteroid"
    Radius      	2.36 //Volume 55.06 = 66.07%
    RotationPeriod  93.7
	TidalLocked     true
    Orbit
    {
		SemiMajorAxis	6.803E-8 // separation 30 km
		Period			0.010689
		Eccentricity    0
		Inclination		0
		ArgOfPericenter	0
		MeanAnomaly		0
		AscendingNode   0
		RefPlane		"Equator"
    }
}

Asteroid "S2015 (5674) 1"
{
    ParentBody  	"Wolff system"
    Class       	"Asteroid"
    Radius      	1.89 //Volume 28.28 = 33.93%
    RotationPeriod  93.7
	TidalLocked     true
    Orbit
    {
		SemiMajorAxis	1.325E-7 // separation 30 km
		Period			0.010689
		Eccentricity	0
		Inclination		0
		ArgOfPericenter	180
		MeanAnomaly		0
        AscendingNode   0
		RefPlane		"Equator"
    }
}

Asteroid "Daphne/(41) Daphne"
{
    ParentBody  "Sol"
    Class       "Asteroid"
    Radius      87
    Mass		1.057E-6
    RotationPeriod  5.9879856
    Orbit
    {
        Epoch            2457400.5
        SemiMajorAxis    2.7607
		Period  		 4.5871
        Eccentricity     0.27514
        Inclination      15.793
        AscendingNode    178.09
        ArgOfPericenter  46.013
        MeanAnomaly      107.31
        RefPlane       	"Ecliptic"
    }
}

Asteroid "S2008 (41) 1"
{
    ParentBody  "(41) Daphne"
    Class       "Asteroid"
    Radius      1
    Orbit
    {
        SemiMajorAxis   2.961E-6
        Period          0.003012
        Eccentricity    0
        Inclination     0
        RefPlane       "Equator"
    }
}

Asteroid "Huntress/(7225) Huntress"
{
    ParentBody		"Sol"
    Class			"Asteroid"
    Radius			3.27
    RotationPeriod	2.44
    Orbit
    {
		Epoch			2457400.5
		SemiMajorAxis	2.3406
		Period			3.5811
		Eccentricity	0.20326
		Inclination		6.8716
		AscendingNode	275.76
		ArgOfPericenter	203.62
		MeanAnomaly		271.94
		RefPlane		"Ecliptic"
    }
}

Asteroid "S2007 (7225) 1"
{
    ParentBody  "(7225) Huntress"
    Class       "Asteroid"
    Radius      0.685
    Orbit
    {
		SemiMajorAxis	6.685E-8 // separation 10 km
		Period			0.001674
		Eccentricity    0
		Inclination		0
		RefPlane		"Equator"
    }
}

Asteroid "Pauling/(4674) Pauling"
{
    ParentBody  	"Sol"
    Class       	"Asteroid"
    Radius      	2.23
    RotationPeriod  2.521
    Orbit
    {
		Epoch			2457400.5
		SemiMajorAxis	1.8587
		Period			2.5341
		Eccentricity	0.070368
		Inclination		19.441
		AscendingNode	232.95
		ArgOfPericenter	239.58
		MeanAnomaly		49.983
		RefPlane		"Ecliptic"
    }
}

Asteroid "S2004 (4674) 1"
{
    ParentBody  "(4674) Pauling"
    Class       "Asteroid"
    Radius      0.705
    Orbit
    {
		SemiMajorAxis	1.671E-6 // 250 km
		Period			0.355928
		Eccentricity    0
		Inclination		0
		RefPlane		"Equator"
    }
}

Asteroid "Huenna/(379) Huenna"
{
    ParentBody  	"Sol"
    Class       	"Asteroid"
    Radius      	43.735
    Mass  			6.413E-8
    RotationPeriod  7.022
    Orbit
    {
		Epoch			2457400.5
		SemiMajorAxis	3.1369
		Period			5.5559
		Eccentricity	0.18637
		Inclination		1.6695
		AscendingNode	172.04
		ArgOfPericenter	179.93
		MeanAnomaly		344.12
		RefPlane		"Ecliptic"
    }
}

Asteroid "S2003 (379) 1"
{
    ParentBody  "(379) Huenna"
    Class       "Asteroid"
    Radius      2.9
    Orbit
    {
		SemiMajorAxis	2.23E-5 // 747 km
		Period			0.239841
		Eccentricity	0.222
		Inclination		0
		RefPlane		"Equator"
    }
}

Asteroid "Roxane/(317) Roxane"
{
    ParentBody		"Sol"
    Class			"Asteroid"
    Radius			9.93
    RotationPeriod	8.169
    Orbit
    {
		Epoch			2457400.5
		SemiMajorAxis	2.2867
		Period			3.4581
		Eccentricity	0.085548
		Inclination		1.7653
		AscendingNode	151.38
		ArgOfPericenter	187.09
		MeanAnomaly		171.1
		RefPlane		"Ecliptic"
    }
}

Asteroid "S2009 (317) 1"
{
    ParentBody  "(317) Roxane"
    Class       "Asteroid"
    Radius      2.65
    Orbit
    {
		SemiMajorAxis	1.718E-6 // 257 km
		Period			0.038331
		Eccentricity	0
		Inclination		0
		RefPlane		"Equator"
    }
}

Asteroid "Emma/(283) Emma"
{
    ParentBody		"Sol"
    Class			"Asteroid"
    Radius			67.35
    Mass			2.311E-7
    RotationPeriod	6.888
    Orbit
    {
		Epoch			2457400.5
		SemiMajorAxis	3.0471
		Period			5.3191
		Eccentricity	0.14845
		Inclination		7.9935
		AscendingNode	304.37
		ArgOfPericenter	53.617
		MeanAnomaly		338.86
		RefPlane		"Ecliptic"
    }
}

Asteroid "S2003 (283) 1"
{
    ParentBody  "(283) Emma"
    Class       "Asteroid"
    Radius      4.5
    Orbit
    {
		SemiMajorAxis	3.884E-6 // 581 km
		Period			0.00918
		Eccentricity	0.12
		Inclination		0
		RefPlane		"Equator"
    }
}

Asteroid "Hermione/(121) Hermione"
{
    ParentBody  	"Sol"
    Class       	"Asteroid"
    Radius      	93.5
    Mass  			7.87E-7
    RotationPeriod  5.551
    Orbit
    {
		Epoch   		2457400.5
		SemiMajorAxis	3.4501
		Period			6.4085
		Eccentricity	0.13427
		Inclination		7.5965
		AscendingNode	73.146
		ArgOfPericenter	298.36
		MeanAnomaly		301.36
		RefPlane		"Ecliptic"
    }
}

Asteroid "LaFayette/S2002 (121) 1"
{
    ParentBody  "(121) Hermione"
    Class       "Asteroid"
    Radius      16
    Orbit
    {
		SemiMajorAxis	4.993E-6 // 747 km
		Period			0.007017
		Eccentricity	0
		Inclination		3
		RefPlane		"Equator"
    }
}

Asteroid "Belgica/(1052) Belgica"
{
	ParentBody  "Sol"
	AsterType   "Flora"
	Radius      4.895
	AbsMagn     11.97
	SlopeParam  0.24
	Albedo      0.273
	Obliquity   0   // no data
	RotationPeriod  2.7097
	Orbit
	{
		Epoch           2456800.5
		SemiMajorAxis   2.235833262
		Period          3.343238208
		MeanAnomaly     185.920955
		ArgOfPericenter 297.450734
		Eccentricity    0.143528228
		Inclination     4.6957669
		AscendingNode   99.643919
		PericenterDist  1.91492808
		RefPlane        "Ecliptic"
	}
}

Asteroid "S2012 (1052) 1"
{
	ParentBody  "(1052) Belgica"
	Radius      1.765
	Orbit
	{
		SemiMajorAxis   2.27275962E-07 // 34  km
		Period          0.0053909435   // 1.969  days
		MeanAnomaly     0
		ArgOfPericenter 0
		Eccentricity    0
		Inclination     10 // no data
		AscendingNode   0
		PericenterDist  0
		RefPlane        "Equator"
	}
}

///////////////////////////////////////////////////////////
// 					  Jupiter Trojans 					 //
///////////////////////////////////////////////////////////

Barycenter "Patroclus-Menoetius"
{
	ParentBody	"Sol"
    Orbit
    {
		Epoch			2457600.5
		SemiMajorAxis	5.21730351
		Period			11.91728581
		MeanAnomaly		120.3713226598398
		ArgOfPericenter	308.408114
		Eccentricity	0.139513679
		Inclination		22.0508739
		AscendingNode	44.366003
		PericenterDist	4.4894183
		RefPlane		"Ecliptic"
    }
}

Asteroid "Patroclus/(617) Patroclus"
{
    ParentBody      "Patroclus-Menoetius"
    Radius          53
    AbsMagn         8.19
    SlopeParam      0.15
    Albedo          0.045
    Obliquity       0	// no data
    RotationPeriod  103.02
    Orbit
    {
        SemiMajorAxis   4.54551924314254e-6 //   680 km
        Period          0.0117264657 //   4.283 days
        MeanAnomaly 	0
        Eccentricity    0.02
        ArgOfPericenter 0
        RefPlane        "Equator"
    }
}

Asteroid "Menoetius/(617) Patroclus I Menoetius"
{
    ParentBody  "Patroclus-Menoetius"
    Class		"Asteroid"
    Radius		49
    Albedo		0.045
    Teff		110
    Orbit
    {
		SemiMajorAxis	4.54551924314254e-6 //   680 km
		Period			0.0117264657 //   4.283 days
		MeanAnomaly		0
		ArgOfPericenter	180
		Eccentricity	0.02
		RefPlane		"Equator"
    }
}

Asteroid "Hektor/(624) Hektor"
{
	ParentBody		"Sol"
	Radius			92
	AbsMagn			7.2
	SlopeParam		0.15
	Albedo			0.023
	Obliquity		109
	RotationPeriod	5.981767
	Orbit
	{
		Epoch			2457600.5
		SemiMajorAxis	5.253931748816843
		Period			12.043251623
		MeanAnomaly		87.97459178108841
		ArgOfPericenter	184.219757880388
		Eccentricity	0.02436140433337919
		Inclination		18.16681906042386
		AscendingNode	342.790946146902
		PericenterDist	5.125938593143938
		RefPlane		"Ecliptic"
	}
}

Asteroid "S2006 (624) 1"
{
	ParentBody	"Hektor"
	Class		"Asteroid"
	Radius		6
    Orbit
	{
		SemiMajorAxis	6.40049216957204E-06 //   957.5 km
		Period			0.0081181175 //   2.965079 days
		MeanAnomaly		0
		ArgOfPericenter	0
		Eccentricity	0.31
		Inclination		0
		AscendingNode	0
		RefPlane		"Equator"
    }
}

////////////////////////////////////////////////////////////
//                       Plutinos                         //
////////////////////////////////////////////////////////////

Barycenter "Orcus-Vanth"
{
	ParentBody	"Sol"
    Orbit
    {
		RefPlane        "Ecliptic"
		Epoch            2456401
		MeanMotion       0.00399016
		SemiMajorAxis    39.3679
		Eccentricity     0.221477
		Inclination      20.5438
		AscendingNode    268.423
		ArgOfPericenter  73.7412
		MeanAnomaly      169.685
    }
}

Asteroid	"Orcus/(90482) Orcus"
{
	ParentBody     "Orcus-Vanth"
	AsterType      "Plutino"
	Radius          473.15
	Mass            0.00010261436 // 0.000105788 * 97%
	Obliquity       90.2	// no data
	EquatorAscNode  0	// no data
	RotationPeriod  13.188
	Albedo          0.09
	Color         ( 0.850 0.850 0.850 )
	AbsMagn         2.2
	SlopeParam      0.15
	Orbit
	{
		RefPlane       "Equator"
		Epoch			2454438.78
		Period          0.02609
		SemiMajorAxis   1.8008e-6 // 6.002754e-5 * 3%
		Eccentricity    0.001
		Inclination     90.2
		AscendingNode   50
		ArgOfPericenter 180
		MeanAnomaly     143.1
	}
}

Asteroid	"Vanth/(90482) Orcus I Vanth"
{
	ParentBody     "Orcus-Vanth"
	Radius          140.0
	Mass            3.17364e-6 // 0.000105788 * 3%
	Albedo          0.28
	Color         ( 0.850 0.850 0.850 )
	TidalLocked     true
	Orbit
	{
		RefPlane       "Equator"
		Epoch			2454438.78
		Period          0.02609
		SemiMajorAxis   5.8227e-5 // 6.002754e-5 * 97%
		Eccentricity    0.001
		Inclination     90.2
		AscendingNode   50
		ArgOfPericenter 0
		MeanAnomaly     143.1
	}
}

Barycenter  "Huya system"
{
	ParentBody     "Sol"

	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00400094
		SemiMajorAxis    39.2972
		Eccentricity     0.273954
		Inclination      15.4746
		AscendingNode    169.318
		ArgOfPericenter  67.657
		MeanAnomaly      357.456
	}
}

Asteroid	"Huya/(38628) Huya"
{
	ParentBody     "Huya system"
	AsterType      "Plutino"
	Radius          219.5
	RotationPeriod  13.5
	Obliquity       0	// no data
	EqAscendNode    0	// no data
	AlbedoGeom      0.081
	AbsMagn         4.9
	SlopeParam      0.15
	Orbit
	{
		RefPlane         "Equator"
		Period            0.02609
		SemiMajorAxis     1.177e-6
		Inclination       0	// no data
		AscendingNode     0	// no data
		ArgOfPericenter 180	// no data
		MeanAnomaly       0	// no date
	}
}

Asteroid	"S2012 (38628) 1"
{
	ParentBody     "Huya system"
	Class          "Asteroid"
	Radius          101
	TidalLocked     true
	Orbit
	{
		RefPlane         "Equator"
		Period            0.02609
		SemiMajorAxis     1.082e-5
		Inclination       0	// no data
		AscendingNode     0	// no data
		ArgOfPericenter   0	// no data
		MeanAnomaly       0	// no date
	}
}

// Triple Plutino 1999 TC36
// Data source: Wikipedia

Barycenter	"1999 TC36/(47171) 1999 TC36"
{
	ParentBody "Sol"
	AsterType  "Plutino"
	AbsMagn     4.9
	SlopeParam  0.15
	Mass        2.50187e-6
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.0039531
		SemiMajorAxis    39.6136
		Eccentricity     0.22865
		Inclination      8.40993
		AscendingNode    97.1911
		ArgOfPericenter  295.304
		MeanAnomaly      356.808
	}
}

Barycenter	"1999 TC36 A/(47171) 1999 A"
{
	ParentBody "1999 TC36"
	Mass        2.377e-6 // 14.2e18 kg
	Orbit
	{
		RefPlane        "Equator"
		Epoch            2456401
		Period           0.137722	// 50.302 days
		SemiMajorAxis    2.5265e-6	// 7411 km * (1.0 - mass ratio 0.95)
		Eccentricity     0			// estimation
		Inclination      70			// no data
		AscendingNode    60			// no data
		ArgOfPericenter  100		// no data
		MeanAnomaly      180		// no data
	}
}

Asteroid	"1999 TC36 A1/(47171) 1999 TC36 A1"
{
	ParentBody  "1999 TC36 A"
	AsterType   "Plutino"
	AbsMagn      5.37
	SlopeParam   0.15
	Albedo       0.079
	Radius       132.5		// volume 0.443 of total
	Mass         1.053e-6	// total mass * fractional volume
	Obliquity    70			// no data
	EqAscendNode 60			// no date
	TidalLocked true
	Orbit
	{
		RefPlane        "Equator"
		Epoch            2456401
		Period           0.005202	// 1.9 days
		SemiMajorAxis    3.272e-6	// 867 km * mass ratio
		Eccentricity     0			// estimation
		Inclination      70			// no data
		AscendingNode    60			// no data
		ArgOfPericenter  0			// no data
		MeanAnomaly      0			// no data
	}
}

Asteroid	"1999 TC36 A2/(47171) 1999 TC36 A2"
{
	ParentBody  "1999 TC36 A"
	AsterType   "Plutino"
	AbsMagn      5.37
	SlopeParam   0.15
	Albedo       0.079
	Radius       143.0		// volume 0.557 of total
	Mass         1.324e-6	// total mass * fractional volume
	Obliquity    70			// no data
	EqAscendNode 60			// no date
	TidalLocked true
	Orbit
	{
		RefPlane        "Equator"
		Epoch            2456401
		Period           0.005202	// 1.9 days
		SemiMajorAxis    2.602e-6	// 867 km * mass ratio
		Eccentricity     0			// estimation
		Inclination      70			// no data
		AscendingNode    60			// no data
		ArgOfPericenter  180		// no data
		MeanAnomaly      0			// no data
	}
}

Asteroid	"1999 TC36 B/(47171) 1999 TC36 B"
{
	ParentBody  "1999 TC36"
	AbsMagn      5.0
	SlopeParam   0.15
	Albedo       0.079
	Radius       66.0
	Mass         1.2487e-7	// 0.746e18 kg
	Obliquity    70			// no data
	EqAscendNode 60			// no date
	TidalLocked true
	Orbit
	{
		RefPlane        "Equator"
		Epoch            2456401
		Period           0.137722	// 50.302 days
		SemiMajorAxis    4.70665e-5	// 7411 km * (1.0 - mass ratio 0.95)
		Eccentricity     0			// estimation
		Inclination      70			// no data
		AscendingNode    60			// no data
		ArgOfPericenter  100		// no data
		MeanAnomaly      0			// no data
	}
}

////////////////////////////////////////////////////////////
//              Other Kuiper belt objects                 //
////////////////////////////////////////////////////////////

Asteroid	"Quaoar/(50000) Quaoar"
{
	ParentBody "Sol"
	AsterType  "Cubewano"
	Radius      625
	Mass        2.3e-4
	Oblateness  0.12
	Albedo      0.12
	Color     ( 0.600 0.450 0.350 )
	AbsMagn     2.48
	SlopeParam  0.15
	AlbedoGeom  0.199
	RotationPeriod 17.6788
	Orbit
	{
		Epoch            2456401
		MeanMotion       0.00347369
		SemiMajorAxis    43.1793
		Eccentricity     0.0364151
		Inclination      7.99306
		AscendingNode    188.96
		ArgOfPericenter  162.952
		MeanAnomaly      276.475
	}
}

Asteroid	"Weywot/(50000) Quaoar I Weywot"
{
	ParentBody     "(50000) Quaoar"
	Class			"Asteroid"
	Radius          40.0
	Mass            1.17e-7 // 1/2000 of Quaoar
	Albedo          0.28
	Orbit
	{
		Period         0.034054
		SemiMajorAxis  9.6926429e-5
		Eccentricity   0.14
		Inclination    14
		RefPlane      "Equator"
	}
}

Barycenter "2002 VT130 system"
{
    ParentBody "Sol" //Volume 12.79E6
    Orbit
    {
		Epoch			2457400.5
		SemiMajorAxis	42.623
		Period			278.271
		Eccentricity	0.02952
		Inclination		1.1615
		AscendingNode	334.86
		ArgOfPericenter	350.7
		MeanAnomaly		105.85
		RefPlane		"Ecliptic"
    }
}

Asteroid "2002 VT130"
{
    ParentBody "2002 VT130 system"
    Class      "Asteroid"
    Radius      125.5 //Volume 8.28E6 = 64.74%
	TidalLocked true
    Orbit
    {
		Epoch           2457400.5
		SemiMajorAxis   5.867E-6 // 2490 km
		Period          0.026831
		Eccentricity    0
		Inclination     0
		ArgOfPericenter 0
		MeanAnomaly     0
		RefPlane       "Equator"
    }
}

Asteroid "S2008 2002 VT130 1"
{
    ParentBody "2002 VT130 system"
    Class      "Asteroid"
    Radius      102.5 //Volume 4.51E6 = 35.26%
	TidalLocked true
    Orbit
    {
		Epoch			2457400.5
		SemiMajorAxis	1.077E-5 // 2490 km
		Period			0.026831
		Eccentricity    0
		Inclination  	0
		ArgOfPericenter	180
		MeanAnomaly		0
		RefPlane		"Equator"
    }
}

Barycenter "2002 XH91 system"
{
    ParentBody "Sol" //Volume 1.722E7
    Orbit
    {
		Epoch			2457400.5
		SemiMajorAxis	44.182
		Period			293.68
		Eccentricity	0.08389
		Inclination		4.98507
		AscendingNode	80.4332
		ArgOfPericenter	183.96
		MeanAnomaly		212.86
		RefPlane		"Ecliptic"
    }
}

Asteroid "2002 XH91"
{
    ParentBody "2002 XH91 system"
    Class      "Asteroid"
    Radius      149 //Volume 1.39E7 = 80.72%
	TidalLocked true
    Orbit
    {
		Epoch			2457400.5
		SemiMajorAxis	2.56E-5 // 19900 km
		Period			0.520202
		Eccentricity	0
		Inclination		0
		ArgOfPericenter	0
		MeanAnomaly		0
		RefPlane		"Equator"
    }
}

Asteroid "S2008 2002 XH91 1"
{
    ParentBody "2002 XH91 system"
    Class      "Asteroid"
    Radius      92.5 //Volume 3.32E6 = 19.28%
	TidalLocked true
    Orbit
    {
		Epoch   		2457400.5
		SemiMajorAxis	1.07E-4 // 19900 km
		Period			0.520202
		Eccentricity	0
		Inclination		0
		ArgOfPericenter	180
		MeanAnomaly		0
		RefPlane		"Equator"
    }
}

Barycenter "Varda-Ilmare"
{
    ParentBody "Sol"
    Orbit
    {
		Epoch			2457400.5
		SemiMajorAxis	45.6342
		Period			308.279
		Eccentricity	0.141677
		Inclination		21.5045
		AscendingNode	184.032
		ArgOfPericenter	184.310
		MeanAnomaly		263.701
		RefPlane		"Ecliptic"
    }
}

Asteroid "Varda/(174567) Varda"  // 2003 MW12
{
    ParentBody  	"Varda-Ilmare"
    Radius      	352.5 //Volume 1.83E8 = 88.15% of total
    Mass			3.891E-5 //Assuming a density of 1.27 g/cm^3
    Oblateness		0
    RotationPeriod	5.91
    Obliquity		100
    EqAscendNode	0
    Surface
    {
		colorSea       (0.144, 0.084, 0.045, 1.000)
		colorShelf     (0.189, 0.116, 0.065, 1.000)
		colorBeach     (0.305, 0.151, 0.069, 0.200)
		colorDesert    (0.333, 0.169, 0.082, 0.200)
		colorLowland   (0.361, 0.178, 0.086, 0.200)
		colorUpland    (0.389, 0.213, 0.102, 0.200)
		colorRock      (0.920, 0.870, 0.660, 0.150)
		colorSnow      (1.000, 1.000, 1.000, 0.154)
		colorLowPlants (0.361, 0.178, 0.086, 0.200)
		colorUpPlants  (0.389, 0.213, 0.102, 0.200)
    }
    Orbit
    {
		SemiMajorAxis	3.8022E-6 // separation 4800 km
		Period			0.015745
		Eccentricity    0.017
		Inclination		100
		ArgOfPericenter	0
		MeanAnomaly		0
        AscendingNode   0
		RefPlane		"Ecliptic"
    }
}

Asteroid "Ilmare/(174567) Varda I Ilmare"
{
    ParentBody  "Varda-Ilmare"
    Radius      180.5 //Volume 2.46E7 = 11.85% of total
    Mass  		5.231E-6 //Assuming a density of 1.27 g/cm^3
	TidalLocked true
    Surface
    {
		colorSea       (0.144, 0.084, 0.045, 1.000)
		colorShelf     (0.189, 0.116, 0.065, 1.000)
		colorBeach     (0.305, 0.151, 0.069, 0.200)
		colorDesert    (0.333, 0.169, 0.082, 0.200)
		colorLowland   (0.361, 0.178, 0.086, 0.200)
		colorUpland    (0.389, 0.213, 0.102, 0.200)
		colorRock      (0.920, 0.870, 0.660, 0.150)
		colorSnow      (1.000, 1.000, 1.000, 0.154)
		colorLowPlants (0.361, 0.178, 0.086, 0.200)
		colorUpPlants  (0.389, 0.213, 0.102, 0.200)
    }
    Orbit
    {
		SemiMajorAxis	2.8885E-5 // separation 4800 km
		Period			0.015745
		Eccentricity	0.017
		Inclination		100
		ArgOfPericenter	180
		MeanAnomaly		0
        AscendingNode	0
		RefPlane		"Ecliptic"
    }
}

Barycenter "Teharonhiawako-Sawiskera"
{
    ParentBody "Sol" //Volume 4.07E6
    Orbit
    {
		Epoch			2457400.5
		SemiMajorAxis	43.894
		Period   		290.816
		Eccentricity    0.029879
		Inclination 	2.58688
		AscendingNode  	304.84
		ArgOfPericenter	233.57
		MeanAnomaly  	159.46
		RefPlane  		"Ecliptic"
    }
}

Asteroid "Teharonhiawako/(88611) Teharonhiawako"
{
    ParentBody  	"Teharonhiawako-Sawiskera"
    Class   	    "Asteroid"
	AsterType       "Cubewano"
    Radius  	    89 //Volume 2.95E6 = 72.48%
    Mass  			2.962E-7
	Obliquity       144.42
	EqAscendNode    54.22
    RotationPeriod  4.7526
	AlbedoGeom      0.145
	AbsMagn         6.0
    Orbit
    {
		Epoch		 	2457400.5
		SemiMajorAxis	5.0912E-5 // 27670 km
		Period   		2.269068
		Eccentricity    0.2494
		Inclination 	144.42
		AscendingNode   54.22
		ArgOfPericenter 200.1
		MeanAnomaly  	296.2
		RefPlane       "Ecliptic"
    }
}

Asteroid "Sawiskera/(88611) Teharonhiawako I Sawiskera"
{
    ParentBody		"Teharonhiawako-Sawiskera"
    Class 	   	  	"Asteroid"
    Radius			64.5 //Volume 1.12E6 = 27.52%
    Mass	  		6.71E-8
	Obliquity       144.42
	EqAscendNode    54.22
    Orbit
    {
		Epoch			2457400.5
		SemiMajorAxis	1.34088E-4 // 27670 km
		Period   		2.269068
		Eccentricity    0.2494
		Inclination 	144.42
		AscendingNode   54.22
		ArgOfPericenter	20.1
		MeanAnomaly  	296.2
		RefPlane       "Ecliptic"
    }
}

////////////////////////////////////////////////////////////
//                  Near-Earth Objects                    //
////////////////////////////////////////////////////////////

Asteroid "Apollo/(1862) Apollo"
	{
	ParentBody 		"Sol"
	AsterType		"Apollo"
	Radius			0.75
	Mass			5.6074e-13
	AbsMagn			16.25
	RotationPeriod	3.065
	Albedo			0.25
	SlopeParam		0.09
	Teff			214
	Orbit
    {
        Epoch           2457400
		Period			1.78
        MeanMotion      0.55288190
        SemiMajorAxis   1.4702055
        Eccentricity    0.5599037
        Inclination     6.35478
        AscendingNode   35.63970
        ArgOfPericenter 285.97279
		PericenterDist	0.6470364
        MeanAnomaly     283.82138
        RefPlane     	"Ecliptic"
    }
}

Asteroid "S2005 (1862) 1"
{
    ParentBody  "(1862) Apollo"
    Class		"Asteroid"
    AsterType	"Apollo"
    Radius		0.04
    DiscDate	"2005.10.29"
    Orbit
    {
        SemiMajorAxis   2.50672e-8
        Period          0.00312121664
        MeanAnomaly     0
        ArgOfPericenter	0
        RefPlane		"Equator"
    }
}

Asteroid "2001 SN263/(153591) 2001 SN263"
{
	ParentBody		"Sol"
	AsterType		"Amor"
	Radius			1.325
	Mass			2.512E-12
	AbsMagn			16.9
	RotationPeriod	3.423
	Obliquity		-80
	EqAscendNode	309
	Orbit
	{
		Epoch			2457400.5
		MeanMotion		0.35
		SemiMajorAxis	1.9868
		Period			2.80066
		Eccentricity	0.47842
		Inclination		6.6858
		AscendingNode	325.8316
		ArgOfPericenter	172.8625
		MeanAnomaly		297.3598
		RefPlane		"Ecliptic"
	}
}

Asteroid "2001 SN263 Gamma"
{
	ParentBody	"2001 SN263"
	Class		"Asteroid"
	AsterType	"Amor"
	Radius		0.17
	Mass		1.6744E-14
	AbsMagn		16.9
	Orbit
	{
		Epoch			2457400.5
		SemiMajorAxis	2.54E-8
		Period			0.001878
		Eccentricity	0.016
		Inclination		7
		RefPlane		"Equator"
	}
}

Asteroid "2001 SN263 Beta"
{
	ParentBody	"2001 SN263"
	Class		"Asteroid"
	AsterType	"Amor"
	Radius		0.385
	Mass		4.0186E-14
	AbsMagn		17.7
	Orbit
	{
		Epoch			2457400.5
		SemiMajorAxis	1.11E-7
		Period			0.017043
		Eccentricity	0.015
		Inclination		-7
		RefPlane		"Equator"
    }
}

Asteroid "1994 CC/(136617) 1994 CC"
{
	ParentBody		"Sol"
	AsterType		"Apollo"
	Radius			0.35
	Mass			4.354E-14
	AbsMagn			17.7
	Obliquity       95
	EqAscendNode    0	// no data
	RotationPeriod	2.3886
	Orbit
	{
		Epoch			2457400.5
		MeanMotion		0.548
		SemiMajorAxis	1.6378
		Period			2.096018
		Eccentricity	0.41695
		Inclination		4.6806
		AscendingNode	268.565
		ArgOfPericenter	24.8612
		MeanAnomaly		38.86802
		RefPlane		"Ecliptic"
	}
}

Asteroid "1994 CC Beta"
{
	ParentBody	"1994 CC"
	Class		"Asteroid"
	AsterType	"Apollo"
	Radius		0.065
	Mass		1.0046E-15
	AbsMagn		17.7
	Orbit
	{
		Epoch			2457400.5
		SemiMajorAxis	1.14E-8
		Period			0.003403
		Eccentricity	0.002
		Inclination		95
		AscendingNode	0	// no data
		RefPlane		"Ecliptic"
	}
}

Asteroid "1994 CC Gamma"
{
	ParentBody	"1994 CC"
	Class		"Asteroid"
	AsterType	"Apollo"
	Radius		0.045
	Mass		1.6744E-16
	AbsMagn		17.7
	Orbit
	{
		Epoch			2457400.5
		SemiMajorAxis	4.08E-8
		Period			0.022933
		Eccentricity	0.192
		Inclination		79
		AscendingNode	0	// no data
		RefPlane		"Ecliptic"
	}
}

Barycenter "Heracles system"
{
	ParentBody	"Sol"
    Orbit
    {
		Epoch			2457600.5
		SemiMajorAxis	1.833500481
		Period			2.482731444
		MeanAnomaly		294.9388476797092
		ArgOfPericenter	227.705989
		Eccentricity	0.772242011
		Inclination		9.0355614
		AscendingNode	309.56692
		PericenterDist	0.417594382
		RefPlane   		"Ecliptic"
    }
}

Asteroid "Heracles/(5143) Heracles"
{
    ParentBody      "Heracles system"
    Radius          1.8
    AbsMagn         13.8
    SlopeParam      0.15
    Albedo          0.4
    Obliquity       0
	EqAscendNode    0
    RotationPeriod  2.706
    Orbit
    {
        SemiMajorAxis   9.90020483e-9 //   4 km
        Period          0.0017681418 //   0.6458 days
        MeanAnomaly     0
        ArgOfPericenter 0
        RefPlane  		"Equator"
    }
}

Asteroid "S2011 (5143) 1"
{
    ParentBody  "Heracles system"
    Class		"Asteroid"
    Radius		0.3
    SlopeParam	0.15
    Albedo		0.4
	TidalLocked true
    Orbit
    {
		SemiMajorAxis	2.666039412E-08 //   4 km
		Period			0.0017681418 //   0.6458 days
		MeanAnomaly		0
		ArgOfPericenter	180
		RefPlane		"Equator"
    }
}

Barycenter "Didymos system"
{
    ParentBody  "Sol"
    Orbit
    {
		Epoch			2457400.5
		SemiMajorAxis	1.6443
		Period			2.108544
        Eccentricity	0.38388
        Inclination		3.4078
        AscendingNode	73.228
        ArgOfPericenter	319.232
        MeanAnomaly		283.85
		RefPlane		"Ecliptic"
    }
}

Asteroid "Didymos/(65803) Didymos"
{
    ParentBody      "Didymos system"
    AsterType       "Apollo"
    Radius          0.375
    Mass            8.824E-14
    RotationPeriod  2.2593
    Obliquity       19
    EqAscendNode    157
    SlopeParam      0.15
    Orbit
    {
        Epoch           2457400.5
        SemiMajorAxis   3.75175e-11 // 7.888E-9
        Period          0.001357
        Eccentricity    0.06
        Inclination     0
        AscendingNode   0
        MeanAnomaly     0
        ArgOfPericenter 0
        RefPlane        "Equator"
    }
}

Asteroid "S2003 (65803) 1"
{
    ParentBody  "Didymos system"
    Class		"Asteroid"
    AsterType	"Apollo"
    Radius		0.075
    Mass		4.217E-16
    TidalLocked true
    Orbit
    {
        Epoch           2457400.5
        SemiMajorAxis   7.85048e-9 // 7.888E-9
        Period          0.001357
        Eccentricity    0.06
        Inclination     0
        AscendingNode   0
        MeanAnomaly     0
        ArgOfPericenter 180
        RefPlane        "Equator"
    }
}

Barycenter "2004 BL86 system"
{
	ParentBody  "Sol"
	Orbit
	{
		Epoch			2457400.5
        SemiMajorAxis	1.5022
		Period			1.8412
        Eccentricity	0.40307
        Inclination		23.744
        AscendingNode	126.72
        ArgOfPericenter	311.25
        MeanAnomaly		354.03
        RefPlane		"Ecliptic"
    }
}

Asteroid "2004 BL86/(357439) 2004 BL86"
{
    ParentBody		"2004 BL86 system"
    AsterType		"Apollo"
    Radius			0.1625
	Obliquity		0	// no data
    RotationPeriod	2.6205
	Orbit
    {
		Epoch			2457400.5
        SemiMajorAxis   1.479604574E-9  // 3.34E-9
        Period          0.001643
		MeanAnomaly		0
        Eccentricity    0
        Inclination     0
		ArgOfPericenter	0
        RefPlane       "Equator"
    }
}

Asteroid "S2015 (357439) 1"
{
    ParentBody  "2004 BL86 system"
    Class       "Asteroid"
    AsterType   "Apollo"
    Radius      0.035
	TidalLocked true
    Orbit
    {
        Epoch           2457400.5
        SemiMajorAxis   3.321411288E-9   // 3.34E-9
        Period          0.001643
		MeanAnomaly		0
        Eccentricity    0
        Inclination     0
		ArgOfPericenter	180
        RefPlane       "Equator"
    }
}

Barycenter "Sekhmet system"
{
	ParentBody	 "Sol"
	Orbit
	{
		Epoch			2457600.5
		SemiMajorAxis	0.947461319
		Period			0.922253761
		MeanAnomaly		232.9560028829811
		ArgOfPericenter	37.435861
		Eccentricity	0.29623718
		Inclination		48.969357
		AscendingNode	58.5495966
		PericenterDist	0.66678805
		RefPlane		"Ecliptic"
    }
}

Asteroid "Sekhmet/(5381) Sekhmet"
{
    ParentBody      "Sekhmet system"
    AsterType       "Aten"
    Radius          0.5
    AbsMagn         16.5
    SlopeParam      0.15
    Albedo          0.25
    Obliquity       0	// no data
    RotationPeriod  2.7
    Orbit
    {
        SemiMajorAxis   2.70637909000897E-10 //   1.54 km
        Period          0.0014259032 //   0.5208 days
        MeanAnomaly     0
        ArgOfPericenter 0
        Eccentricity    0
        Inclination     0
        AscendingNode   0
        RefPlane        "Equator"
    }
}

Asteroid "S2003 (5381) 1"
{
    ParentBody      "Sekhmet system"
    Class           "Asteroid"
    AsterType 		"Aten"
    Radius          0.15
	TidalLocked     true
    Orbit
    {
        SemiMajorAxis   1e-8 //   1.54 km
        Period          0.0014259032 //   0.5208 days
        MeanAnomaly     0
        ArgOfPericenter 180
        Eccentricity    0
        Inclination     0
        AscendingNode   0
        RefPlane        "Equator"
    }
}

Barycenter "Hermes system"
{
	ParentBody	 "Sol"
    Orbit
    {
		Epoch			2457600.5
		SemiMajorAxis	1.655109415
		Period			2.129355952
		MeanAnomaly		330.6278831909314
		ArgOfPericenter	92.7131629
		Eccentricity	0.62413501
		Inclination		6.068238
		AscendingNode	34.2429732
		PericenterDist	0.622097684
		RefPlane		"Ecliptic"
    }
}

Asteroid "Hermes/(69230) Hermes"
{
	ParentBody	 "Hermes system"
    AsterType   "Apollo"
    Radius      0.3
    AbsMagn     17.5
    SlopeParam  0.15
    Albedo      0.25
	TidalLocked true
	Obliquity	0	// Arecibo data 2003
	Oblateness	0   // oblateness  0.8 reported of one component in 2006; doesn't look nice at the moment, though
    RotationPeriod  13.894
    Orbit
    {
		SemiMajorAxis	7.2e-09 // 1.1 km
		Period			0.0015849757 // 0.5789 days
		MeanAnomaly		0
		ArgOfPericenter	0
		Eccentricity	0
		Inclination		10	// no data
		RefPlane		"Equator"
    }
}

Asteroid "S2003 (69230) 1"
{
    ParentBody      "Hermes system"
    Class           "Asteroid"
    AsterType       "Apollo"
    Radius          0.27
	TidalLocked		true
	Obliquity		0	// Arecibo data
	Oblateness		0
    RotationPeriod  13.892
    Orbit
    {
		SemiMajorAxis	7.3530458345e-09 // 1.1 km
		Period			0.0015849757 // 0.5789 days
		MeanAnomaly		0
		ArgOfPericenter	180
		Eccentricity	0
		Inclination		10	// no data
		RefPlane		"Equator"
    }
}

Barycenter "Dionysus system"
{
	ParentBody	 "Sol"
    Orbit
    {
		Epoch			2457600.5
		SemiMajorAxis	2.197503781
		Period			3.257636697
		MeanAnomaly		304.8556796126329
		ArgOfPericenter	204.192352
		Eccentricity	0.542416439
		Inclination		13.5501872
		AscendingNode	82.161864
		PericenterDist	1.005541605
		RefPlane	 	"Ecliptic"
    }
}

Asteroid "Dionysus/(3671) Dionysus"
{
    ParentBody      "Dionysus system"
    AsterType       "Amor"
    Radius          0.715
    AbsMagn         16.4
    SlopeParam      0.15
    Albedo          0.179
    RotationPeriod  2.705
    Orbit
    {
        SemiMajorAxis   1.87988908655175E-10	  //   3.4 km
        Period          0.0031650232	  //   1.156 days
        MeanAnomaly     0
        ArgOfPericenter 0
        Eccentricity    0.07
        Inclination     0
        AscendingNode   0
        RefPlane        "Equator"
    }
}

Asteroid "S1997 (3671) 1"
{
    ParentBody  "Dionysus system"
    Class       "Asteroid"
    AsterType   "Amor"
    Radius      0.145
    TidalLocked true
    Orbit
    {
        SemiMajorAxis   2.25396073070575E-08	  //   3.4 km
        Period          0.0031650232	  //   1.156 days
        MeanAnomaly     0
        ArgOfPericenter 180
        Eccentricity    0.07
        Inclination     0
        AscendingNode   0
        RefPlane        "Equator"
    }
}

Asteroid "2002 AM31/(153958) 2002 AM31"
{
    ParentBody		"Sol"
    Class			"Asteroid"
    AsterType		"Apollo"
    Radius			0.225
    Mass			2.411E-14 //Assuming a density of 3 g/cm^3
    RotationPeriod  2.817
    Orbit
    {
		Epoch			2457400.5
		SemiMajorAxis	1.703
		Period			2.2225
		Eccentricity	0.45168
		Inclination		4.6448
		AscendingNode	144.41
		ArgOfPericenter	197.83
		MeanAnomaly		282.02
		RefPlane		"Ecliptic"
    }
}

Asteroid "S2012 (153958) 1"
{
    ParentBody  "2002 AM31"
    Class       "Asteroid"
    AsterType   "Apollo"
    Radius      0.055
    Mass		3.501E-16 //Assuming a density of 3 g/cm^3
    Orbit
    {
		SemiMajorAxis   1E-8 // separation 1.5 km
		Period          0.003001
		Eccentricity    0.45
		Inclination     0
		RefPlane        "Equator"
    }
}

Barycenter "Ishtar system"
{
	ParentBody "Sol"
    Orbit
    {
		Epoch			2457600.5
		SemiMajorAxis	1.980767174
		Period			2.787779032
		MeanAnomaly		291.3164160244695
		ArgOfPericenter	354.70897
		Eccentricity	0.390732639
		Inclination		8.3009094
		AscendingNode	102.686712
		PericenterDist	1.20681679
		RefPlane	 	"Ecliptic"
    }
}

Asteroid "Ishtar/(7088) Ishtar"
{
	ParentBody      "Ishtar system"
	AsterType       "Amor"
	Radius          0.695
	AbsMagn         16.7
	SlopeParam      0.15
	Albedo          0.16
	Obliquity       0	// no data
	RotationPeriod  2.769
	Orbit
	{
		SemiMajorAxis   1.26769242087516E-09 //   2,8 km
		Period          0.0023556972 //   0.8604 days
		MeanAnomaly     0
		ArgOfPericenter 0
		Eccentricity    0
		Inclination     0
		RefPlane        "Equator"
	}
}

Asteroid "S2006 (7088) Ishtar 1"
{
    ParentBody  "Ishtar system"
    Class       "Asteroid"
    AsterType   "Amor"
    Radius      0.29
    AbsMagn     14.8
	TidalLocked true
    Orbit
    {
        SemiMajorAxis   1.74491515214765E-08 //   2,8 km
        Period          0.0023556972 //   0.8604 days
        MeanAnomaly     0
        ArgOfPericenter 180
        Eccentricity    0
        Inclination     0
        RefPlane        "Equator"
    }
}

Asteroid "1990 TR/(5646) 1990 TR"
{
	ParentBody		"Sol"
	AsterType 		"Amor"
	Radius			1.34
	AbsMagn			15.2
	SlopeParam		0.15
	Albedo			0.454
	RotationPeriod	3.1999
    Orbit
    {
		Epoch			2457600.5
		SemiMajorAxis	2.142140459
		Period			3.135307332
		MeanAnomaly		92.94605447876019
		ArgOfPericenter	335.674924
		Eccentricity	0.437049083
		Inclination		7.914407
		AscendingNode	14.140494
		PericenterDist	1.20591994
		RefPlane	 	"Ecliptic"
    }
}

Asteroid "S2012 (5646) 1"
{
	ParentBody "1990 TR"
	Class 		"Asteroid"
	AsterType 	"Amor"
	Radius		0.24
    Orbit
    {
		SemiMajorAxis	3.40913943236E-08 //   5.1 km
		Period			0.0022212658 //   0.8113 days
		MeanAnomaly		0
		ArgOfPericenter	180
		Eccentricity	0
		RefPlane	 	"Equator"
    }
}


///////////////////////////////////////////////////////////
//               Mars crossing Asteroids                 //
///////////////////////////////////////////////////////////

Asteroid "1999 KW4/(66391) 1999 KW4"
{
	ParentBody		"Sol"
	Class			"Asteroid"
	AsterType		"Aten"
	Radius			0.75
	Mass			4.0186E-13
	AbsMagn			16.5
	RotationPeriod	2.7645
	Orbit
	{
		Epoch			2457400.5
        MeanMotion		1.9148
        SemiMajorAxis	0.64228
		Period			0.514759
        Eccentricity	0.68847
        Inclination		38.884
        AscendingNode	244.919
        ArgOfPericenter	192.619
        MeanAnomaly		290.088
        RefPlane		"Ecliptic"
    }
}

Asteroid "S2001 (66391) 1"
{
	ParentBody	"1999 KW4"
	Class		"Asteroid"
	AsterType	"Aten"
	Radius		0.25
	Mass		2.2604E-14
	AbsMagn		14
	Orbit
	{
		Epoch			2457400.5
		SemiMajorAxis	1.74E-8
		Period			0.001987
		Eccentricity	0
		Inclination		0
		RefPlane		"Equator"
    }
}

Barycenter "1998 RO1 system"
{
	ParentBody  "Sol"
	Orbit
    {
		Epoch			2457400.5
		MeanMotion		0.515
		SemiMajorAxis	0.990924
		Period			0.986427
		Eccentricity	0.720129
		Inclination		22.68
		AscendingNode	351.875
		ArgOfPericenter	151.13
		MeanAnomaly		109.479
		RefPlane		"Ecliptic"
	}
}

Asteroid "1998 RO1/(66063) 1998 RO1"
{
	ParentBody		"1998 RO1 system"
	AsterType		"Aten"
	Radius			0.4
	Mass			6.969E-14
	RotationPeriod	2.492
	Obliquity		37
	EqAscendNode	277
	Orbit
	{
		Epoch			2457400.5
		SemiMajorAxis	5.52185126674e-9
		Period			0.001659
		Inclination		37
		Eccentricity	0.05
		ArgOfPericenter	0
		MeanAnomaly		0
		AscendingNode	277
		RefPlane		"Equator"
	}
}

Asteroid "1998 RO1 Beta"
{
	ParentBody	"1998 RO1 system"
	Class		"Asteroid"
	AsterType	"Aten"
	Radius		0.19
	Mass		5.16E-15
	TidalLocked true
	Orbit
	{
		Epoch			2457400.5
		SemiMajorAxis	7.75361201786e-9
		Period			0.001659
		MeanAnomaly		0
		Eccentricity	0.05
		ArgOfPericenter	180
		Inclination		37
		AscendingNode	277
		RefPlane		"Equator"
	}
}

Barycenter "Litva system"
{
	ParentBody		"Sol"
	Orbit
	{
		Epoch			2457400.5
		SemiMajorAxis	1.9044
		Period			2.6282
		Eccentricity	0.13794
		Inclination		22.908
		AscendingNode	182.61
		ArgOfPericenter	283.98
		MeanAnomaly		26.575
		RefPlane		"Ecliptic"
	}
}

Barycenter "Litva-S2009"
{
    ParentBody      "Litva system"
    Orbit
    {
        Epoch           2457400.5
        SemiMajorAxis   6.375e-8
        Period          0.585912
        Eccentricity    0
        Inclination     0
		AscendingNode   0
        ArgOfPericenter 0
        MeanAnomaly     0
        RefPlane        "Equator"
    }
}

Asteroid "Litva/(2577) Litva"
{
	ParentBody		"Litva-S2009"
	Radius			2
	RotationPeriod	2.813
	Obliquity       0
	EqAscendNode    0
	Orbit
	{
		SemiMajorAxis	5.77e-9
		Period			0.004093
		Eccentricity	0
		Inclination		0
		AscendingNode   0
		ArgOfPericenter	0
		MeanAnomaly		0
		RefPlane		"Equator"
	}
}

Asteroid "S2009 (2577) 1"
{
	ParentBody		"Litva-S2009"
	Class			"Asteroid"
	Radius			0.7
	RotationPeriod	5.6842
	Obliquity       0
	EqAscendNode    0
	Orbit
	{
		SemiMajorAxis	1.346e-7
		Period			0.004093
		Eccentricity	0
		Inclination		0
		AscendingNode   0
		ArgOfPericenter	180
		MeanAnomaly		0
		RefPlane		"Equator"
	}
}

Asteroid "S2012 (2577) 1"
{
    ParentBody      "Litva system"
    Class           "Asteroid"
    Radius          0.6
	Obliquity       0
	EqAscendNode    0
    Orbit
    {
        Epoch           2457400.5
        SemiMajorAxis   2.463e-6
        Period          0.585912
        Eccentricity    0
        Inclination     0
		AscendingNode   0
        ArgOfPericenter 180
        MeanAnomaly     0
        RefPlane        "Equator"
    }
}

Asteroid "Carlwirtz/(26074) Carlwirtz"
{
    ParentBody  "Sol"
    Class       "Asteroid"
    Radius      1.81
    RotationPeriod  2.4593
    Orbit
    {
        Epoch            2457400.5
        SemiMajorAxis    1.811
		Period           2.4372
        Eccentricity     0.088958
        Inclination      31.614
        AscendingNode    102.83
        ArgOfPericenter  73.385
        MeanAnomaly      190.66
        RefPlane       "Ecliptic"
    }
}

Asteroid "S2013 (26074) 1"
{
    ParentBody  "(26074) Carlwirtz"
    Class       "Asteroid"
    Radius      0.2
    Orbit
    {
        SemiMajorAxis   4.08E-8
        Period          0.001838
		Inclination		0
        RefPlane       "Equator"
    }
}

Asteroid "Eureka/(5261) Eureka"
{
    ParentBody		"Sol"
    Class			"Asteroid"
    Radius			0.595
    RotationPeriod  2.6902
    Orbit
    {
        Epoch            2457400.5
        SemiMajorAxis    1.5235
		Period           1.880519722
        Eccentricity     0.064859
        Inclination      20.28
        AscendingNode    245.06
        ArgOfPericenter  95.47
        MeanAnomaly      295.62
        RefPlane       "Ecliptic"
    }
}

Asteroid "S2011 (5261) 1"
{
    ParentBody  "(5261) Eureka"
    Class       "Asteroid"
    Radius      0.23
    Orbit
    {
        SemiMajorAxis   1.4E-8
        Period          0.001931
		Inclination  	0
        RefPlane       "Equator"
    }
}

Asteroid "1993 QO/(16635) 1993 QO"
{
    ParentBody      "Sol"
    Class           "Asteroid"
    Radius          2.305
    RotationPeriod  7.622
    Orbit
    {
        Epoch			2457400.5
        SemiMajorAxis	2.2983
		Period			3.4843
        Eccentricity	0.28424
        Inclination		21.943
        AscendingNode	313.81
        ArgOfPericenter	77.948
        MeanAnomaly		4.7118
        RefPlane		"Ecliptic"
    }
}

Asteroid "S2007 (16635) 1"
{
    ParentBody      "1993 QO"
    Class           "Asteroid"
    Radius          0.72
    RotationPeriod  2.2083
	Obliquity       0
    Orbit
    {
        SemiMajorAxis   8.022E-8
        Period          0.00368
		Inclination  	0
        RefPlane       "Equator"
    }
}

Barycenter "Atami system"
{
    ParentBody "Sol"
    Orbit
    {
		Epoch			2457400.5
		SemiMajorAxis   1.9476
		Period   		2.7181
		Eccentricity    0.2555
		Inclination		13.085
		AscendingNode	213.37
		ArgOfPericenter	206.63
		MeanAnomaly		35.579
		RefPlane		"Ecliptic"
    }
}

Asteroid "Atami A/(1139) Atami A"
{
    ParentBody  "Atami system"
    Class       "Asteroid"
    Radius      3 //Volume 113.1 = 63.34% of total
    Mass  		3.031E-11 //Assuming a density of 1.6 g/cm^3
    RotationPeriod  27.45
    Obliquity       0
    Orbit
    {
		SemiMajorAxis	3.6759E-8 // separation    15 km
		Period			0.003132
		Eccentricity	0
		Inclination		0
		ArgOfPericenter	0
		MeanAnomaly		0
		RefPlane		"Equator"
    }
}

Asteroid "Atami B/(1139) Atami B"
{
    ParentBody      "Atami system"
    Class           "Asteroid"
    Radius          2.5 //Volume 65.45 = 36.66% of total
    Mass            1.753E-11 //Assuming a density of 1.6 g/cm^3
    RotationPeriod  27.45
    Obliquity       0
    Orbit
    {
		SemiMajorAxis	6.351E-8 // separation    15 km
		Period			0.003132
		Eccentricity	0
		Inclination		0
		ArgOfPericenter	180
		MeanAnomaly		0
		RefPlane		"Equator"
    }
}

Asteroid "Roddy/(3873) Roddy"
{
	ParentBody 		"Sol"
	Radius			3.625
	AbsMagn			12.7
	SlopeParam		0.15
	Albedo			0.512
	RotationPeriod	2.479
    Orbit
    {
		RefPlane	 	"Ecliptic"
		Epoch			2457600.5
		SemiMajorAxis	1.892048452
		Period			2.60259495
		MeanAnomaly		349.2252281489226
		ArgOfPericenter	267.548484
		Eccentricity	0.133696579
		Inclination		23.3549639
		AscendingNode	250.077283
		PericenterDist	1.63908805
    }
}

Asteroid "S2012 (3873) 1"
{
    ParentBody  "(3873) Roddy"
    Class       "Asteroid"
    Radius      0.98

    Orbit
    {
        RefPlane        "Equator"
        SemiMajorAxis   9.17709622233744E-08 //   14 km
        Period          0.0021949819 //   0.8017 days
        MeanAnomaly     0
        ArgOfPericenter 180
        Eccentricity    0
        Inclination     0
    }
}

Barycenter "Gavrilin system"
{
	ParentBody	 "Sol"
	Orbit
    {
		Epoch			2457600.5
		SemiMajorAxis	2.370459627
		Period			3.649698985
		MeanAnomaly		176.2015129076027
		ArgOfPericenter	113.133971
		Eccentricity	0.318396091
		Inclination		21.816668
		AscendingNode	278.356932
		PericenterDist	1.61571455
		RefPlane	 	"Ecliptic"
    }
}

Asteroid "Gavrilin/(7369) Gavrilin"
{
    ParentBody      "Gavrilin system"
    Radius          3.77
    AbsMagn         13.1
    SlopeParam      0.15
    Albedo          0.16
    TidalLocked     true
    Orbit
    {
        SemiMajorAxis 	5.70716709978102E-09 //   27 km
        Period          0.0056045004 //   2.047 days
        MeanAnomaly     0
        ArgOfPericenter 0
        Eccentricity    0
        Inclination     0
        RefPlane        "Equator"
    }
}

Asteroid "S2007 (7369) 1"
{
    ParentBody      "Gavrilin system"
    Class           "Asteroid"
    Radius          1.205
    TidalLocked     true
    Orbit
    {
		SemiMajorAxis	1.74776685201467E-07 //   27 km
		Period			0.0056045004 //   2.047 days
		MeanAnomaly		0
		ArgOfPericenter	180
		Eccentricity	0
		Inclination		0
		RefPlane		"Equator"
    }
}

Asteroid "Wirt/(2044) Wirt"
{
	ParentBody	 "Sol"
	Radius			3.23
	AbsMagn			13
	SlopeParam		0.15
	Albedo			0.191
	RotationPeriod	3.6898
    Orbit
    {
		Epoch			2457600.5
		SemiMajorAxis	2.380343808
		Period			3.672550162
		MeanAnomaly		305.9993987640921
		ArgOfPericenter	50.3867
		Eccentricity	0.34376841
		Inclination		23.974369
		AscendingNode	53.673166
		PericenterDist	1.5620568
		RefPlane	 	"Ecliptic"
    }
}

Asteroid "S2005 (2044) 1"
{
	ParentBody "(2044) Wirt"
	Radius	0.81
    Orbit
    {
		SemiMajorAxis	7.8969e-8 // 12 km
		Period			0.0021640435 // 0.7904 days
		MeanAnomaly		0
		ArgOfPericenter	180
		Eccentricity	0
		Inclination		20 // no data
		AscendingNode	0
		PericenterDist	0
		RefPlane		"Equator"
    }
}

///////////////////////////////////////////////////////////
//           Unordered list of asteroids                 //
///////////////////////////////////////////////////////////

Asteroid "1999 TO14/(22899) 1999 TO14"
{
    ParentBody  "Sol"
    Class       "Asteroid"
    Radius      2.77

    RotationPeriod  4.03

    Orbit
    {
        Epoch            2457400.5
        SemiMajorAxis    2.8443
        Period           4.797
        Eccentricity     0.08128
        Inclination      2.8811
        AscendingNode    136.03
        ArgOfPericenter  218.38
        MeanAnomaly      56.959
        RefPlane        "Ecliptic"
    }
}

Asteroid "S2003 (22899) 1"
{
    ParentBody  "1999 TO14"
    Class       "Asteroid"
    Radius      0.615

    Orbit
    {
        Epoch           2457400.5
        SemiMajorAxis   1.217E-6
        Period          0.153323
        Eccentricity    0
        Inclination     0
        RefPlane       "Equator"
    }
}

Asteroid "2000 GL74/(17246) 2000 GL74"
{
    ParentBody  "Sol"
    Class       "Asteroid"
    Radius      2.25

    RotationPeriod  5

    Orbit
    {
        Epoch            2457400.5
        SemiMajorAxis    2.8389
        Period           4.7835
        Eccentricity     0.021177
        Inclination      2.4449
        AscendingNode    34.461
        ArgOfPericenter  229.91
        MeanAnomaly      293.34
        RefPlane        "Ecliptic"
    }
}

Asteroid "S2004 (17246) 1"
{
    ParentBody  "2000 GL74"
    Class       "Asteroid"
    Radius      0.5

    Orbit
    {
        Epoch           2457400.5
        SemiMajorAxis   1.524E-6
        Period          0.246412
        Eccentricity    0
        Inclination     0
        RefPlane       "Equator"
    }
}

Asteroid "Polonskaya/(2006) Polonskaya"
{
    ParentBody  "Sol"
    Class       "Asteroid"
    Radius      2.255

    RotationPeriod  3.118

    Orbit
    {
        Epoch            2457400.5
        SemiMajorAxis    2.3239
        Period           3.5427
        Eccentricity     0.19337
        Inclination      4.9188
        AscendingNode    0.98817
        ArgOfPericenter  24.4
        MeanAnomaly      160.45
        RefPlane        "Ecliptic"
    }
}

Asteroid "S2005 (2004) 1"
{
    ParentBody  "(2006) Polonskaya"
    Class       "Asteroid"
    Radius      0.495

    RotationPeriod  6.6571

    Orbit
    {
        Epoch           2457400.5
        SemiMajorAxis   5.68E-8
        Period          0.002185
        Eccentricity    0
        Inclination     0
        RefPlane       "Equator"
    }
}

Barycenter "Typhon-Echidna"
{
    ParentBody  "Sol"
	Mass        1.6986e-7

    Orbit
    {
        Epoch            2457400.5
        SemiMajorAxis    38.108
        Period           235.2471
        Eccentricity     0.54015
        Inclination      2.4254
        AscendingNode    351.91
        ArgOfPericenter  159.04
        MeanAnomaly      12.264
        RefPlane        "Ecliptic"
    }
}

Asteroid "Typhon/(42355) Typhon"
{
    ParentBody    "Typhon-Echidna"
    Class         "Asteroid"
    Radius         81
    Mass           1.457e-7
    Obliquity      42
    EqAscendNode   254
    RotationPeriod 9.67

    Orbit
    {
        Epoch            2457400.5
        SemiMajorAxis    1.502e-6
        Period           0.051971
        Eccentricity     0.507
        Inclination      42
        AscendingNode    254
        ArgOfPericenter  174
        MeanAnomaly      11
        RefPlane        "Equator"
    }
}

Asteroid "Echidna/Typhon I"
{
    ParentBody    "Typhon-Echidna"
    Class         "Asteroid"
    Radius         44.5
    Mass           2.416e-8	// assumption of the same density as Typhon
    Obliquity      42
    EqAscendNode   254

    Orbit
    {
        Epoch            2457400.5
        SemiMajorAxis    9.058e-6
        Period           0.051971
        Eccentricity     0.507
        Inclination      42
        AscendingNode    254
        ArgOfPericenter  354
        MeanAnomaly      11
        RefPlane        "Equator"
    }
}

Asteroid "1998 PG/(31345) 1998 PG"
{
	ParentBody		"Sol"
	AsterType		"Amor"
	Radius			0.45
	AbsMagn			17.3
	SlopeParam		0.15
	Albedo			0.15
	RotationPeriod	2.5162
    Orbit
    {
		Epoch			2457600.5
		SemiMajorAxis	2.01495513
		Period			2.860264998
		MeanAnomaly		77.84604594856323
		ArgOfPericenter	155.955286
		Eccentricity	0.39155352
		Inclination		6.4942996
		AscendingNode	222.793932
		PericenterDist	1.225992357
		RefPlane	 	"Ecliptic"
    }
}

Asteroid "S2001 (31345) 1"
{
	ParentBody	"1998 PG"
	Class 		"Asteroid"
	AsterType	"Amor"
	Radius		0.135
    Orbit
    {
		SemiMajorAxis	9.35842197118E-9 //   1.4 km
		Period			0.0015975701 //   0.5835 days
		MeanAnomaly		0
		ArgOfPericenter	0
		Eccentricity	0
		Inclination		0
    }
}

Asteroid "Mette/(1727) Mette"
{
	ParentBody		"Sol"
	Radius			5.09
	AbsMagn			12.5
	SlopeParam		0.15
	Albedo			0.16
	RotationPeriod	2.981
    Orbit
    {
		Epoch			2457600.5
		SemiMajorAxis	1.854222431
		Period			2.524939334
		MeanAnomaly		166.6136943698989
		ArgOfPericenter	313.012551
		Eccentricity	0.101729224
		Inclination		22.8969492
		AscendingNode	133.045118
		PericenterDist	1.66559382
		RefPlane	 	"Ecliptic"
    }
}

Asteroid "S2013 (1727) 1"
{
    ParentBody  "(1727) Mette"
    Class       "Asteroid"
    Radius      1.07
    TidalLocked true
    Orbit
    {
        SemiMajorAxis   1.40376329568E-07 //   21 km
        Period          0.0023945755 //   0.8746 days
        MeanAnomaly     0
        ArgOfPericenter 180
        Eccentricity    0
        Inclination     0
		RefPlane       "Equator"
    }
}

Asteroid "Stephengould/(8373) Stephengould"
{
	ParentBody "Sol"
	AsterType  "NEO"
	Radius			2.645
	AbsMagn			13.9
	SlopeParam		0.15
	Albedo			0.16
	RotationPeriod	4.435
    Orbit
    {
		Epoch			2457600.5
		SemiMajorAxis	3.280218619
		Period			5.941038768
		MeanAnomaly		31.19895596470528
		ArgOfPericenter	55.460013
		Eccentricity	0.554698714
		Inclination		40.790905
		AscendingNode	88.8845143
		PericenterDist	1.46068557
		RefPlane		"Ecliptic"
    }
}

Asteroid "S2010 (8373) 1"
{
    ParentBody  "(8373) Stephengould"
    Class       "Asteroid"
    Radius      0.715

    Orbit
    {
        SemiMajorAxis   1.00268806834E-07 //   15 km
        Period          0.003896045 //   1.423 days
        MeanAnomaly     0
        ArgOfPericenter 180
        Eccentricity    0
        Inclination     0
    }
}

Asteroid "Alauda/(702) Alauda"
{
	ParentBody  	"Sol"
	Class       	"Asteroid"
	Radius      	100.98
	Mass  			1.015E-6
	RotationPeriod  8.354
    Orbit
    {
		Epoch   		2457400.5
		SemiMajorAxis	3.1919977
		Period			5.70298
		Eccentricity	0.020055
		Inclination		20.611
		AscendingNode	289.93
		ArgOfPericenter	351.67
		MeanAnomaly		102.07
		RefPlane		"Ecliptic"
    }
}

Asteroid "Pichi unem/Alauda I"
{
	ParentBody  "(702) Alauda"
	Class       "Asteroid"
	Radius      1.755
	Orbit
    {
		SemiMajorAxis	8.202E-6 // separation 1227 km
		Period   		0.013454
		Eccentricity    0.003
		Inclination		0
		RefPlane		"Equator"
    }
}

Barycenter "Lundia system"
{
    ParentBody  	"Sol"
    Orbit
    {
		Epoch   		2457400.5
		SemiMajorAxis	2.2824
		Period			3.4482
		Eccentricity	0.19313
		Inclination		7.1491
		AscendingNode	154.58
		ArgOfPericenter	196.28
		MeanAnomaly		208.04
		RefPlane		"Ecliptic"
    }
}

Asteroid "Lundia/(809) Lundia"
{
    ParentBody  "Lundia system"
    Class       "Asteroid"
    Radius      3.45
    Mass  		8.138E-11
	TidalLocked true
    Orbit
    {
		SemiMajorAxis	5.01e-8 // separation 15.8 km
		Period			0.001759
		Eccentricity    0
		Inclination		0
		AscendingNode   0
		ArgOfPericenter 180
		MeanAnomaly     0
		RefPlane		"Equator"
    }
}

Asteroid "S2005 (809) 1"
{
    ParentBody  "Lundia system"
    Class       "Asteroid"
    Radius      3.05
    Mass  		3.862E-11
	TidalLocked true
    Orbit
    {
		SemiMajorAxis	5.55e-8 // separation 15.8 km
		Period			0.001759
		Eccentricity    0
		Inclination		0
		AscendingNode   0
		ArgOfPericenter 0
		MeanAnomaly     0
		RefPlane		"Equator"
    }
}

Barycenter "Frostia system"
{
    ParentBody  	"Sol"
    Orbit
    {
		Epoch   		2457400.5
		SemiMajorAxis	2.3685
		Period			3.6452
		Eccentricity	0.17357
		Inclination		6.0875
		AscendingNode	190.61
		ArgOfPericenter	84.361
		MeanAnomaly		272.23
		RefPlane		"Ecliptic"
    }
}

Asteroid "Frostia/(854) Frostia"
{
    ParentBody  "Frostia system"
    Class       "Asteroid"
    Radius      3.175
    Mass  		2.763e-11
    TidalLocked true
    Orbit
    {
		SemiMajorAxis	3.128e-8 // separation 17 km
		Period   		0.004304
		Eccentricity    0
		Inclination		0
		AscendingNode   0
		ArgOfPericenter 180
		MeanAnomaly     0
		RefPlane		"Equator"
    }
}

Asteroid "S2004 (854) 1"
{
    ParentBody  "Frostia system"
    Class       "Asteroid"
    Radius      2.3
	Mass        1.05e-11
    TidalLocked true
    Orbit
    {
		SemiMajorAxis	8.232e-8 // separation 17 km
		Period   		0.004304
		Eccentricity    0
		Inclination		0
		AscendingNode   0
		ArgOfPericenter 0
		MeanAnomaly     0
		RefPlane		"Equator"
    }
}

Barycenter "Berna system"
{
	ParentBody	 "Sol"
    Orbit
    {
		Epoch			2457600.5
		SemiMajorAxis	2.655728252
		Period			4.32796413
		MeanAnomaly		14.562692453
		ArgOfPericenter	99.290186
		Eccentricity	0.208458632
		Inclination		12.5347978
		AscendingNode	298.356889
		PericenterDist	2.10211877
		RefPlane	 	"Ecliptic"
    }
}

Asteroid "Berna/(1313) Berna"
{
	ParentBody 		"Berna system"
	AsterType 		"Eunomia"
	Radius			5.3
	AbsMagn			11.6
	SlopeParam		0.15
	Albedo			0.184
	RotationPeriod	25.464
	TidalLocked     true
    Orbit
    {
		SemiMajorAxis	5.5e-08 // 25 km
		Period			0.0029049218 // 1.061 days
		ArgOfPericenter	0
		MeanAnomaly		0
		RefPlane		"Equator"
    }
}

Asteroid "S2004 (1313) 1"
{
	ParentBody 	    "Berna system"
	Radius		    4.185
	TidalLocked     true
    Orbit
    {
		SemiMajorAxis	11.2e-8 // 25 km
		Period			0.0029049218 // 1.061 days
		ArgOfPericenter	180
		MeanAnomaly		0
		RefPlane		"Equator"
    }
}

Asteroid "Isberga/(939) Isberga"
{
    ParentBody  	"Sol"
    Class       	"Asteroid"
    Radius      	6.2
    Mass  			6.045E-10
    RotationPeriod  2.91695
    Orbit
    {
		Epoch            2457400.5
		SemiMajorAxis    2.2467
		Period           3.3677
		Eccentricity     0.17746
		Inclination      2.5877
		AscendingNode    327.14
		ArgOfPericenter  5.904982
		MeanAnomaly      13.611
		RefPlane        "Ecliptic"
    }
}

Asteroid "S2006 (939) 1"
{
    ParentBody  "(939) Isberga"
    Class       "Asteroid"
    Radius      1.8
    Orbit
    {
		SemiMajorAxis	2.206E-7 // separation 33 km
		Period   		0.003038
		Eccentricity    0.1
		Inclination  	0
		RefPlane       "Equator"
    }
}

Asteroid "2002 UX25/(55637) 2002 UX25"
{
    ParentBody  "Sol"
	AsterType   "Cubewano"
    Radius      332.5
    Mass        2.093E-5
    RotationPeriod  14.382
	AbsMagn     3.7
	SlopeParam  0.15

    Orbit
    {
        Epoch           2457400.5
        SemiMajorAxis   42.785
        Period          279.859
        Eccentricity    0.144846
        Inclination     19.438
        AscendingNode   204.64
        ArgOfPericenter 276.83
        MeanAnomaly     293.896
        RefPlane       "Ecliptic"
    }
}

Asteroid "S2005 (55637) 1"
{
    ParentBody  "2002 UX25"
    Class       "Asteroid"
    Radius      105

    Orbit
    {
        Epoch           2457400.5
        SemiMajorAxis   3.189E-5
        Period          0.022749
        Eccentricity    0.17
        Inclination     0
        RefPlane       "Equator"
    }
}

Asteroid "2002 WC19/(119979) 2002 WC19"
{
    ParentBody  "Sol"
	AsterType   "ResonantTNO"
    Radius      220
    Mass        1.289E-5
	AbsMagn     4.9
	SlopeParam  0.15
    Orbit
    {
        Epoch           2457400.5
        SemiMajorAxis   48.182
        Period          334.45
        Eccentricity    0.26549
        Inclination     9.1674
        AscendingNode   109.78
        ArgOfPericenter 43.545
        MeanAnomaly     314.89
        RefPlane       "Ecliptic"
    }
}

Asteroid "S2006 (119979) 1"
{
    ParentBody  "2002 WC19"
    Class       "Asteroid"
    Radius      69.5

    Orbit
    {
        Epoch           2457400.5
        SemiMajorAxis   2.734E-5
        Period          0.023007
        Eccentricity    0.2
        Inclination     0
        RefPlane       "Equator"
    }
}

Asteroid "2003 AZ84/(208996) 2003 AZ84"
{
    ParentBody  "Sol"
	AsterType   "Plutino"
    Radius      361.5
    RotationPeriod  13.44
	AbsMagn     3.7
	SlopeParam  0.15

    Orbit
    {
        Epoch           2457400.5
        SemiMajorAxis   39.6805
        Period          249.962
        Eccentricity    0.17444
        Inclination     13.548
        AscendingNode   251.95
        ArgOfPericenter 14
        MeanAnomaly     226.66
        RefPlane       "Ecliptic"
    }
}

Asteroid "S2005 (208996) 1"
{
    ParentBody  "2003 AZ84"
    Class       "Asteroid"
    Radius      36

    Orbit
    {
        Epoch           2457400.5
        SemiMajorAxis   4.813E-5
        Period          0.032855
        Eccentricity    0
        Inclination     0
        RefPlane       "Equator"
    }
}

Barycenter "Sila-Nunam"
{
    ParentBody "Sol"

    Orbit
    {
        Epoch           2457400.5
        SemiMajorAxis   44.133
        Period          293.192
        Eccentricity    0.015946
        Inclination     2.23732
        AscendingNode   304.32
        ArgOfPericenter 214.91
        MeanAnomaly     337.33
        RefPlane       "Ecliptic"
    }
}

Asteroid "Sila/(79360) Sila"
{
    ParentBody  "Sila-Nunam"
    Class       "Asteroid"
	AsterType   "Cubewano"
	AbsMagn     5.3
	SlopeParam  0.15
    Radius      124.5
    Mass        9.7632E-7
    TidalLocked true
    Orbit
    {
        SemiMajorAxis   8.538E-6 // separation 2777 km
        Period          0.034251
        Eccentricity    0.018
        Inclination     103.51
        ArgOfPericenter 326
        MeanAnomaly     16.3
        AscendingNode   140.76
        RefPlane       "Equator"
    }
}

Asteroid "Nunam/(79360) Sila I Nunam"
{
    ParentBody  "Sila-Nunam"
    Class       "Asteroid"
    Radius      118
    Mass        8.3168E-7
    TidalLocked true
    Orbit
    {
        SemiMajorAxis   1.002E-5 // separation 2777 km
        Period          0.034251
        Eccentricity    0.018
        Inclination     103.51
        ArgOfPericenter 146
        MeanAnomaly     16.3
        AscendingNode   140.76
        RefPlane       "Equator"
    }
}

Asteroid "Salacia/(120347) Salacia"
{
    ParentBody "Sol"
	AsterType  "Cubewano"
	AbsMagn     4.2
	SlopeParam  0.15
    Radius      427
    Mass  		7.334E-5
    RotationPeriod  6.5

    Orbit
    {
		Epoch			2457400.5
		SemiMajorAxis	41.962
		Period    		271.83
		Eccentricity	0.10629
		Inclination		23.944
		AscendingNode	280.23
		ArgOfPericenter	308.46
		MeanAnomaly		119.2
		RefPlane		"Ecliptic"
	}
}

Asteroid "Actaea/Salacia I Actaea"
{
    ParentBody  "Salacia"
    Class       "Asteroid"
    Radius      143

    Orbit
    {
		Epoch			2457400.5
		SemiMajorAxis	3.756E-5
		Period    		0.015042
		Eccentricity	0.0084
		Inclination		0
		RefPlane		"Equator"
    }
}

Asteroid "2007 UK126/(229762) 2007 UK126"
{
    ParentBody  "Sol"
    Radius      295
	AbsMagn     3.4
	SlopeParam  0.15
    RotationPeriod  11.05

    Orbit
    {
		Epoch			2457400.5
		SemiMajorAxis	74.158
		Period    		638.627
		Eccentricity	0.49374
		Inclination		23.34
		AscendingNode	131.287
		ArgOfPericenter	346.144
		MeanAnomaly		342.106
		RefPlane		"Ecliptic"
    }
}

Asteroid "S2008 (229762) 1"
{
    ParentBody  "2007 UK126"
    Class       "Asteroid"
    Radius      51.5

    Orbit
    {
        Epoch			2457400.5
        SemiMajorAxis	2.406E-5
		Period			0.016154
        Eccentricity	0
        Inclination		0
        RefPlane       "Equator"
    }
}

Asteroid "1998 ST27/(363027) 1998 ST27"
{
    ParentBody  	"Sol"
    Class       	"Asteroid"
    Radius      	0.4
    RotationPeriod	3

    Orbit
    {
		Epoch   		2457400.5
		SemiMajorAxis	0.81938
		Period			0.74172
		Eccentricity    0.53001
		Inclination  	21.055
		AscendingNode	197.57
		ArgOfPericenter	322.45
		MeanAnomaly		267.57
		RefPlane		"Ecliptic"
    }
}

Asteroid "S2001 (363027) 1"
{
    ParentBody		"1998 ST27"
    Class			"Asteroid"
    Radius      	0.06
    RotationPeriod 	6

    Orbit
    {
		Epoch   		2457400.5
		SemiMajorAxis	3.01E-8
		Period			0.011225
		Eccentricity    0.3
		Inclination  	0
		RefPlane  		"Equator"
    }
}

Asteroid "2000 JO23/(32039) 2000 JO23"
{
    ParentBody  	"Sol"
    Class			"Asteroid"
    Radius			1.98
    RotationPeriod	6.5979

    Orbit
    {
		Epoch   		2457400.5
		SemiMajorAxis	2.2228
		Period			3.3141
		Eccentricity    0.28275
		Inclination		6.5093
		AscendingNode	303.88
		ArgOfPericenter	353.2
		MeanAnomaly		37.133
		RefPlane		"Ecliptic"
    }
}

Asteroid "S2007 (32039) 1"
{
    ParentBody  "2000 JO23"
    Class       "Asteroid"
    Radius      0.635

    Orbit
    {
		Epoch   		2457400.5
		SemiMajorAxis	3.543E-7
		Period			0.041069
		Eccentricity    0
		Inclination		0
		RefPlane  		"Equator"
    }
}

Barycenter "Ceto-Phorcys"
{
    ParentBody "Sol" //Volume 8.43E6

    Orbit
    {
		Epoch   		2457400.5
		SemiMajorAxis	101.903
		Period			1028.702
		Eccentricity    0.825449
		Inclination		22.2784
		AscendingNode	171.905
		ArgOfPericenter	319.778
		MeanAnomaly		8.6746
		RefPlane  		"Ecliptic"
    }
}

Asteroid "Ceto/(65489) Ceto"
{
    ParentBody  	"Ceto-Phorcys"
    Class       	"Asteroid"
    Radius      	111.5 //Volume 5.81E6 = 69%
    RotationPeriod  4.43
    Obliquity       68.8
    EqAscendNode    0

    Orbit
    {
		Epoch   		2457400.5
		SemiMajorAxis	3.813E-6 // 1840 km
		Period			0.026158
		Eccentricity    0.004
		Inclination		68.8
        AscendingNode   0
		ArgOfPericenter	180
		MeanAnomaly		0
    }
}

Asteroid "Phorcys/Ceto I Phorcys"
{
    ParentBody  "Ceto-Phorcys"
    Class       "Asteroid"
    Radius      85.5 //Volume 2.62E6 = 31%
    Orbit
    {
		Epoch   		2457400.5
		SemiMajorAxis	8.487E-6 // 1840 km
		Period			0.026158
		Eccentricity    0.004
		Inclination		68.8
		AscendingNode   0
		ArgOfPericenter	0
		MeanAnomaly		0
    }
}

Barycenter "2003 QR91 System"
{
    ParentBody "Sol" //Volume 8.21E6

    Orbit
    {
		Epoch   		2457400.5
		SemiMajorAxis	46.376
		Period			315.821
		Eccentricity    0.17962
		Inclination  	3.49874
		AscendingNode	273.309
		ArgOfPericenter	12.76
		MeanAnomaly		30.59
		RefPlane  		"Ecliptic"
    }
}

Asteroid "2003 QR91"
{
    ParentBody "2003 QR91 System"
    Class      "Asteroid"
    Radius      103.5 //Volume 4.68E6 = 57%
	AsterType  "Cubewano"
	AbsMagn     6.5
	SlopeParam  0.15
    TidalLocked true
    Orbit
    {
		Epoch   		2457400.5
		SemiMajorAxis	5.1471E-6 // 1790 km
		Period			0.020534
		Eccentricity    0
		Inclination  	0
		ArgOfPericenter	180
		MeanAnomaly  	0
    }
}

Asteroid "S2007 (2003 QR91) 1"
{
    ParentBody	"2003 QR91 System"
    Class		"Asteroid"
    Radius		94.5 //Volume 3.53E6 = 43%
    TidalLocked	true
    Orbit
    {
		Epoch   		2457400.5
		SemiMajorAxis	6.8229E-6 // 1790 km
		Period			0.020534
		Eccentricity    0
		Inclination  	0
		ArgOfPericenter	0
		MeanAnomaly  	0
    }
}

Barycenter "2004 PB108 System"
{
    ParentBody "Sol" //Volume 8.63E6

    Orbit
    {
		Epoch   		2457400.5
		SemiMajorAxis	45.025
		Period			302.126
		Eccentricity	0.10982
		Inclination		20.2373
		AscendingNode	147.4823
		ArgOfPericenter	274.6
		MeanAnomaly		303.132
		RefPlane  		"Ecliptic"
    }
}

Asteroid "2004 PB108"
{
    ParentBody  "2004 PB108 System"
    Class       "Asteroid"
    Radius      121.5 //Volume 7.51E6 = 87%
    Mass  		1.586E-6
	AsterType  "Cubewano"
	AbsMagn     6.6
	SlopeParam  0.15
	Albedo      0.08
	Obliquity   106.55

    Orbit
    {
		Epoch   		2457400.5
		SemiMajorAxis	9.0376E-6 // 10400 km
		Period			0.265632
		Eccentricity    0.438
		Inclination		106.55
		ArgOfPericenter	180
		MeanAnomaly  	0
		RefPlane  		"Ecliptic"
    }
}

Asteroid "S2006 (2004 PB108) 1"
{
    ParentBody  "2004 PB108 System"
    Class       "Asteroid"
    Radius      64.5 //Volume 1.12E6 = 13%
	Obliquity   106.55

    Orbit
    {
		Epoch   		2457400.5
		SemiMajorAxis	6.048E-5 // 10400 km
		Period			0.265632
		Eccentricity    0.438
		Inclination  	106.55
		ArgOfPericenter	0
		MeanAnomaly  	0
		RefPlane  		"Ecliptic"
    }
}

Barycenter "Celle system"
{
	ParentBody "Sol"
	Orbit
    {
		RefPlane        "Ecliptic"
		Epoch			2457600.5
		SemiMajorAxis   2.41565
		Period          3.7567
		Eccentricity    0.0933727
		Inclination     5.24809
		AscendingNode   271.363
		ArgOfPericenter 334.209
		MeanAnomaly     90.1563
    }
}

Asteroid    "Celle/(3782) Celle"
{
    ParentBody     "Celle system"
    Class        	"Asteroid"
    Mass            5.67928e-011
    Radius          3
    InertiaMoment   0.399604
    RotationPeriod  3.84
    Obliquity       -3.78755
    EqAscendNode    270.792
    AlbedoBond      0.2
    AlbedoGeom      0.24
    Brightness      3.5
    Color          (1.382 1.351 1.337)
    Surface
    {
		SurfStyle       0.148692
		OceanStyle      0.483344
		Randomize      (0.384, -0.888, 0.529)
		BumpHeight      0.89615
		BumpOffset      0.54
		SpecBrightWater 0
		SpecBrightIce   0.03
		SpecularPower   30
		Hapke           1
		SpotBright      4
		SpotWidth       0.05
		DayAmbient      0.07
    }

    Orbit
    {
		RefPlane        "Equator"
		SemiMajorAxis   1.6842e-8 // 1.203e-7 * 0.14
		Period          0.00638865
		Eccentricity    0
		Inclination     10 // no data
		AscendingNode   0
		ArgOfPericenter 0
		MeanAnomaly     0
    }
}

Asteroid    "S2001 (3782) 1"
{
    ParentBody     	"Celle system"
    Class        	"Asteroid"
    Mass            9.29592e-012  // 0.14
    Radius          1.17
    InertiaMoment   0.397935
    Oblateness      0.00174242
    Obliquity       0
    EqAscendNode    0
    TidalLocked     true
    AlbedoBond      0.2
    AlbedoGeom      0.24
    Brightness      3.5
    Color          (1.122 1.096 1.086)
    Surface
    {
		SurfStyle       0.19635
		OceanStyle      0.0230508
		Randomize      (0.264, 0.999, 0.221)
		BumpHeight      0.719686
		BumpOffset      0.315
		SpecBrightWater 0
		SpecBrightIce   0.03
		SpecularPower   30
		Hapke           1
		SpotBright      4
		SpotWidth       0.05
		DayAmbient      0.07
    }

    Orbit
    {
		RefPlane        "Equator"
		SemiMajorAxis   1.03458e-7 // 1.203e-7 * 0.86
		Period          0.00638865
		Eccentricity    0
		Inclination     10 // no data
		AscendingNode   0
		ArgOfPericenter 180
		MeanAnomaly     0
    }
}

Barycenter "Tama System"
{
	ParentBody	 "Sol"

    Orbit
    {
		Epoch			2457600.5
		SemiMajorAxis	2.213775091
		Period			3.293885112
		MeanAnomaly		323.1902651
		ArgOfPericenter	354.353642
		Eccentricity	0.12750272
		Inclination		3.7266981
		AscendingNode	71.49098987
		PericenterDist	1.931512745
		RefPlane		"Ecliptic"
    }
}

Asteroid "Tama/(1089) Tama"
{
	ParentBody 		"Tama System"
	Radius			5.35
	AbsMagn			11.7
	SlopeParam		0.15
	Albedo			0.242
	RotationPeriod	16.444
	RotationOffset  -100
	Obliquity		5 // no data
	
	Surface
	{
		Randomize      (-0.190, -0.117, -0.527)
	}

    Orbit
    {
	SemiMajorAxis	3.36622635921822E-08 // 20.7  km
	Period			0.0018760155 // 0.6852  days
	MeanAnomaly		0
	ArgOfPericenter	0
	Eccentricity	0
	Inclination		5 // no data
	AscendingNode	0
	RefPlane		"Equator"
    }
}

Asteroid "S2003 (1089) 1"
{
	ParentBody	"Tama System"
	Radius		3.665
	TidalLocked	true
    Orbit
    {
		SemiMajorAxis	1.04708689838775E-07 // 20.7  km
		Period			0.0018760155 // 0.6852  days
		MeanAnomaly		0
		ArgOfPericenter	180
		Eccentricity	0
		Inclination		5 // no data
		AscendingNode	0
		RefPlane		"Equator"
    }
}

Asteroid "Hovland/(9069) Hovland"
{
    ParentBody		"Sol"
    Class			"Asteroid"
    Radius			1.5
    RotationPeriod	4.2175

    Orbit
    {
		Epoch			2457400.5
		SemiMajorAxis	1.9132
		Period			2.6464
		Eccentricity    0.1182
		Inclination		19.572
		AscendingNode	247.94
		ArgOfPericenter	171.04
		MeanAnomaly		200.24
		RefPlane		"Ecliptic"
    }
}

Asteroid "S2004 (9069) 1"
{
    ParentBody  "(9069) Hovland"
    Class       "Asteroid"
    Radius      0.45

    Orbit
    {
		Epoch			2457400.5
		SemiMajorAxis	5.21E-8
		Period			0.003461
		Eccentricity    0
		Inclination  	0
		RefPlane  		"Equator"
    }
}

Asteroid "Wendelinefroger/(15268) Wendelinefroger"
{
    ParentBody  	"Sol"
    Class      		"Asteroid"
    Radius      	1.915
    RotationPeriod	2.4224

    Orbit
    {
		Epoch   		2457400.5
		SemiMajorAxis	2.3651
		Period			3.6374
		Eccentricity    0.23509
		Inclination		2.7541
		AscendingNode	144.06
		ArgOfPericenter	210.39
		MeanAnomaly		205.83
		RefPlane  		"Ecliptic"
    }
}

Asteroid "S2008 (15268) 1"
{
    ParentBody  "(15268) Wendelinefroger"
    Class       "Asteroid"
    Radius      0.515

    Orbit
    {
		Epoch   		2457400.5
		SemiMajorAxis	5.82E-8
		Period			0.002861
		Eccentricity    0
		Inclination		0
		RefPlane		"Equator"
    }
}
