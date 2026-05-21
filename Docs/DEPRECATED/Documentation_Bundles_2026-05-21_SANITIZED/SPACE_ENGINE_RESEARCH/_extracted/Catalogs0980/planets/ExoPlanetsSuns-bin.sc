///////////////////////////////////////////////////////////
//                                                       //
//      Binary and multiple stars whith exoplanets       //
//                                                       //
///////////////////////////////////////////////////////////


// Star solver log level:
// 0 - do not log
// 1 - log errors and warnings only
// 2 - log everything
LogLevel    1


//11 Com;spanish wiki

Star "11 Com A/HIP 60202/HD 107383"
{
	ParentBody "11 Com"
	Class      "G8 III"
	Radius     13224000
	AppMagn    4.7
	MassSol    2.7
	Teff       4742
	FeH       -0.35
	Orbit
	{
		Period          12492.9793
		SemiMajorAxis   166.663659
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "11 Com B"
{
	ParentBody "11 Com"
	Class      "K V" //unknown,related with     AppMagn
	AppMagn    12.9
	Orbit
	{
		Period          12492.9793
		SemiMajorAxis   642.845543
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//EPS CrB;english and spanish wiki

Star "EPS CrB A/HIP 78159/HD 143107"
{
	ParentBody "EPS CrB"
	Class      "K2 III"
	Radius     15312000
	AppMagn    4.14
	MassSol    2.5
	FeH       -0.094
	Age        1.74
	Orbit
	{
		Period          898.3044
		SemiMajorAxis   24.344262
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "EPS CrB B"
{
	ParentBody "EPS CrB"
	Class      "K3 V"
	AppMagn    12.6
	MassSol    0.55
	Orbit
	{
		Period          898.3044
		SemiMajorAxis   110.655738
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//UPS And;english wiki

Star "Titawin/UPS And A"
{
	ParentBody "UPS And"
	Class      "F8 V"
	Radius     1030080
	AppMagn    4.09
	MassSol    1.27
	Teff       6212
	FeH        0.09
	Age        3.8
	Orbit
	{
		Period          16729.1384
		SemiMajorAxis   119.2053
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "UPS And B"
{
	ParentBody "UPS And"
	Class      "M4 V"
	Orbit
	{
		Period          16729.1384
		SemiMajorAxis   630.7947
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Muscida; eng and sp wiki

Star "OMI UMa A/HIP 41704/HD 71369"
{
	ParentBody "Muscida"
	Class      "G4 II"
	Radius     10440000
	AppMagn    3.36
	MassSol    3
	RadSol     14.1
	Teff       5242
	FeH       -0.09
	Orbit
	{
		Period          4106.19425982
		SemiMajorAxis   57.591
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "OMI UMa B"
{
	ParentBody "Muscida"
	Class      "M1 V"
	AppMagn    15
	Orbit
	{
		Period          4106.19425982
		SemiMajorAxis   332.2556
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//HD 89744; spanish wiki

Star "HD 89744 A/HIP 50786 A/GJ 9326 A"
{
	ParentBody "HD 89744"
	Class      "F7 V"
	Radius     1461600
	AppMagn    5.73
	MassSol    1.5
	Orbit
	{
		Period          97459.23279986
		SemiMajorAxis   109.6815
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 89744 B/HIP 50786 B/GJ 9326 B"
{
	ParentBody "HD 89744"
	Class      "M5 V" // unknown subclass
	MassSol    0.07
	Orbit
	{
		Period          97459.23279986
		SemiMajorAxis   2350.3185
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//TAU Boo;spanish and english wiki

Star "TAU Boo A/HIP 67275 A/HD 120136 A"
{
	ParentBody "TAU Boo"
	Class      "F7V"
	Radius     926376
	AppMagn    4.5
	MassSol    1.2
	Teff       6309
	FeH        0.28
	Age        2.52
	Orbit
	{
		Period          2941.894031
		SemiMajorAxis   60
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "TAU Boo B/HIP 67275 B/HD 120136 B"
{
	ParentBody "TAU Boo"
	Class      "M2V"
	MassSol    0.4
	Orbit
	{
		Period          2941.894031
		SemiMajorAxis   180
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Gliese 777;eng,sp wiki

Star "Gliese 777 A/HIP 98767/HD 190360"
{
	ParentBody "Gliese 777"
	Class      "G6 IV"
	Radius     835200
	AppMagn    5.71
	MassSol    0.9
	Teff       5588
	FeH        0.24
	Age        12.11
	Orbit
	{
		Period          150127.90110422
		SemiMajorAxis   750
		Inclination     55.979 //unknown just aligned with planets in SE
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Gliese 777 B"
{
	ParentBody "Gliese 777"
	Class      "M5 V"	//unknown subclass
	AppMagn    14.4
	Orbit
	{
		Period          150127.90110422
		SemiMajorAxis   2250
		Inclination     55.979 //unknown just aligned with planets in SE
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//HD 114729; spanish wiki

Star "HD 114729 A/HIP 64469 A"
{
	ParentBody "SAO 204237"
	Class      "G3 V"
	Radius     974400
	AppMagn    6.69
	MassSol    0.94
	Teff       5662
	FeH       -0.22
	Age        4.58
	Orbit
	{
		Period          4344.80546069
		SemiMajorAxis   59.2437
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 114729 B/HIP 64469 B/2MASS J13124398-3152167"
{
	ParentBody "SAO 204237"
	Class      "M5 V"	//unknown subclass
	MassSol    0.25
	Orbit
	{
		Period          4344.80546069
		SemiMajorAxis   222.7563
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//HR 4523;eng, sp wiki

Star "HD 102365 A/HR 4523 A/HIP 57443"
{
	ParentBody "HR 4523"
	Class      "G2 V"
	Radius     668160
	AppMagn    4.89
	MassSol    0.89
	Teff       5650
	FeH       -0.28
	Age        9
	Orbit
	{
		Period          3681.82077263
		SemiMajorAxis   17.1533
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 102365 B/HR 4523 B"
{
	ParentBody "HR 4523"
	Class      "M4 V"
	MassSol    0.07
	Orbit
	{
		Period          3681.82077263
		SemiMajorAxis   217.8467
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Altarf; spanish wiki

Star "Altarf A/BET Cnc A"
{
	ParentBody "Altarf"
	Class      "K4 III"
	Radius     35000000
	AppMagn    3.526
	MassSol    3
	Teff       4092.1
	FeH       -0.29
	Age        1.85
	Orbit
	{
		Period          72522.3901
		SemiMajorAxis   190.8565
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Altarf B/BET Cnc B"
{
	ParentBody "Altarf"
	Class      "M5 V" 	//unknown
	Orbit
	{
		Period          72522.3901
		SemiMajorAxis   2385.7065
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

RemoveStar "HIP 57443"
StarBarycenter "HR 4523/66 G. Cen/GJ 442/LHS 311"
{
	RA      11 46 31.07263
	Dec     -40 30 1.3
	Dist    9.2331
}

//PSI1 Aqr;english and spanish wiki

Barycenter "91 Aqr (BC)"
{
	ParentBody "91 Aqr"
	Orbit
	{
		Period          61411.77
		SemiMajorAxis   1017.5452
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "91 Aqr A/PSI1 Aqr A/HIP 114855/HD 219449"
{
	ParentBody "91 Aqr"
	Class      "K0 III"
	Radius     7071360
	AppMagn    4.22
	MassSol    1.74
	Teff       4665
	FeH       -0.03
	Age        3.56
	Orbit
	{
		Period          61411.77
		SemiMajorAxis   1264.6634
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "91 Aqr B/PSI1 Aqr B"
{
	ParentBody "91 Aqr (BC)"
	Class      "K3 V"
	AppMagn    9.62
	MassSol    0.7
	Orbit
	{
		Period          84
		SemiMajorAxis   10.75
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "91 Aqr C/PSI1 Aqr C"
{
	ParentBody "91 Aqr (BC)"
	Class      "K3 V"
	AppMagn    10.1
	MassSol    0.7
	Orbit
	{
		Period          84
		SemiMajorAxis   10.75
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//94 Cet;6thCVB,english and spanish wiki

Star "94 Cet A/HIP 14954/HD 19994"
{
	ParentBody "94 Cet"
	Class      "F8 V"
	Radius     1322400
	AppMagn    5.06
	MassSol    1.33
	Teff       6190
	FeH        0.24
	Age        8.91
	Orbit
	{
		Period          1420
		SemiMajorAxis   13.6094
		Eccentricity    0.26
		Inclination     114.1
		AscendingNode   84.13
		ArgOfPericenter 247.74
		Epoch           2444970.17382
		MeanAnomaly     0
	}
}

Star "94 Cet B"
{
	ParentBody "94 Cet"
	Class      "M3 V"
	AppMagn    11
	MassSol    0.13
	Orbit
	{
		Period          1420
		SemiMajorAxis   139.2348
		Eccentricity    0.26
		Inclination     114.1
		AscendingNode   84.13
		ArgOfPericenter 67.74
		Epoch           2444970.17382
		MeanAnomaly     0
	}
}

//Errai;english and spanish wiki
//very good system
//data orbit from english wiki more according than the 6thCVB 

Star "GAM Cep A/Errai A"
{
	ParentBody "Errai"
	Class      "K1 IV"
	Radius     3340800
	AppMagn    3.23
	MassSol    1.6
	Teff       4800
	FeH        0.18
	Age        6.6
	Orbit
	{
		Period          67.5
		SemiMajorAxis   3.987
		Eccentricity    0.4112
		Inclination     119.3
		AscendingNode   18.04
		ArgOfPericenter 161.01
		Epoch           2448478.325139
		MeanAnomaly     0
	}
}

Star "GAM Cep B/Errai B"
{
	ParentBody "Errai"
	Class      "M1 V"
	Radius     348000
	MassSol    0.4
	Orbit
	{
		Period          67.5
		SemiMajorAxis   15.948
		Eccentricity    0.4112
		Inclination     119.3
		AscendingNode   18.04
		ArgOfPericenter 341.01
		Epoch           2448478.325139
		MeanAnomaly     0
	}
}

//HD 46375;spanish wiki

Star "HD 46375 A/HIP 31246"
{
	ParentBody "SAO 114040"
	Class      "K1 IV"
	Radius     696000
	AppMagn    7.84
	MassSol    0.91
	Teff       5285
	FeH        0.24
	Age        4.96
	Orbit
	{
		Period          5277.0495
		SemiMajorAxis   134.6846
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 46375 B"
{
	ParentBody "SAO 114040"
	Class      "M5 V" //unknown,related with mass,it could be also a white dwarf
	MassSol    0.58
	Orbit
	{
		Period          5277.0495
		SemiMajorAxis   211.3154
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//HD 195019;spanish wiki

Star "HD 195019 A/HIP 100970 A"
{
	ParentBody "GC 28482"
	Class      "G3 IV"
	Radius     974400
	AppMagn    6.91
	MassSol    1.06
	Teff       5788
	FeH        0.068
	Age        5.33
	Orbit
	{
		Period          1096.3959
		SemiMajorAxis   56.7353
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 195019 B"
{
	ParentBody "GC 28482" 
	Class      "K3 V"
	AppMagn    10.6
	Orbit
	{
		Period          1096.3959
		SemiMajorAxis   74.2463
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//ADS 16402;english and spanish wiki

Star "ADS 16402 A"
{
	ParentBody "ADS 16402"
	Class      "F8 V"
	Radius     781608
	AppMagn    10.4
	MassSol    1.16
	Orbit
	{
		Period          38229.946682
		SemiMajorAxis   746.753247
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ADS 16402 B/HAT-P-1"
{
	ParentBody "ADS 16402"
	Class      "G0 V"
	Radius     817104
	MassSol    1.15
	Teff       5980
	FeH        0.13
	Age        3.6
	Orbit
	{
		Period          38229.946682
		SemiMajorAxis   753.246753
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Kepler-14;english wiki

Star "Kepler-14 A"
{
	ParentBody "Kepler-14"
	Class      "F V"		//unknown related with mass
	AppMagn    12.12
	MassSol    1.51
	RadSol     2.048
	Teff       6395
	FeH        0.12
	Age        2.2
	Orbit
	{
		Period          2800
		SemiMajorAxis   134.206897
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Kepler-14 B"
{
	ParentBody "Kepler-14"
	Class      "F V"		//unknown related with mass
	MassSol    1.39
	Orbit
	{
		Period          2800
		SemiMajorAxis   145.793103
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star    "2M 0103-55 A/2MASS J01033563-5515561 A"
{
	ParentBody "2MASS J01033563-5515561"
	Class      "M8 V"
	MassSol     0.4
	Age         0.03

	Orbit
	{
		SemiMajorAxis   1	// random value
		Eccentricity    0.4	// random value
		AscendingNode   0
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star    "2M 0103-55 B/2MASS J01033563-5515561 B"
{
	ParentBody "2MASS J01033563-5515561"
	Class      "M8 V"
	MassSol     0.4
	Age         0.03

	Orbit
	{
		SemiMajorAxis   1	// random value
		Eccentricity    0.4	// random value
		AscendingNode   0
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star	"DP Leo A"
{
	ParentBody  "DP Leo"
	Class       "DA1"
	Lum         0.00449
	MassSol     0.6
	Radius      7934.4
	Temperature 13500

	Orbit
	{
		Period          0.0001708
		SemiMajorAxis   0.0003522
		Eccentricity    0
		Inclination     79.5
		ArgOfPericenter 20
		MeanAnomaly     0
	}
}

Star	"DP Leo B"
{
	ParentBody  "DP Leo"
	Class       "M5V"
	Lum         0.00105
	MassSol     0.09
	Radius      83520
	Temperature 3000

	Orbit
	{
		Period          0.0001708
		SemiMajorAxis   0.0023478
		Eccentricity    0
		Inclination     79.5
		ArgOfPericenter 200
		MeanAnomaly     0
	}
}

Star	"Ross 458 A/Gliese 494 A/Wolf 462 A/DT Vir A/HIP 63510 A"
{
	ParentBody  "DT Vir"
	Class       "M0V"

	Orbit   // random data
	{
		Period          0.1
		Eccentricity    0.15
		Inclination     90.0322
		AscendingNode   0
		ArgOfPericenter 20
		MeanAnomaly     0
	}
}

Star	"Ross 458 B/Gliese 494 B/Wolf 462 B/DT Vir B/HIP 63510 B"
{
	ParentBody  "DT Vir"
	Class       "M7V"

	Orbit   // random data
	{
		Period          0.1
		Eccentricity    0.15
		Inclination     90.0322
		AscendingNode   0
		ArgOfPericenter 200
		MeanAnomaly     0
	}
}

// data from arXiv:1311.7664v2 
Star	"FW Tau A"
{
	ParentBody  "FW Tau"
	Class       "M4V"
	MassSol     0.28
	Age         0.0018

	Orbit   // random data
	{
		SemiMajorAxis   5.5	// 11 AU mutual separation
		Inclination     0	// random
		ArgOfPericenter 20	// random
		MeanAnomaly     0	// random
	}
}

// data from arXiv:1311.7664v2 
Star	"FW Tau B"
{
	ParentBody  "FW Tau"
	Class       "M4V"
	MassSol     0.28
	Age         0.0018

	Orbit   // random data
	{
		SemiMajorAxis   5.5	// 11 AU mutual separation
		Inclination     0	// random
		ArgOfPericenter 200	// random
		MeanAnomaly     0	// random
	}
}

Star	"HU Aqr A"
{
	ParentBody  "HU Aqr"
	Class       "DA"
	Luminosity  0.0022
	MassSol     0.88
	Radius      6960
	Temperature 12500

	Orbit
	{
		Period          0.00027
		SemiMajorAxis   0.0006
		Eccentricity    0
		Inclination     85
		ArgOfPericenter 10
		MeanAnomaly     0
	}
}

Star	"HU Aqr B"
{
	ParentBody  "HU Aqr"
	Class       "M4V"
	Luminosity  0.0052
	MassSol     0.2
	Radius      153120
	Temperature 3400

	Orbit
	{
		Period          0.00027
		SemiMajorAxis   0.0026
		Eccentricity    0
		Inclination     85
		ArgOfPericenter 190
		MeanAnomaly     0
	}
}

Star	"HW Vir A"
{
	ParentBody  "HW Vir"
	Class       "B5VI"
	MassSol     1.35
	Radius      550000

	Orbit
	{
		Period          0.0003197741
		ArgOfPericenter 205
		MeanAnomaly     0
	}
}

Star	"HW Vir B"
{
	ParentBody  "HW Vir"
	Class       "M6V"
	MassSol     0.14
	Radius      125280
	Temperature 3084

	Orbit
	{
		Period          0.0003197741
		ArgOfPericenter 25
		MeanAnomaly     0
	}
}

Star	"NY Vir A"
{
	ParentBody  "NY Vir"
	Class       "DA"
	MassSol     1.29 	// calculated by SE
	Temperature 33000

	Orbit
	{
		Period          2.7379e-4
		SemiMajorAxis   0.003696	// calculated by SE
		ArgOfPericenter 20
		MeanAnomaly     0
	}
}

Star	"NY Vir B"
{
	ParentBody  "NY Vir"
	Class       "M4V"
	MassSol     0.49 	// calculated by SE
	Temperature 3000

	Orbit
	{
		Period          2.7379e-4
		SemiMajorAxis   0.001404	// calculated by SE
		ArgOfPericenter 200
		MeanAnomaly     0
	}
}

Star	"NN Ser A"
{
	ParentBody  "NN Ser"
	Class       "DA1"
	Lum         4.2
	MassSol     0.535
	Radius      14685.6
	Temperature 57000

	Orbit
	{
		Period          0.0003559282
		SemiMajorAxis   0.00068367
		Eccentricity    0
		Inclination     89.6
		ArgOfPericenter 263.464
		MeanAnomaly		0
	}
}

Star	"NN Ser B"
{
	ParentBody  "NN Ser"
	Class       "M4V"
	Lum         0.00172
	MassSol     0.111
	Radius      107184
	Temperature 3000

	Orbit
	{
		Period          0.0003559282
		SemiMajorAxis   0.00329517
		Eccentricity    0
		Inclination     89.6
		ArgOfPericenter 83.464
		MeanAnomaly		0
	}
}

Star	"UZ For A"	// Data copied from DP Leo
{
	ParentBody  "UZ For"
	Class       "DA1"
	Lum         0.00449
	MassSol     0.6
	Radius      7934.4
	Temperature 13500

	Orbit
	{
		Period          0.0001708
		SemiMajorAxis   0.0003522
		Eccentricity    0
		Inclination     79.5
		ArgOfPericenter 20
		MeanAnomaly     0
	}
}

Star	"UZ For B"	// Data copied from DP Leo
{
	ParentBody  "UZ For"
	Class       "M5V"
	Lum         0.00105
	MassSol     0.09
	Radius      83520
	Temperature 3000

	Orbit
	{
		Period          0.0001708
		SemiMajorAxis   0.0023478
		Eccentricity    0
		Inclination     79.5
		ArgOfPericenter 200
		MeanAnomaly     0
	}
}

Star	"SR 12 A"
{
	ParentBody  "SR 12"
	Class       "K4V"
	MassSol     0.68 // random

	Orbit	//	random orbit
	{
		Period          0.09
		SemiMajorAxis   0.096
		AscendingNode   18
		ArgOfPericenter 20
		MeanAnomaly     0
	}
}

Star	"SR 12 B"
{
	ParentBody  "SR 12"
	Class       "M2V"
	MassSol     0.32 // random

	Orbit	//	random orbit
	{
		Period          0.09
		SemiMajorAxis   0.204
		AscendingNode   18
		ArgOfPericenter 200
		MeanAnomaly     0
	}
}

Star	"Kepler-16 A"
{
	ParentBody  "Kepler-16"
	Class       "KV"
	MassSol     0.6897
	Radius      451634.4
	Temperature 4450

	Orbit
	{
		Period          0.112471
		SemiMajorAxis   0.050921
		Eccentricity    0.15944
		Inclination     90.0322
		ArgOfPericenter 20
		MeanAnomaly     0
	}
}

Star	"Kepler-16 B"
{
	ParentBody  "Kepler-16"
	Class       "MV"
	MassSol     0.20255
	Radius      157456.08
	Temperature 3000

	Orbit
	{
		Period          0.112471
		SemiMajorAxis   0.173389
		Eccentricity    0.15944
		Inclination     90.0322
		ArgOfPericenter 200
		MeanAnomaly     0
	}
}

Star	"Kepler-34 A"
{
	ParentBody  "Kepler-34"
	Class       "G0V"
	MassSol     1.048
	Radius      808752
	Temperature 5913

	Orbit
	{
		Period          0.076102378
		SemiMajorAxis   0.11301
		Eccentricity    0.52
		Inclination     89.858
		ArgOfPericenter 210
		MeanAnomaly     0
	}
}

Star	"Kepler-34 B"
{
	ParentBody  "Kepler-34"
	Class       "G0V"
	MassSol     1.021
	Radius      760728
	Temperature 5867

	Orbit
	{
		Period          0.076102378
		SemiMajorAxis   0.11599
		Eccentricity    0.52
		Inclination     89.858
		ArgOfPericenter 30
		MeanAnomaly     0
	}
}

Star	"Kepler-35 A"
{
	ParentBody  "Kepler-35"
	MassSol     0.8877
	RadSol      1.0284
	Teff        5606
	FeH         -0.13

	Orbit
	{
		Period          0.0567678
		SemiMajorAxis   0.0839
		Eccentricity    0.142
		Inclination     90
		ArgOfPericenter 20
		MeanAnomaly     0
	}
}

Star	"Kepler-35 B"
{
	ParentBody  "Kepler-35"
	MassSol     0.8094
	RadSol      0.7861
	Teff        5202
	FeH         -0.13

	Orbit
	{
		Period          0.0567678
		SemiMajorAxis   0.0921
		Eccentricity    0.142
		Inclination     90
		ArgOfPericenter 200
		MeanAnomaly     0
	}
}

Star	"Kepler-38 A"
{
	ParentBody  "Kepler-38"
	MassSol      0.949
	RadSol       1.757
	Teff         5640
	FeH          -0.158
	Age          10

	Orbit
	{
		Period          0.051460017
		SemiMajorAxis   0.03053	// 0.1469 * mass ratio
		Eccentricity    0.1032
		Inclination     89.265
		ArgOfPericenter 268.68
		MeanAnomaly     0
	}
}

Star	"Kepler-38 B"
{
	ParentBody  "Kepler-38"
	MassSol     0.249
	RadSol      0.2724
	Temperature 3000

	Orbit
	{
		Period          0.051460017
		SemiMajorAxis   0.11637	// 0.1469 * mass ratio
		Eccentricity    0.1032
		Inclination     89.265
		ArgOfPericenter 88.68
		MeanAnomaly     0
	}
}

Star	"Kepler-47 A"
{
	ParentBody  "Kepler-47"
	Class       "GV"
	MassSol     1.043
	Radius      670944
	Temperature 5636
	Lum         0.84
	FeH        -0.25

	Orbit
	{
		Period          0.02039298
		SemiMajorAxis   0.02153964	// 0.0836 * mass ratio
		Eccentricity    0.0234
		Inclination     89.34
		ArgOfPericenter 20
		MeanAnomaly     0
	}
}

Star	"Kepler-47 B"
{
	ParentBody  "Kepler-47"
	Class       "MV"
	MassSol     0.362
	Radius      244000
	Temperature 3357
	Lum         0.014

	Orbit
	{
		Period          0.02039298
		SemiMajorAxis   0.06206036	// 0.0836 * mass ratio
		Eccentricity    0.0234
		Inclination     89.34
		ArgOfPericenter 200
		MeanAnomaly     0
	}
}

Barycenter	"Kepler-64 (AB)"
{
	ParentBody  "Kepler-64"
	MassSol     1.936

	Orbit
	{
		Period          7000
		SemiMajorAxis   436	// 1000 * mass ratio
		Inclination     85	// random value
		ArgOfPericenter 0	// random value
		MeanAnomaly     0	// random value
	}
}

Barycenter	"Kepler-64 (CD)"
{
	ParentBody  "Kepler-64"
	MassSol     1.5

	Orbit
	{
		Period          7000
		SemiMajorAxis   564	// 1000 * mass ratio
		Inclination     85	// random value
		ArgOfPericenter 180	// random value
		MeanAnomaly     0	// random value
	}
}

Star	"Kepler-64 A"
{
	ParentBody "Kepler-64 (AB)"
	Class      "F8IV"
	MassSol     1.528
	Radius      1206864
	FeH         0.21
	Age         2.6

	Orbit
	{
		Epoch           2454967.81936	// eclipse
		Period          0.054758867
		SemiMajorAxis   0.036753719		// 0.1744 * mass ratio
		Eccentricity    0.2117
		Inclination     87.612			// eclipsing binary
		ArgOfPericenter 219.3
		MeanAnomaly     0
	}
}

Star	"Kepler-64 B"
{
	ParentBody "Kepler-64 (AB)"
	Class      "MV"
	MassSol     0.408
	Radius      263088
	FeH         0.21
	Age         2.6

	Orbit
	{
		Epoch           2454967.81936	// eclipse
		Period          0.054758867
		SemiMajorAxis   0.137646281		// 0.1744 * mass ratio
		Eccentricity    0.2117
		Inclination     87.612			// eclipsing binary
		ArgOfPericenter 39.3
		MeanAnomaly     0
	}
}

Star	"Kepler-64 C"
{
	ParentBody "Kepler-64 (CD)"
	Class      "G3V"
	MassSol     0.99

	Orbit
	{
		Period          203.469	// calculated
		SemiMajorAxis   20.4	// 60 * mass ratio
		Eccentricity    0.4		// random value
		Inclination     86	    // random value
		ArgOfPericenter 0		// random value
		MeanAnomaly     0		// random value
	}
}

Star	"Kepler-64 D"
{
	ParentBody  "Kepler-64 (CD)"
	Class       "MV"
	MassSol     0.51

	Orbit
	{
		Period          203.469	// calculated
		SemiMajorAxis   39.6	// 60 * mass ratio
		Eccentricity    0.4		// random value
		Inclination     86	    // random value
		ArgOfPericenter 180		// random value
		MeanAnomaly     0		// random value
	}
}

// TODO: Kepler-413 actually a triple system

Star	"Kepler-413 A"
{
	ParentBody  "Kepler-413"
	AppMagn      15.52
	MassSol      0.82
	RadSol       0.7761
	Teff         4700
	FeH          -0.2
	
	RotationPeriod 315.6

	Orbit
	{
		Period          0.0276967
		SemiMajorAxis   0.03053	// mass ratio * 0.1015
		Eccentricity    0.037
		ArgOfPericenter 260		// random
		MeanAnomaly     0		// random
	}
}

Star	"Kepler-413 B"
{
	ParentBody  "Kepler-413"
	MassSol     0.5423
	RadSol      0.484

	Orbit
	{
		Period          0.0276967
		SemiMajorAxis   0.11637	// mass ratio * 0.1015
		Eccentricity    0.037
		ArgOfPericenter 80		// random
		MeanAnomaly     0		// random
	}
}

// data from arXiv:1409.1605v1
Star	"Kepler-453 A"
{
	ParentBody	"Kepler-453"
	AppMagn		13.552 // KOI-1451 value from the article
	MassSol		0.934
	RadSol		0.833
	Teff        5527
	FeH         0.09
	Age         1.25

	RotationPeriod 487.44

	Orbit
	{
		Epoch           2454964
		Period          0.07480526
		SemiMajorAxis	0.031754 // mass ratio 0.208 and a = 0.18479
		Eccentricity	0.051
		Inclination		90.275
		ArgOfPericenter	82.86
		MeanAnomaly		0
	}
}

// data from arXiv:1512.03428v1
Star	"Kepler-444 A"
{
	ParentBody	"Kepler-444"
	AppMagn     8.88	// 0.85% of the system flux
	Class      "K0V"
	MassSol		0.758
	RadSol      0.752
	Teff        5046
	FeH        -0.55
	Age         11.23

	Orbit
	{
		Epoch           2456511.83
		Period          198
		SemiMajorAxis	10.624	// mass ratio 0.71 and a = 36.7
		Eccentricity	0.864
		Inclination		90.4
		AscendingNode   73.1
		ArgOfPericenter	162.8
		//MeanLongitude   183.5
		MeanAnomaly     127.6
	}
}

Barycenter	"Kepler-444 (BC)"
{
	ParentBody	"Kepler-444"
	MassSol		0.54

	Orbit
	{
		Epoch           2456511.83
		Period          198
		SemiMajorAxis	26.076	// mass ratio 0.71 and a = 36.7
		Eccentricity	0.864
		Inclination		90.4
		AscendingNode   73.1
		ArgOfPericenter	342.8
		//MeanLongitude   183.5
		MeanAnomaly     127.6
	}
}

Star	"Kepler-444 B"
{
	ParentBody	"Kepler-444 (BC)"
	//AbsMagn     6.91	// K band
	MassSol		0.29

	Orbit
	{
		Epoch           2456511.83
		SemiMajorAxis	0.139	// mass ratio 0.537 and a = 0.3
		Eccentricity	0
		Inclination		90		// assumed co-planar
		AscendingNode   73		// assumed co-planar
		ArgOfPericenter	123		// random
		MeanAnomaly		0
	}
}

Star	"Kepler-444 C"
{
	ParentBody	"Kepler-444 (BC)"
	//AbsMagn     7.21	// K band
	MassSol		0.25

	Orbit
	{
		Epoch           2456511.83
		SemiMajorAxis	0.161	// mass ratio 0.537 and a = 0.3
		Eccentricity	0
		Inclination		90		// assumed co-planar
		AscendingNode   73		// assumed co-planar
		ArgOfPericenter	303		// random
		MeanAnomaly		0
	}
}

// data from arXiv:1409.1605v1
Star	"Kepler-453 B"
{
	ParentBody	"Kepler-453"
	AppMagn     18.719	// 0.85% of the system flux
	MassSol		0.1938
	RadSol		0.2143
	Teff        3309

	Orbit
	{
		Epoch           2454964
		Period          0.07480526
		SemiMajorAxis	0.1530359 // mass ratio 0.208 and a = 0.18479
		Eccentricity	0.051
		Inclination		90.275
		ArgOfPericenter	262.86
		MeanAnomaly		0
	}
}

Star	"HD 41004 A/HIP 28393 A"
{
	ParentBody  "HD 41004"
	AppMagn 8.65
	AbsMagn 5.51
	Class  "K1V"
	MassSol 0.7
	Teff    5035
	FeH    -0.09
	Age     1.64

	Orbit
	{
		Period          8.0		// calculated
		SemiMajorAxis   2.353	// 6.47 * mass ratio
		Eccentricity    0.0		// random value
		ArgOfPericenter 0		// random value
		MeanAnomaly     0		// random value
	}
}

Star	"HD 41004 B/HIP 28393 B"
{
	ParentBody  "HD 41004"
	AppMagn 12.33
	AbsMagn 9.16
	Class  "M2V"
	MassSol 0.4
	Teff    5035
	FeH    -0.01
	Age     1.56

	Orbit
	{
		Period          8.0		// calculated
		SemiMajorAxis   4.117	// 6.47 * mass ratio
		Eccentricity    0.0		// random value
		ArgOfPericenter 180		// random value
		MeanAnomaly     0		// random value
	}
}

Star	"RR Cae A/Gliese 2034 A"
{
	ParentBody  "RR Cae"
	Class       "DA8"
	MassSol     0.44
	Radius      10440
	Temperature 7540

	Orbit   // random data
	{
		Period          0.01998674
		Inclination     90 // eclipsing binary
		ArgOfPericenter 20
		MeanAnomaly     0
	}
}

Star	"RR Cae B/Gliese 2034 B"
{
	ParentBody  "RR Cae"
	Class       "M4V"
	MassSol     0.182
	Radius      141288
	Temperature 3100

	Orbit   // random data
	{
		Period          0.01998674
		Inclination     90 // eclipsing binary
		ArgOfPericenter 200
		MeanAnomaly     0
	}
}

// data from arXiv:1311.7664v2 
Star	"ROXs 42B A"
{
	ParentBody  "ROXs 42B"
	Class       "M0V"
	MassSol     0.89
	Teff        2200
	Age         0.0068

	Orbit   // random data
	{
		//Period          9.962	// calculated
		SemiMajorAxis   2.88	// mass ratio * 10 AU mutual separation
		Eccentricity    0.8		// random, components are approaching now, could be caused by high orbit inclination or eccentricity
		Inclination     0		// random
		ArgOfPericenter 20		// random
		MeanAnomaly     0		// random
	}
}

// data from arXiv:1311.7664v2 
Star	"ROXs 42B B"
{
	ParentBody  "ROXs 42B"
	MassSol     0.36
	Age         0.0068

	Orbit   // random data
	{
		//Period          9.962	// calculated
		SemiMajorAxis   7.12	// mass ratio * 10 AU mutual separation
		Eccentricity    0.8		// random, components are approaching now, could be caused by high orbit inclination or eccentricity
		Inclination     0		// random
		ArgOfPericenter 200		// random
		MeanAnomaly     0		// random
	}
}

Barycenter	"Gliese 667 (AB)"
{
	ParentBody "Gliese 667"
	MassSol     1.42  // total mass

	Orbit
	{
		Period         564.5	// calculated by SE
		SemiMajorAxis  18		// 100 * mass ratio
		Eccentricity   0.3
		Inclination    120		// from AB pair
		AscendingNode  310		// from AB pair
		ArgOfPericen   270		// random
		MeanAnomaly    0
	}
}

Star	"Gliese 667 A/HD 156384 A"
{
	ParentBody "Gliese 667 (AB)"
	Class      "K3V"
	AppMagn     5.91
	MassSol     0.73
	Radius      528960
	FeH        -0.59
	Age         2

	Orbit
	{
		Epoch          2442742.6630093779 // 1975.9
		Period         42.15
		SemiMajorAxis  6.1225	// 12.6 * mass ratio
		Eccentricity   0.58
		Inclination    128
		AscendingNode  313
		ArgOfPericen   67
		MeanAnomaly    0
	}
}

Star	"Gliese 667 B/HD 156384 B"
{
	ParentBody "Gliese 667 (AB)"
	Class      "K5V"
	AppMagn     7.20
	MassSol     0.69
	Radius      487200
	FeH        -0.59
	Age         2

	Orbit
	{
		Epoch          2442742.6630093779 // 1975.9
		Period         42.15
		SemiMajorAxis  6.4775	// 12.6 * mass ratio
		Eccentricity   0.58
		Inclination    128
		AscendingNode  313
		ArgOfPericen   247
		MeanAnomaly    0
	}
}

Star	"Gliese 667 C/HD 156384C"
{
	ParentBody "Gliese 667"
	Class      "M1.5V"
	AppMagn     10.20
	MassSol     0.31
	Radius      292320
	Temperature 3700
	FeH        -0.59
	Age         2

	RotationPeriod  2520 // hours

	Orbit
	{
		Period         564.5	// calculated by SE
		SemiMajorAxis  82 		// 100 * mass ratio
		Eccentricity   0.3
		Inclination    120		// from AB pair
		AscendingNode  310		// from AB pair
		ArgOfPericen   90		// random
		MeanAnomaly    0
	}
}

Star	"Gliese 676 A"
{
	ParentBody "Gliese 676"
	Class  "M0V"
	AppMagn 9.59
	Lum     0.082
	MassSol 0.71
	FeH     0.23

	Orbit
	{
		SemiMajorAxis  232	// 800 * mass ratio
		ArgOfPericen   90	// random
		MeanAnomaly    0
	}
}

Star	"Gliese 676 B"
{
	ParentBody "Gliese 676"
	Class  "M3V"
	MassSol 0.29

	Orbit
	{
		SemiMajorAxis  568	// 800 * mass ratio
		ArgOfPericen   270	// random
		MeanAnomaly    0
	}
}

Star	"HR 7162 A/BD+32 3267 A/HD 176051 A/HIP 93017 A/SAO 67612 A"
{
	ParentBody  "HD 176051"
	Class       "F9V"
	AbsMagn     4.33
	MassSol     1.07
	Temperature 6000

	Orbit
	{
		Period          61.4
		SemiMajorAxis   7.6
		Eccentricity    0.2667
		Inclination     114.2
		ArgOfPericen    0
		MeanAnomaly     0
	}
}

Star	"HR 7162 B/BD+32 3267 B/HD 176051 B/HIP 93017 B/SAO 67612 B"
{
	ParentBody  "HD 176051"
	Class       "K1V"
	AbsMagn     6.65
	MassSol     0.71

	Orbit
	{
		Period          61.4
		SemiMajorAxis   11.5
		Eccentricity    0.2667
		Inclination     114.2
		ArgOfPericen    180
		MeanAnomaly     0
	}
}

// data from A.A.Tokovinin, R.F.Griffin et al, The Triple System HR 7272
Star	"HD 178911 B/HIP 94075/BD+34 3438/SAO 67875"
{
	ParentBody  "HD 178911"
	Class  "G5V"
	AppMagn 8.12
	MassSol 1.07
	RadSol  1.14
	Teff    5650
	FeH     0.28
	Age     5.2

	Orbit
	{
		SemiMajorAxis   480 // mass ratio * 752
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter	"HD 178911 A/HIP 94076/BD+34 3439/SAO 67879"
{
	ParentBody  "HD 178911"
	AppMagn 6.74
	MassSol 1.89

	Orbit
	{
		SemiMajorAxis   272 // mass ratio * 752
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star	"HD 178911 Aa"
{
	ParentBody  "HD 178911 A"
	Class  "G1V"
	AppMagn 6.90
	MassSol 1.1
	FeH     0.06

	Orbit
	{
		Epoch           2450572.2
		Period          3.54915
		SemiMajorAxis   1.436 // mass ratio * 3.435
		Eccentricity    0.589
		Inclination     150
		AscendingNode   267.7
		ArgOfPericenter 262.5
		MeanAnomaly     0
	}
}

Star	"HD 178911 Ab"
{
	ParentBody  "HD 178911 A"
	Class  "K1V"
	AppMagn 8.90
	MassSol 0.79

	Orbit
	{
		Epoch           2450572.2
		Period          3.54915
		SemiMajorAxis   1.999 // mass ratio * 3.435
		Eccentricity    0.589
		Inclination     150
		AscendingNode   267.7
		ArgOfPericenter 82.5
		MeanAnomaly     0
	}
}

Barycenter	"16 Cyg (AC)"
{
	ParentBody "16 Cyg"
	Orbit
	{
		Period          18212.2
		SemiMajorAxis   440.94   // mass ratio 1:1
		Eccentricity    0.862
		Inclination     53.57
		AscendingNode   354.87
		ArgOfPericenter 7.15
		MeanAnomaly     38.67
	}
}

Star    "16 Cyg A/Gliese 765.1 A/HD 186408/HIP 96895/HR 7503/SAO 31898/Struve 4046 A"
{
	ParentBody "16 Cyg (AC)"
	Class      "G1.5 V"
	MassSol     1.02
	Radius      1183200
	AppMagn     5.96
	Luminosity  1.6
	Teff        5803
	FeH         0.0569
	Age         10.4

	RotationPeriod  645.6	// 26.9 days

	Orbit
	{
		Period          431	// calculated
		SemiMajorAxis   12	// 73 * mass ratio
		Eccentricity    0.4	// random value
		Inclination     50	// random value
		ArgOfPericenter 180	// random value
		MeanAnomaly     0	// random value
	}
}

Star    "16 Cyg C"
{
	ParentBody "16 Cyg (AC)"
	Class      "M V"
	MassSol    0.2	// some plausible value

	Orbit
	{
		Period          431	// calculated
		SemiMajorAxis   61	// 73 * mass ratio
		Eccentricity    0.4	// random value
		Inclination     50	// random value
		ArgOfPericenter 0	// random value
		MeanAnomaly     0	// random value
	}
}

Star    "16 Cyg B/Gliese 765.1 B/HD 186427/HIP 96901/HR 7504/SAO 31899/Struve 4046 B/KIC 12069449"
{
	ParentBody "16 Cyg"
	Class      "G2.5 V"
	MassSol     1.01
	Radius      835200
	AppMagn     6.2
	Luminosity  1.3
	Teff        5572
	FeH         0.0899
	Age         9.9

	RotationPeriod  698.4	// 29.1 days

	Orbit
	{
		Period          18212.2
		SemiMajorAxis   440.94   // est. mass ratio 1:1
		Eccentricity    0.862
		Inclination     53.57
		AscendingNode   354.87
		ArgOfPericenter 187.15
		MeanAnomaly     38.67
	}
}

Star    "Copernicus/55 Cnc A"
{
	ParentBody "55 Cnc"
	Class      "K0 V" // K0IV-V
	MassSol     0.95
	Radius      765600
	AppMagn     5.95
	Age         7.4 // 10.2

	Orbit
	{
		Period          32124.52834
		SemiMajorAxis   124.0366972
		Eccentricity    0.5
		Inclination     50
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star    "55 Cnc B"
{
	ParentBody "55 Cnc"
	Class      "M4 V"
	MassSol     0.13
	Radius      208800
	AppMagn     13.15
	Age         7.4 // 10.2

	Orbit
	{
		Period          32124.52834
		SemiMajorAxis   915.9633028
		Eccentricity    0.5
		Inclination     50
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star    "83 Leo A/Gliese 429 A/HR 4414/HIP 55846/HD 99491"
{
	ParentBody "83 Leo"
	Class      "K0 IV"
	MassSol     1
	Radius      1322400
	Luminosity  0.66
	Age         4

	Orbit
	{
		Period          32000
		SemiMajorAxis   337	// 720 * mass ratio
		Eccentricity    0.43
		Inclination     126.6
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star    "83 Leo B/Gliese 429 B/HIP 55848/HD 99492"
{
	ParentBody "83 Leo"
	Class      "K2 V"
	MassSol     0.88
	Radius      563760
	Luminosity  0.24
	Age         4

	Orbit
	{
		Period          32000
		SemiMajorAxis   383	// 720 * mass ratio
		Eccentricity    0.43
		Inclination     126.6
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star	"Algieba A/GAM1 Leo/GAM Leo A/HD 89484/HR 4057/SAO 81298"
{
	ParentBody "GAM Leo"
	Class  "K1IIIb"
	MassSol 1.23
	RadSol  31.88
	AppMagn 2.28
	Lum     320
	Teff    4330
	FeH    -0.51

	Orbit
	{
		Epoch           2401671.3
		Period          510.3
		Eccentricity    0.845
		Inclination     76
		AscendingNode   143.4
		ArgOfPericenter 180	// random
		MeanAnomaly     0
	}
}

Star	"Algieba B/GAM2 Leo/GAM Leo B/HD 89485/HR 4058/SAO 81299"
{
	ParentBody "GAM Leo"
	Class  "G7IIIb"
	MassSol 1.23
	RadSol  10
	AppMagn 3.51
	Lum     40
	Teff    4980
	FeH    -0.52

	Orbit
	{
		Epoch           2401671.3
		Period          510.3
		Eccentricity    0.845
		Inclination     76
		AscendingNode   143.4
		ArgOfPericenter 0	// random
		MeanAnomaly     0
	}
}

// data from arXiv:1503.01211v1
Barycenter	"30 Ari A/HD 16246/HIP 12189/HR 765/SAO 75471"
{
	ParentBody "30 Ari"
	MassSol 1.71

	Orbit
	{
		SemiMajorAxis   727	// 1500 * mass ratio
		Eccentricity    0.4	// random
		ArgOfPericenter 0	// random
		MeanAnomaly     0
	}
}

Star	"30 Ari Aa"
{
	ParentBody "30 Ari A"
	Class  "F5V"
	AppMagn 6.48
	MassSol 1.31
	RadSol  1.37
	Lum     3.6
	Teff    6300
	FeH     0.245
	Age     0.86

	Orbit
	{
		Period          0.0030117
		Eccentricity    0	// guess
		ArgOfPericenter 60	// random
		MeanAnomaly     0
	}
}

Star	"30 Ari Ab"
{
	ParentBody "30 Ari A"
	Class  "M1V"	// random
	MassSol 0.4		// random

	Orbit
	{
		Period          0.0030117
		Eccentricity    0	// guess
		ArgOfPericenter 240	// random
		MeanAnomaly     0
	}
}

Barycenter	"30 Ari (BC)/HD 16232/HIP 12184/HR 764/SAO 75470"
{
	ParentBody "30 Ari"
	MassSol 1.61

	Orbit
	{
		SemiMajorAxis   773	// 1500 * mass ratio
		Eccentricity    0.4	// random
		ArgOfPericenter 180	// random
		MeanAnomaly     0
	}
}

Star	"30 Ari B"
{
	ParentBody "30 Ari (BC)"
	Class  "F8V"
	AppMagn 7.09
	MassSol 1.11
	RadSol  1.13
	Lum     1.964
	Teff    6424
	FeH     0.7
	Age     0.91

	Orbit
	{
		Period          80
		SemiMajorAxis   6.9	// 22.3 * mass ratio
		ArgOfPericenter 180	// random
		MeanAnomaly     0
	}
}

Star	"30 Ari C"
{
	ParentBody "30 Ari (BC)"
	Class  "M1V"
	AppMagn 12.09	// 5 mag fainter
	MassSol 0.50

	Orbit
	{
		Period          80
		SemiMajorAxis   15.4	// 22.3 * mass ratio
		ArgOfPericenter 0	// random
		MeanAnomaly     0
	}
}
Star	"Gliese 15 A/Groombridge 34 A/GX And/HD 1326 A/HIP 1475 A/SAO 36248 A"
{
	ParentBody "Gliese 15"
	AppMagn 8.09
	Class  "M1.5V"
	MassSol 0.38
	RadSol  0.39
	Teff    3567
	FeH    -0.32

	Orbit
	{
		Epoch           2401754
		Period          2600
		Eccentricity    0
		Inclination     61.4
		AscendingNode   45.3
		ArgOfPericenter 0	// random
		MeanAnomaly     0
	}
}

Star	"Gliese 15 B/Groombridge 34 B/GQ And/HD 1326 B/HIP 1475 B/SAO 36248 B"
{
	ParentBody "Gliese 15"
	AppMagn 11.06
	Class  "M3.5V"
	MassSol 0.163
	RadSol  0.19
	Teff    3000

	Orbit
	{
		Epoch           2401754
		Period          2600
		Eccentricity    0
		Inclination     61.4
		AscendingNode   45.3
		ArgOfPericenter 180	// random
		MeanAnomaly     0
	}
}

Star	"Gliese 3021 A/HD 1237 A/HIP 1292 A"
{
	ParentBody "Gliese 3021"
	AppMagn 6.59
	Class  "G6V"
	MassSol 0.9
	RadSol  0.9
	Teff    5540
	FeH     0.1
	Age     8.77

	Orbit
	{
		SemiMajorAxis   13	// mass ratio * 68
		ArgOfPericenter 0	// random
		MeanAnomaly     0
	}
}

Star	"Gliese 3021 B/HD 1237 B/HIP 1292 B"
{
	ParentBody "Gliese 3021"
	Class  "M4V"
	MassSol 0.2 // guess

	Orbit
	{
		SemiMajorAxis   55	// mass ratio * 68
		ArgOfPericenter 180	// random
		MeanAnomaly     0
	}
}

Star	"EPS Ret A/HD 27442 A/HIP 19921 A"
{
	ParentBody "EPS Ret"
	Class  "K2IV"
	AppMagn 4.44
	MassSol 1.2
	RadSol  6.6
	Teff    4749
	FeH     0.22
	Age     7.15

	Orbit
	{
		SemiMajorAxis   110	// mass ratio * 240
		Eccentricity    0.5	// random
		ArgOfPericenter 0	// random
		MeanAnomaly     0
	}
}

Star	"EPS Ret B/HD 27442 B/HIP 19921 B/WD 0415-594"
{
	ParentBody "EPS Ret"
	Class  "WD"
	MassSol 1.0		// random
	AppMagn 12.5	// SIMBAD
	Teff    15000

	Orbit
	{
		SemiMajorAxis   130	// mass ratio * 240
		Eccentricity    0.5	// random
		ArgOfPericenter 180	// random
		MeanAnomaly     0
	}
}

Star	"HD 196885 A/HIP 101966 A/HR 7907 A/SAO 106360 A"
{
	ParentBody "HD 196885"
	Class  "F8V"
	AppMagn 6.398
	MassSol 1.28
	RadSol  1.31
	Teff    6254
	FeH     0.22
	Age     2

	Orbit
	{
		SemiMajorAxis   6	// mass ratio * 24
		Eccentricity    0.5	// random
		ArgOfPericenter 0	// random
		MeanAnomaly     0
	}
}

Star	"HD 196885 B/HIP 101966 B/HR 7907 B/SAO 106360 B"
{
	ParentBody "HD 196885"
	Class  "M1V"	// SIMBAD
	MassSol 0.42	// guess

	Orbit
	{
		SemiMajorAxis   18	// mass ratio * 24
		Eccentricity    0.5	// random
		ArgOfPericenter 180	// random
		MeanAnomaly     0
	}
}

Star	"WASP-70 A/HD 358155"
{
	ParentBody "WASP-70"
	Class  "G4V"
	AppMagn 10.79
	MassSol 1.106
	RadSol  1.215
	Teff    5763
	FeH    -0.006
	Age     9.5

	Orbit
	{
		SemiMajorAxis   320	// mass ratio * 800
		Eccentricity    0.6	// random
		ArgOfPericenter 60	// random
		MeanAnomaly     0
	}
}

Star	"WASP-70 B"
{
	ParentBody "WASP-70"
	Class  "K3V"
	MassSol 0.8
	Teff    4900

	Orbit
	{
		SemiMajorAxis   480	// mass ratio * 800
		Eccentricity    0.6	// random
		ArgOfPericenter 240 // random
		MeanAnomaly     0
	}
}

Star	"KELT-2 A/HD 42176 A/HIP 29301 A/SAO 58830 A"
{
	ParentBody "KELT-2"
	Class  "F8V"
	AppMagn 8.713
	MassSol 1.314
	RadSol  1.836
	Teff    6151
	FeH    -0.015
	Age     3.97

	Orbit
	{
		SemiMajorAxis   98	// mass ratio * 295
		Eccentricity    0.1	// random
		ArgOfPericenter 90	// random
		MeanAnomaly     0
	}
}

Star	"KELT-2 B/HD 42176 B/HIP 29301 B/SAO 58830 B"
{
	ParentBody "KELT-2"
	Class  "K1V"
	AppMagn 12
	MassSol 0.65 // guess

	Orbit
	{
		SemiMajorAxis   197	// mass ratio * 295
		Eccentricity    0.1	// random
		ArgOfPericenter 270 // random
		MeanAnomaly     0
	}
}

Star	"HD 106515 A/HIP 59743 A/GJ 9398 A/LTT 4599/SAO 138674"
{
	ParentBody "HD 106515"
	Class  "G5V"
	MassSol 0.97
	RadSol  1.62
	AppMagn 7.971
	Lum     1.23
	Teff    5362
	FeH     0.03
	Age     6

	Orbit
	{
		SemiMajorAxis   187	// mass ratio * 390
		Eccentricity    0.1	// random
		ArgOfPericenter 90	// random
		MeanAnomaly     0
	}
}

Star	"HD 106515 B/HIP 59743 B/GJ 9398 B/LTT 4598/SAO 138673"
{
	ParentBody "HD 106515"
	Class  "G8V"
	MassSol 0.89
	AppMagn 8.3

	Orbit
	{
		SemiMajorAxis   203	// mass ratio * 390
		Eccentricity    0.1	// random
		ArgOfPericenter 270	// random
		MeanAnomaly     0
	}
}

Star	"MU2 Oct A/HD 196067/HIP 102125/SAO 257836/LTT 8159/IDS 20298-7542 A/HR 7864"
{
	ParentBody "MU2 Oct"
	Class  "G0V"
	MassSol 1.29
	RadSol  1.73
	AppMagn 6.02
	Lum     3.73
	Teff    6017
	FeH     0.18
	Age     3.3

	Orbit
	{
		SemiMajorAxis   445.25	// mass ratio * 932
		Eccentricity    0.1	// random
		ArgOfPericenter 0	// random
		MeanAnomaly     0
	}
}

Star	"MU2 Oct B/HD 196068/HIP 102128/SAO 257837/LTT 8160/IDS 20298-7542 B"
{
	ParentBody "MU2 Oct"
	Class  "G1V"
	MassSol 1.18
	AppMagn 7.18
	Teff    5768

	Orbit
	{
		SemiMajorAxis   486.75	// mass ratio * 932
		Eccentricity    0.1	// random
		ArgOfPericenter 180	// random
		MeanAnomaly     0
	}
}

// data from arXiv:1312.4938
Star	"Kepler-410 A/KOI-42 A/KIC 8866102 A"
{
	ParentBody "Kepler-410"
	Class  "F5V"	// guess
	MassSol 1.3
	RadSol  1.41
	AppMagn 9.5
	Teff    6375
	FeH     0.09
	Age     2.76
	
	Obliquity 82.5
	
	Orbit
	{
		SemiMajorAxis   71	// mass ratio * 210
		Inclination     85	// assumed to be near the planet's b inclination and the star's A obliquity
		ArgOfPericenter 0	// random
		MeanAnomaly     0
	}
}

Star	"Kepler-410 B/KOI-42 B/KIC 8866102 B"
{
	ParentBody "Kepler-410"
	Class  "K5V"	// guess
	MassSol 0.66	// guess
	AppMagn 12.2
	Teff    4850

	Orbit
	{
		SemiMajorAxis   139	// mass ratio * 210
		Inclination     85	// assumed to be near the planet's b inclination and the star's A obliquity
		ArgOfPericenter 180	// random
		MeanAnomaly     0
	}
}

// data from arXiv:1406.6172.pdf
Star	"Kepler-420 A/KOI-1257 A/KIC 8751933 A"
{
	ParentBody "Kepler-420"
	Class  "G5V"
	AppMagn 14.867
	MassSol 0.99
	RadSol  1.13
	Teff    5520
	FeH     0.27
	Age     9.3

	Orbit
	{
		Period          9.39
		SemiMajorAxis   2.2	// mass ratio * 5.3
		Eccentricity    0.31
		Inclination     18.2
		ArgOfPericenter 0	// from article
		MeanAnomaly     0
	}
}

Star	"Kepler-420 B/KOI-1257 B/KIC 8751933 B"
{
	ParentBody "Kepler-420"
	Class  "G6V"
	AppMagn 17.494 // 8.9% of the primary luminosity
	MassSol 0.7
	RadSol  0.68
	Teff    4270

	Orbit
	{
		Period          9.39
		SemiMajorAxis   3.1	// mass ratio * 5.3
		Eccentricity    0.31
		Inclination     18.2
		ArgOfPericenter 180	// from article
		MeanAnomaly     0
	}
}

// data from arXiv:1211.6033
Star	"WASP-77 A"
{
	ParentBody "WASP-77"
	Class  "G8V"
	AppMagn 11.29
	MassSol 1.002
	RadSol  0.955
	Teff    5500
	FeH     0
	Age     1
	
	RotationPeriod 369.6

	Orbit
	{
		SemiMajorAxis   127.3	// mass ratio * 306.9 AU (3.3 arcsec)
		Eccentricity    0.4		// random
		ArgOfPericenter 180		// random
		MeanAnomaly     0
	}
}

Star	"WASP-77 B"
{
	ParentBody "WASP-77"
	Class  "K5V"
	AppMagn 13.40	// SIMBAD
	MassSol 0.71
	RadSol  0.69
	Teff    4700
	FeH     -0.12

	Orbit
	{
		SemiMajorAxis   179.6	// mass ratio * 306.9 AU (3.3 arcsec)
		Eccentricity    0.4		// random
		ArgOfPericenter 0		// random
		MeanAnomaly     0
	}
}

// data from arXiv:1410.3449
Star	"WASP-87 A"
{
	ParentBody "WASP-87"
	Class  "F5"
	AppMagn 10.7
	MassSol 1.204
	RadSol  1.627
	Teff    6450
	FeH    -0.41
	Age     3.8

	Orbit
	{
		SemiMajorAxis   774		// mass ratio * 1968 AU (8.2 arcsec)
		Eccentricity    0.3		// random
		ArgOfPericenter 0		// random
		MeanAnomaly     0
	}
}

Star	"WASP-87 B/2MASS 12211848-5250332"
{
	ParentBody "WASP-87"
	Class  "G6V"	// guess
	AppMagn 12.8
	MassSol 0.78	// guess
	Teff    5700

	Orbit
	{
		SemiMajorAxis   1194	// mass ratio * 1968 AU (8.2 arcsec)
		Eccentricity    0.3		// random
		ArgOfPericenter 180		// random
		MeanAnomaly     0
	}
}

// data from arXiv:1409.7566v2
Star	"WASP-94 A/2MASS 20550794-3408079"
{
	ParentBody "WASP-94"
	AppMagn 10.1
	Class  "F8"
	MassSol 1.29
	RadSol  1.36
	Teff    6170
	FeH     0.26

	RotationPeriod 468

	Orbit
	{
		SemiMajorAxis   1326	// mass ratio * 2705 AU (15.0297 arcsec)
		Eccentricity    0.3		// random
		ArgOfPericenter 0		// random
		MeanAnomaly     0
	}
}

Star	"WASP-94 B/2MASS 20550915-3408078"
{
	ParentBody "WASP-94"
	AppMagn 10.5
	Class  "F9"
	MassSol 1.24
	RadSol  1.35
	Teff    6040
	FeH     0.23

	RotationPeriod 1092 // lower limit

	Orbit
	{
		SemiMajorAxis   1379	// mass ratio * 2705 AU (15.0297 arcsec)
		Eccentricity    0.3		// random
		ArgOfPericenter 180		// random
		MeanAnomaly     0
	}
}

Star	"HD 20782/HIP 15527/SAO 168469/LTT 1582/WDS J03201-2851 A"
{
	ParentBody "LDS 93 AB"
	Class  "G2V"
	AppMagn 7.38
	MassSol 1
	Teff    5578
	FeH    -0.05
	Age     7.1

	Orbit
	{
		SemiMajorAxis   4172	// mass ratio * 9080 AU (252 arcsec)
		Eccentricity    0.1		// random
		ArgOfPericenter 90		// random
		MeanAnomaly     0
	}
}

// data from: Wikipedia
Star	"HD 20781/HIP 15526/SAO 168468/LTT 1581/WDS J03201-2851 B"
{
	ParentBody "LDS 93 AB"
	Class  "G9.5V"
	AppMagn 8.44
	MassSol 0.85
	RadSol  0.82
	Teff    5236
	FeH    -0.125

	Orbit
	{
		SemiMajorAxis   4908	// mass ratio * 9080 AU (252 arcsec)
		Eccentricity    0.1		// random
		ArgOfPericenter 270		// random
		MeanAnomaly     0
	}
}

Star	"XO-2 S"
{
	ParentBody "XO-2"
	Class  "G9V"
	AppMagn 11.086
	MassSol 0.982
	RadSol  1.02
	Teff    5399
	FeH     0.39
	Age     7.1

	Orbit
	{
		SemiMajorAxis   1989	// mass ratio * 4000 AU (30 arcsec)
		Eccentricity    0.2		// random
		ArgOfPericenter 270		// random
		MeanAnomaly     0
	}
}

Star	"XO-2 N"
{
	ParentBody "XO-2"
	Class  "K0V"
	AppMagn 11.18
	MassSol 0.971
	RadSol  0.964
	Teff    5340
	FeH     0.45
	Age     6.3

	Orbit
	{
		SemiMajorAxis   2011	// mass ratio * 4000 AU (30 arcsec)
		Eccentricity    0.2		// random
		ArgOfPericenter 90		// random
		MeanAnomaly     0
	}
}

// data from 1402.6352v1
Star	"Kepler-132 A/KOI-284 A/KIC 6021275 A"
{
	ParentBody "Kepler-132"
	Class   "G0V"	// guess
	AppMagn 11.818
	MassSol 0.919
	RadSol  1.13
	Teff    5963
	FeH    -0.026

	Orbit
	{
		SemiMajorAxis   215	// mass ratio * 450 AU (0.9 arcsec)
		ArgOfPericenter 180	// random
		MeanAnomaly     0
	}
}

Star	"Kepler-132 B/KOI-284 B/KIC 6021275 B"
{
	ParentBody "Kepler-132"
	Class   "G4V"	// guess
	AppMagn 13		// guess
	RadSol  1.07
	MassSol 0.84	// guess
	Teff    5792
	FeH    -0.03

	Orbit
	{
		SemiMajorAxis   235	// mass ratio * 450 AU (0.9 arcsec)
		ArgOfPericenter 0	// random
		MeanAnomaly     0
	}
}

// data from 1402.6352v1
Star	"Kepler-296 A/KOI-1422 A/KIC 11497958 A"
{
	ParentBody "Kepler-296"
	Class   "M1V"
	AppMagn 15.921
	MassSol 0.445
	RadSol  0.56
	Teff    3581
	FeH     0.168
	Age     13

	Orbit
	{
		SemiMajorAxis   14.24	// mass ratio * 49.72 AU (0.22 arcsec)
		Inclination		85		// guess
		ArgOfPericenter 180		// random
		MeanAnomaly     0
	}
}

Star	"Kepler-296 B/KOI-1422 B/KIC 11497958 B"
{
	ParentBody "Kepler-296"
	Class   "M3V"
	AppMagn 17.426	// 20% of the system flux
	MassSol 0.18	// guess
	RadSol  0.42	// 0.75 of A radius

	Orbit
	{
		SemiMajorAxis   35.51	// mass ratio * 49.72 AU (0.22 arcsec)
		Inclination		85		// guess
		ArgOfPericenter 0		// random
		MeanAnomaly     0
	}
}

// data from arXiv:1107.0918v1
Barycenter	"HD 132563 A/HIP 73261 A"
{
	ParentBody "HD 132563"
	MassSol 1.641

	Orbit	// lower limits
	{
		SemiMajorAxis   152	// mass ratio * 400 AU (4.1 arcsec)
		Eccentricity    0.2
		ArgOfPericenter 180	// random
		MeanAnomaly     0
	}
}

Star	"HD 132563 Aa/HIP 73261 Aa"
{
	ParentBody "HD 132563 A"
	Class  "F8V"
	AppMagn 8.966
	MassSol 1.081
	Teff    6168
	FeH    -0.18
	Age     5

	Orbit	// mean values
	{
		Epoch           2402012.34
		Period          47
		SemiMajorAxis   5.05	// mass ratio * 14.8 AU
		Eccentricity    0.86
		ArgOfPericenter 340.2
		MeanAnomaly     0
	}
}

Star	"HD 132563 Ab/HIP 73261 Ab"
{
	ParentBody "HD 132563 A"
	AppMagn 14.35 // 0.7% of the A binary flux
	MassSol 0.56

	Orbit	// mean values
	{
		Epoch           2402012.34
		Period          47
		SemiMajorAxis   9.75	// mass ratio * 14.8 AU
		Eccentricity    0.86
		ArgOfPericenter 160.2
		MeanAnomaly     0
	}
}

Star	"HD 132563 B/HIP 73261 B"
{
	ParentBody "HD 132563"
	Class  "G0V"
	AppMagn 9.472
	MassSol 1.01
	Teff    5985
	FeH    -0.19
	Age     5

	Orbit	// lower limits
	{
		SemiMajorAxis   248	// mass ratio * 400 AU (4.1 arcsec)
		Eccentricity    0.2
		ArgOfPericenter 0	// random
		MeanAnomaly     0
	}
}

// data from arXiv:1503.01211v1
Star	"HD 2567/HIP 2292/SAO 128781"
{
	ParentBody "WDS 00293-0555"
	Class  "G0V"
	AppMagn 7.76
	MassSol 1.1	// guess
	
	Orbit
	{
		SemiMajorAxis   26050	// mass ratio * 47600 AU (839 arcsec)
		Eccentricity    0.2
		ArgOfPericenter 180	// random
		MeanAnomaly     0
	}
}

Barycenter	"HD 2638/HIP 2350"
{
	ParentBody "WDS 00293-0555"
	MassSol 1.33

	Orbit
	{
		SemiMajorAxis   21550	// mass ratio * 47600 AU (839 arcsec)
		Eccentricity    0.2
		ArgOfPericenter 0	// random
		MeanAnomaly     0
	}
}

Star	"HD 2638 B/HIP 2350 B"
{
	ParentBody "HD 2638"
	Class  "G8V"
	AppMagn 8.96
	MassSol 0.87
	Teff    5192
	FeH     0.16
	Age     3

	Orbit
	{
		Period          130
		SemiMajorAxis   9.9		// mass ratio * 28.5
		ArgOfPericenter 0		// random
		MeanAnomaly     0
	}
}

Star	"HD 2638 C/HIP 2350 C"
{
	ParentBody "HD 2638"
	Class  "M1V"
	AppMagn 12.25
	MassSol 0.46

	Orbit
	{
		Period          130
		SemiMajorAxis   18.6	// mass ratio * 28.5
		ArgOfPericenter 180		// random
		MeanAnomaly     0
	}
}

// Data from: arXiv:1512.00189v1
Star	"KOI-2939 A"
{
	ParentBody "KOI-2939"
	Class  "FV"
	AppMagn 13.78
	MassSol 1.210
	RadSol  1.7903
	Teff    6210
	FeH     -0.14
	Age     4.4

	Orbit
	{
		Epoch           2454952.13097
		Period          0.03082563
		Eccentricity    0.1593
		SemiMajorAxis   0.0569462
		Inclination     87.9305
		ArgOfPericenter 120.85
		MeanAnomaly     0
	}
}

Star	"KOI-2939 B"
{
	ParentBody "KOI-2939"
	Class  "GV"
	MassSol 0.975
	RadSol  0.9663
	Teff    5770

	Orbit
	{
		Epoch           2454952.13097
		Period          0.03082563
		Eccentricity    0.1593
		SemiMajorAxis   0.070671
		Inclination     87.9305
		ArgOfPericenter 300.85
		MeanAnomaly     0
	}
}

Star	"Gliese 229 A"
{
	ParentBody     "Gliese 229"
	Class          "M1"
	AppMagn        8.125
	MassSol        0.58
	RadSol         0.69
	Teff           3564
	Age            3.0
	RotationPeriod 838	// 1 km/s equatorial speed

	Orbit
	{
		SemiMajorAxis   1.72	// 35 * mass ratio
		ArgOfPericen    0		// random
		MeanAnomaly     0
	}
}

Star	"Gliese 229 B"
{
	ParentBody     "Gliese 229"
	Class          "T0"
	Luminosity     0.00032
	MassSol        0.03
	Teff           950
	DiscMethod     "Imaging"
	DiscDate       "1994"

	Orbit
	{
		SemiMajorAxis   33.28	// 35 * mass ratio
		ArgOfPericen    180		// random
		MeanAnomaly     0
	}
}

// HIP 72940/HD 131399
// http://exoplanet.eu/catalog/hd_131399a_b/
// http://www.openexoplanetcatalogue.com/planet/HD%20131399%20Ab/
// https://de.wikipedia.org/wiki/HD_131399_Ab
// 2016.07.11 12:31:02

Star "HD 131399 A"
{
    ParentBody  "HD 131399"
    Class       "A1V"
    MassSol     1.820
    AppMagn     7.07
    Teff        9300
    Age         0.016
    Orbit
    {
        SemiMajorAxis   161.64
        Period          3556
        Inclination     40	// taken from planet b
        Eccentricity    0.2	// no data
        ArgOfPericen    35  // no data
		MeanAnomaly     0
        RefPlane        "ExtraSolar"
    }
}

Barycenter  "HD 131399 (BC)"
{
    ParentBody  "HD 131399"
    MassSol     1.56
    Orbit
    {
        SemiMajorAxis   188.52
        Period          3556
        Inclination     40	// taken from planet b
        Eccentricity    0.2	// no data
        ArgOfPericen    215 // no date
		MeanAnomaly     0
        RefPlane        "ExtraSolar"
    }
}

Star "HD 131399 B"
{
    ParentBody  "HD 131399 (BC)"
    Class       "G"
    MassSol     0.960
    Teff        5700
    Orbit
    {
        SemiMajorAxis   2.88
        Period          16.445
        Inclination     43	// taken from planet b
        Eccentricity    0.3	// no data
        ArgOfPericen    0	// no data
		MeanAnomaly     0
        RefPlane        "ExtraSolar"
    }
}

Star "HD 131399 C"
{
    ParentBody  "HD 131399 (BC)"
    Class       "K"
    MassSol     0.600
    Teff        4400
    Orbit
    {
        SemiMajorAxis   4.62
        Period          16.445
        Inclination     43	// taken from planet b
        Eccentricity    0.3	// no data
        ArgOfPericen    180	// no data
		MeanAnomaly     0
        RefPlane        "ExtraSolar"
    }
}
